using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class GetUnipileWebhookEventsHandler(ILinkedInWebhookRepository inbox, IValidator<PaginationRequest> validator)
{
    public async Task<Result<List<LinkedInWebhookEvent>>> HandleAsync(PaginationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<List<LinkedInWebhookEvent>>.Invalid(validation.AsErrors());

        return Result<List<LinkedInWebhookEvent>>.Success(await inbox.GetPageAsync((request.Page - 1) * request.PageSize, request.PageSize, ct));
    }
}