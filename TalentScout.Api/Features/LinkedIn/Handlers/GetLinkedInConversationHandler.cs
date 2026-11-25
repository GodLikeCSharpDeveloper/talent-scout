using Ardalis.Result;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Data.Repositories;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class GetLinkedInConversationHandler(
    ILinkedInConversationRepository conversations,
    ILinkedInConversations linkedInConversations,
    IOptions<LinkedInOptions> options,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<LinkedInConversationResponse>> HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await conversations.FindForEnrollmentAsync(id, ct);
        if (item is { ChatId: not null, ProviderChatId: null } && options.Value.IsConfigured && item.AccountId == options.Value.AccountId)
        {
            var chat = await linkedInConversations.GetChatAsync(item.ChatId, ct);
            item.ProviderChatId = chat.ProviderChatId;
            await unitOfWork.SaveChangesAsync(ct);
        }

        return item is null
            ? Result<LinkedInConversationResponse>.NotFound("LinkedIn conversation was not found.")
            : Result<LinkedInConversationResponse>.Success(
                new LinkedInConversationResponse(
                    item.EnrollmentId,
                    item.AccountId,
                    item.RecipientId,
                    item.ChatId,
                    LinkedInConversationUrl.Create(item.ProviderChatId),
                    item.ConnectionRequestDelivery,
                    item.InvitationId,
                    item.ConnectionConfirmedAt));
    }
}