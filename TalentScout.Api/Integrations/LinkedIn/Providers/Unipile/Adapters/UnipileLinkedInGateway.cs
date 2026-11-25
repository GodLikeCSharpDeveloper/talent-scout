using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;

public sealed class UnipileLinkedInGateway(
    IUnipileSearchApi search,
    IUnipileMessagingApi messaging,
    IOptions<LinkedInOptions> options) : ILinkedInGateway
{
    public async Task<ProfileSearchResult> SearchAsync(ProfileSearch request, CancellationToken ct)
    {
        var page = await search.SearchAsync(
            new UnipilePeopleSearchRequest(
                options.Value.AccountId,
                request.Query,
                request.LocationIds,
                request.Limit,
                request.Cursor),
            ct);

        if (page.Items is null || page.Items.Count > 100 || page.Items.Any(x => string.IsNullOrWhiteSpace(x.Id)))
            throw new ApiException(502, "Unipile returned an invalid LinkedIn search page.");

        return new ProfileSearchResult(
            page.Items.Select(x => new LinkedInProfileReference(x.Id)).ToList(),
            string.IsNullOrEmpty(page.Cursor) ? null : page.Cursor);
    }

    public async Task<SentMessage> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        var result = await messaging.SendAsync(
            new UnipileSendMessageRequest(
                options.Value.AccountId,
                message.RecipientId,
                message.Text,
                message.ChatId,
                options.Value.UseInMail),
            ct);
        var chatId = result.ChatId ?? message.ChatId;

        if (string.IsNullOrWhiteSpace(result.MessageId) || string.IsNullOrWhiteSpace(chatId))
            throw new ApiException(502, "Unipile did not confirm the message and chat identifiers.");

        return new SentMessage(UnipileIdentifiers.Message(options.Value.AccountId, result.MessageId), chatId);
    }
}