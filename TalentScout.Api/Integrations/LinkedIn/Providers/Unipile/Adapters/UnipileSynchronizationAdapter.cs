using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileSynchronizationAdapter(
    IUnipileProfilesApi profiles,
    IUnipileMessagingApi messaging,
    ILinkedInContacts contacts,
    ILinkedInConversations chats,
    IOptions<LinkedInOptions> options) : ILinkedInSynchronization
{
    public async Task<LinkedInSyncSnapshot> ReadAsync(string recipientId, string? chatId, CancellationToken ct)
    {
        var account = options.Value.AccountId;
        var contact = await contacts.GetStatusAsync(recipientId, ct);
        var invitation = await GetPendingInvitationAsync(account, recipientId, ct);
        var chat = await FindChatAsync(account, recipientId, chatId, ct);
        var messages = await GetMessagesAsync(account, chat, ct);

        return new LinkedInSyncSnapshot(
            contact with { InvitationPending = invitation is not null || contact.InvitationPending },
            chat,
            messages,
            invitation?.Id,
            invitation?.InvitationText);
    }

    private async Task<UnipileSentInvitation?> GetPendingInvitationAsync(string account, string recipient, CancellationToken ct)
    {
        var invitations = await UnipilePagination.ReadAllAsync((cursor, token) => profiles.GetSentInvitationsAsync(account, cursor, token), ct);

        if (invitations.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.InvitedUserId)))
            throw new ApiException(502, "Unipile returned an incomplete invitation list.");

        return invitations.FirstOrDefault(x => x.InvitedUserId == recipient);
    }

    private async Task<IReadOnlyList<LinkedInSyncMessage>> GetMessagesAsync(string account, LinkedInChat? chat, CancellationToken ct)
    {
        if (chat is null)
            return [];

        var remote = await UnipilePagination.ReadAllAsync((cursor, token) => messaging.GetMessagesAsync(account, chat.ChatId, cursor, token), ct);

        return remote.Where(x => x.IsEvent != true && x.Hidden != true).Select(x => MapMessage(x, account, chat.ChatId)).ToArray();
    }

    private async Task<LinkedInChat?> FindChatAsync(string account, string recipient, string? chatId, CancellationToken ct)
    {
        if (chatId is null)
        {
            IReadOnlyList<UnipileChatResponse> matches;
            try
            {
                matches = await UnipilePagination.ReadAllAsync((cursor, token) => messaging.GetChatsForAttendeeAsync(account, recipient, cursor, token), ct);
            }
            catch (ApiException exception) when (exception.Status == 404)
            {
                matches = [];
            }

            var candidates = matches.Where(x => x.AccountId == account).ToArray();

            if (candidates.Length > 1)
                throw new ApiException(409, "Multiple LinkedIn chats found. Bind the intended conversation before synchronizing.");

            chatId = candidates.SingleOrDefault()?.Id;
        }

        if (string.IsNullOrWhiteSpace(chatId))
            return null;

        var chat = await chats.GetChatAsync(chatId, ct);

        if (chat.AccountId != account || (chat.RecipientId is not null && chat.RecipientId != recipient))
            throw new ApiException(409, "LinkedIn chat does not belong to this candidate and account.");

        return chat;
    }

    private static LinkedInSyncMessage MapMessage(UnipileChatMessage message, string account, string chatId)
    {
        if (string.IsNullOrWhiteSpace(message.Id) || message.ChatId != chatId || message.Timestamp is null || message.IsSender is null ||
            (message.AccountId is not null && message.AccountId != account))
            throw new ApiException(502, "Unipile returned an invalid synchronization message.");

        return new LinkedInSyncMessage(
            UnipileIdentifiers.Message(account, message.Id),
            message.Text ?? "",
            message.Timestamp.Value,
            message.IsSender.Value,
            message.Deleted == true,
            message.Edited == true,
            message.Attachments?.Count > 0);
    }
}