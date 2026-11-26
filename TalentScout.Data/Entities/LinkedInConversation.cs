namespace TalentScout.Data.Entities;

public sealed class LinkedInConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public string AccountId { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public string? ChatId { get; set; }
    public string? ProviderChatId { get; set; }
    public string? InvitationId { get; set; }
    public string? ConnectionNote { get; set; }
    public string ConnectionState { get; set; } = "Unknown";
    public DateTimeOffset? LastSynchronizedAt { get; set; }
    public DateTimeOffset? NextSynchronizationAt { get; set; }
    public DateTimeOffset? SynchronizationLeaseUntil { get; set; }
    public string? SynchronizationError { get; set; }
    public DeliveryStatus? ConnectionRequestDelivery { get; set; }
    public DateTimeOffset? ConnectionConfirmedAt { get; set; }
    public DateTimeOffset? ConnectionRequestedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}