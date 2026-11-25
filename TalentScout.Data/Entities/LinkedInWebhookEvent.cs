namespace TalentScout.Data.Entities;

public sealed class LinkedInWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = "message_received";
    public string AccountId { get; set; } = "";
    public string ExternalMessageId { get; set; } = "";
    public string ChatId { get; set; } = "";
    public string? ProviderChatId { get; set; }
    public string SenderId { get; set; } = "";
    public string Content { get; set; } = "";
    public bool HasAttachments { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}