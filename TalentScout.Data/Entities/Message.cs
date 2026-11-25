namespace TalentScout.Data.Entities;

public enum MessageKind
{
    Initial,
    Invitation,
    Reply
}

public enum DeliveryStatus
{
    Pending,
    Sending,
    Sent,
    Received,
    Unknown,
    Cancelled
}

public enum Interest
{
    Interested,
    NotInterested,
    Ambiguous
}

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; set; }
    public MessageKind Kind { get; set; }
    public string Content { get; set; } = "";
    public string? ExternalId { get; set; }
    public DeliveryStatus Delivery { get; set; }
    public Interest? Interest { get; set; }
    public decimal? Confidence { get; set; }
    public string? Analysis { get; set; }
    public bool Analyzed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AttemptedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}