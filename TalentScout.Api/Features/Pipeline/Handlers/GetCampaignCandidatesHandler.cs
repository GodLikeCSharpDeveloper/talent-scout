using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Features.Pipeline.Responses;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class GetCampaignCandidatesHandler(ICampaignRepository campaigns, IEnrollmentRepository enrollments, IValidator<GetCampaignCandidatesRequest> validator)
{
    public async Task<Result<List<CampaignCandidateResponse>>> HandleAsync(Guid id, GetCampaignCandidatesRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<CampaignCandidateResponse>>.Invalid(validation.AsErrors());

        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        if (!await campaigns.ExistsAsync(id, ct))
            return Result<List<CampaignCandidateResponse>>.NotFound("Campaign was not found.");

        var records = await enrollments.GetCampaignPageAsync(id, request.Status, request.SearchBatchId, request.ApprovalStatus, skip, take, ct);

        return Result<List<CampaignCandidateResponse>>.Success(
            records.Select(x => new CampaignCandidateResponse(
                x.Id,
                x.CandidateId,
                x.Name,
                x.Status,
                x.FitScore,
                x.Assessment,
                x.HasSentInitialMessage,
                x.HasSentInvitation,
                x.SearchBatchId,
                x.ApprovalStatus,
                x.ApprovedAt,
                x.OutreachStartedAt)).ToList());
    }
}