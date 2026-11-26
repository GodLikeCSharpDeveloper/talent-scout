using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public interface IUnipileMessagingApi
{
    Task<UnipileSendResponse> SendAsync(UnipileSendMessageRequest request, CancellationToken ct);
    Task<UnipileChatResponse> GetChatAsync(string accountId, string chatId, CancellationToken ct);
    Task<UnipilePage<UnipileChatMessage>> GetMessagesAsync(string accountId, string chatId, string? cursor, CancellationToken ct);
    Task<UnipilePage<UnipileChatResponse>> GetChatsForAttendeeAsync(string accountId, string attendeeId, string? cursor, CancellationToken ct);
}