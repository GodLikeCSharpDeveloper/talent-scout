using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public interface IUnipileMessagingApi
{
    Task<UnipileSendResponse> SendAsync(UnipileSendMessageRequest request, CancellationToken ct);
    Task<UnipileChatResponse> GetChatAsync(string accountId, string chatId, CancellationToken ct);
}