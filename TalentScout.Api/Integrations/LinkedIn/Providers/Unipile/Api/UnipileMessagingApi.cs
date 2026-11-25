using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;

public sealed class UnipileMessagingApi(IUnipileHttpClient http) : IUnipileMessagingApi
{
    public Task<UnipileChatResponse> GetChatAsync(string accountId, string chatId, CancellationToken ct)
    {
        return http.GetAsync<UnipileChatResponse>(
            $"chats/{Uri.EscapeDataString(chatId)}?account_id={Uri.EscapeDataString(accountId)}",
            ct);
    }

    public async Task<UnipileSendResponse> SendAsync(UnipileSendMessageRequest request, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.AccountId), "account_id");
        form.Add(new StringContent(request.Text), "text");
        var path = "chats";
        if (request.ChatId is null)
        {
            form.Add(new StringContent(request.RecipientId), "attendees_ids");
            if (request.UseInMail)
            {
                form.Add(new StringContent("true"), "linkedin[inmail]");
                form.Add(new StringContent("classic"), "linkedin[api]");
                form.Add(new StringContent("Career opportunity"), "subject");
            }
        }
        else
            path = $"chats/{Uri.EscapeDataString(request.ChatId)}/messages";

        return await http.SendAsync<UnipileSendResponse>(HttpMethod.Post, path, form, ct);
    }
}