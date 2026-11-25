using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Abstractions;

public interface ILinkedInConversations
{
    Task<LinkedInChat> GetChatAsync(string chatId, CancellationToken ct);
}