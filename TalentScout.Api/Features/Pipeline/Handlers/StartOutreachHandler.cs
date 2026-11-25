using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class StartOutreachHandler(
    ICampaignRepository campaigns,
    IEnrollmentRepository enrollments,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IValidator<StartOutreachRequest> validator)
{
    public async Task<Result<StartOutreachResponse>> HandleAsync(Guid id, StartOutreachRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<StartOutreachResponse>.Invalid(validation.AsErrors());

        var campaign = await campaigns.FindAsync(id, ct);

        if (campaign is null)
            return Result<StartOutreachResponse>.NotFound("Campaign was not found.");
        if (campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped)
            return Result<StartOutreachResponse>.Conflict("Resume a paused campaign before starting outreach. Stopped campaigns cannot be restarted.");

        var selected = await enrollments.GetSelectedAsync(id, request.EnrollmentIds, ct);

        if (selected.Count != request.EnrollmentIds.Count)
            return Result<StartOutreachResponse>.NotFound("Some selected candidates do not belong to this campaign.");
        if (selected.Any(x => x.ApprovalStatus == OutreachApprovalStatus.Excluded ||
                              (x.OutreachStartedAt is null && (x.Status != CandidateStatus.Screened || x.HasSentInitialMessage))))
            return Result<StartOutreachResponse>.Conflict("Only screened, non-excluded candidates can start outreach. No candidates were started.");

        var started = new List<Guid>();
        var alreadyStarted = new List<Guid>();
        var now = DateTimeOffset.UtcNow;
        foreach (var enrollment in selected)
        {
            if (enrollment.OutreachStartedAt.HasValue)
            {
                if (enrollment.ApprovalStatus != OutreachApprovalStatus.Approved)
                {
                    enrollment.ApprovalStatus = OutreachApprovalStatus.Approved;
                    enrollment.ApprovedAt = now;
                    audit.Add(id, enrollment.Id, "OutreachManuallyApproved", "Existing automatic outreach");
                }
                alreadyStarted.Add(enrollment.Id);

                continue;
            }

            enrollment.ApprovalStatus = OutreachApprovalStatus.Approved;
            enrollment.ApprovedAt = now;
            enrollment.OutreachStartedAt = now;
            enrollment.NextContactAt = null;
            started.Add(enrollment.Id);
            audit.Add(id, enrollment.Id, "OutreachApprovedAndStarted", "Manual selection");
        }

        if (started.Count > 0 && campaign.Status == CampaignStatus.Draft)
        {
            campaign.Status = CampaignStatus.Active;
            audit.Add(id, null, "CampaignStatusChanged", CampaignStatus.Active.ToString());
        }

        await unitOfWork.SaveChangesAsync(ct);

        return Result<StartOutreachResponse>.Success(new StartOutreachResponse(started, alreadyStarted));
    }
}