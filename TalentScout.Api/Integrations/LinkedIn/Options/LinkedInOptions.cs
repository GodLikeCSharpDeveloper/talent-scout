namespace TalentScout.Api.Integrations.LinkedIn.Options;

public sealed class LinkedInOptions
{
    public string Provider { get; set; } = "Disabled";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
    public bool UseInMail { get; set; }
    public bool IsConfigured => Provider == "Unipile" && !string.IsNullOrWhiteSpace(ApiKey);
}