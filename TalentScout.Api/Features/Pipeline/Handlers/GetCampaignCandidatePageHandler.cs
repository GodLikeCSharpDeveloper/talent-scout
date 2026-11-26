using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Data.ReadModels;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Pipeline.Handlers;

public sealed class GetCampaignCandidatePageHandler(ICampaignRepository campaigns, IEnrollmentRepository enrollments, IValidator<GetCampaignCandidatesRequest> validator)
{
    public async Task<Result<CampaignCandidatePage>> HandleAsync(Guid id, GetCampaignCandidatesRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<CampaignCandidatePage>.Invalid(validation.AsErrors());
        if (!await campaigns.ExistsAsync(id, ct))
            return Result<CampaignCandidatePage>.NotFound("Campaign was not found.");

        return Result<CampaignCandidatePage>.Success(await enrollments.GetCandidatePageAsync(request.ToQuery(id), request.Page, request.PageSize, ct));
    }
}