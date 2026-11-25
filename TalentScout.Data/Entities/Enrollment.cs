namespace TalentScout.Data.Entities;

public enum CandidateStatus
{
    Found,
    Screened,
    Rejected,
    FirstMessageSent,
    AwaitingReply,
    Interested,
    NotInterested,
    NeedsReview,
    InterviewInvitationSent,
    InterviewBooked
}

public sealed class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public Guid? SearchBatchId { get; set; }
    public OutreachApprovalStatus ApprovalStatus { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? OutreachStartedAt { get; set; }
    public CandidateStatus Status { get; set; }
    public decimal? FitScore { get; set; }
    public string? Assessment { get; set; }
    public bool HasSentInitialMessage { get; set; }
    public bool HasSentInvitation { get; set; }
    public DateTimeOffset? NextContactAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}