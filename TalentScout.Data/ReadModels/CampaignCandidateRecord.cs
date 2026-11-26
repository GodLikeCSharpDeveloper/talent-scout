using TalentScout.Data.Entities;

namespace TalentScout.Data.ReadModels;

public sealed record CampaignCandidateRecord
{
    public Guid Id { get; init; }
    public Guid CandidateId { get; init; }
    public string Name { get; init; } = "";
    public CandidateStatus Status { get; init; }
    public decimal? FitScore { get; init; }
    public string? Assessment { get; init; }
    public bool HasSentInitialMessage { get; init; }
    public bool HasSentInvitation { get; init; }
    public Guid? SearchBatchId { get; init; }
    public OutreachApprovalStatus ApprovalStatus { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? OutreachStartedAt { get; init; }
    public InvitationApprovalStatus InvitationApprovalStatus { get; init; }
    public DateTimeOffset? InvitationApprovedAt { get; init; }
    public DateTimeOffset? NextContactAt { get; init; }
    public DateTimeOffset? LastFailedAttemptAt { get; init; }
    public string? ProfileUrl { get; init; }
    public string? ProviderChatId { get; init; }
    public DeliveryStatus? ConnectionRequestDelivery { get; init; }
    public string? ConnectionState { get; init; }
    public DateTimeOffset? LastSynchronizedAt { get; init; }
    public string? SynchronizationError { get; init; }
    public bool RetryRequiresConfirmation { get; init; }
    public LinkedInOutreachStatus LinkedInStatus { get; init; }
}