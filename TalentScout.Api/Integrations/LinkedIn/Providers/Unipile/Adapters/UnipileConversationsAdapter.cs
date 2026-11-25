using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileConversationsAdapter(IUnipileMessagingApi messaging, IOptions<LinkedInOptions> options) : ILinkedInConversations
{
    public async Task<LinkedInChat> GetChatAsync(string chatId, CancellationToken ct)
    {
        var chat = await messaging.GetChatAsync(options.Value.AccountId, chatId, ct);

        if (chat.AccountId != options.Value.AccountId || string.IsNullOrWhiteSpace(chat.Id))
            throw new ApiException(502, "Unipile returned a chat from another account.");

        return new LinkedInChat(chat.AccountId, chat.Id, chat.ProviderId, chat.AttendeeProviderId);
    }
}