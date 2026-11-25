using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.Audit.Handlers;

public sealed class GetAuditEventsHandler(ICampaignRepository campaigns, IAuditRepository audit, IValidator<PaginationRequest> validator)
{
    public async Task<Result<List<AuditEvent>>> HandleAsync(Guid campaignId, PaginationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<AuditEvent>>.Invalid(validation.AsErrors());

        var skip = (request.Page - 1) * request.PageSize;
        var take = request.PageSize;

        if (!await campaigns.ExistsAsync(campaignId, ct))
            return Result<List<AuditEvent>>.NotFound("Campaign was not found.");

        return Result<List<AuditEvent>>.Success(await audit.GetPageAsync(campaignId, skip, take, ct));
    }
}