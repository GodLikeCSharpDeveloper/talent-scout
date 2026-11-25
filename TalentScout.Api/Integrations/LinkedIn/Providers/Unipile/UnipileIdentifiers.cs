namespace TalentScout.Api.Integrations.LinkedIn.Providers.Unipile;

public static class UnipileIdentifiers
{
    public static string Message(string accountId, string messageId)
    {
        return $"unipile:{accountId}:{messageId}";
    }
}