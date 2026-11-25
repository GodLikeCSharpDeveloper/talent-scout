using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class ExcludeCandidatesHandler(
    ICampaignRepository campaigns,
    IEnrollmentRepository enrollments,
    IAuditRepository audit,
    IUnitOfWork unitOfWork,
    IValidator<ExcludeCandidatesRequest> validator)
{
    public async Task<Result<ExcludeCandidatesResponse>> HandleAsync(Guid id, ExcludeCandidatesRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<ExcludeCandidatesResponse>.Invalid(validation.AsErrors());
        if (!await campaigns.ExistsAsync(id, ct))
            return Result<ExcludeCandidatesResponse>.NotFound("Campaign was not found.");

        var selected = await enrollments.GetSelectedAsync(id, request.EnrollmentIds, ct);

        if (selected.Count != request.EnrollmentIds.Count)
            return Result<ExcludeCandidatesResponse>.NotFound("Some selected candidates do not belong to this campaign.");
        if (selected.Any(x => x.OutreachStartedAt.HasValue || x.HasSentInitialMessage ||
                              x.Status is not (CandidateStatus.Found or CandidateStatus.Screened or CandidateStatus.Rejected)))
            return Result<ExcludeCandidatesResponse>.Conflict("Outreach has already started for some selected candidates. No candidates were excluded.");

        foreach (var enrollment in selected.Where(x => x.ApprovalStatus != OutreachApprovalStatus.Excluded))
        {
            enrollment.ApprovalStatus = OutreachApprovalStatus.Excluded;
            audit.Add(id, enrollment.Id, "CandidateExcluded", "Manual selection");
        }

        await unitOfWork.SaveChangesAsync(ct);

        return Result<ExcludeCandidatesResponse>.Success(new ExcludeCandidatesResponse(request.EnrollmentIds));
    }
}