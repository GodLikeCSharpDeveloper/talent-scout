namespace TalentScout.Api.Integrations.LinkedIn;

public static class LinkedInConversationUrl
{
    public static string? Create(string? providerChatId)
    {
        if (string.IsNullOrWhiteSpace(providerChatId))
            return null;

        const string prefix = "urn:li:msg_conversation:";
        var id = providerChatId.StartsWith(prefix, StringComparison.Ordinal) ? providerChatId[prefix.Length..] : providerChatId;

        return "https://www.linkedin.com/messaging/thread/" + Uri.EscapeDataString(id) + "/";
    }
}