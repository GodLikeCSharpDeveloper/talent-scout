using Ardalis.Result;
using Ardalis.Result.FluentValidation;
using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class BindLinkedInConversationHandler(
    IEnrollmentRepository enrollments,
    ILinkedInConversationRepository conversations,
    IOptions<LinkedInOptions> options,
    ILinkedInConversations linkedInConversations,
    IUnitOfWork unitOfWork,
    IAuditRepository audit,
    GetLinkedInConversationHandler getConversation,
    IValidator<BindLinkedInConversationRequest> validator)
{
    public async Task<Result<LinkedInConversationResponse>> HandleAsync(Guid id, BindLinkedInConversationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
            return Result<LinkedInConversationResponse>.Invalid(validation.AsErrors());
        if (!options.Value.IsConfigured)
            return Result<LinkedInConversationResponse>.Unavailable("LinkedIn provider is not configured.");

        var enrollment = await enrollments.GetDetailsAsync(id, ct);

        if (enrollment is null)
            return Result<LinkedInConversationResponse>.NotFound("Enrollment was not found.");

        var chat = await linkedInConversations.GetChatAsync(request.ChatId.Trim(), ct);

        if (chat.RecipientId != enrollment.Candidate.ExternalId)
            return Result<LinkedInConversationResponse>.Conflict("The chat does not belong to this candidate or is a group conversation.");

        var item = await conversations.ReserveAsync(id, options.Value.AccountId, enrollment.Candidate.ExternalId, ct);

        if (item.EnrollmentId != id || item.AccountId != options.Value.AccountId)
            return Result<LinkedInConversationResponse>.Conflict("This LinkedIn conversation is already assigned to another campaign.");
        if (item.ChatId is not null && item.ChatId != chat.ChatId)
            return Result<LinkedInConversationResponse>.Conflict("A different chat is already assigned to this enrollment.");

        item.ChatId = chat.ChatId;
        item.ProviderChatId = chat.ProviderChatId;
        audit.Add(enrollment.CampaignId, id, "LinkedInConversationBound");
        await unitOfWork.SaveChangesAsync(ct);

        return await getConversation.HandleAsync(id, ct);
    }
}