using TalentScout.Data.Entities;
using TalentScout.Data.ReadModels;

namespace TalentScout.Api.Features.Pipeline.Responses;

public sealed record CampaignCandidateResponse(
    Guid Id,
    Guid CandidateId,
    string Name,
    CandidateStatus Status,
    decimal? FitScore,
    string? Assessment,
    bool HasSentInitialMessage,
    bool HasSentInvitation,
    Guid? SearchBatchId,
    OutreachApprovalStatus ApprovalStatus,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? OutreachStartedAt,
    InvitationApprovalStatus InvitationApprovalStatus,
    DateTimeOffset? InvitationApprovedAt,
    DateTimeOffset? NextContactAt = null,
    DateTimeOffset? LastFailedAttemptAt = null,
    string? ProfileUrl = null,
    string? ProviderChatId = null,
    DeliveryStatus? ConnectionRequestDelivery = null,
    string? ConnectionState = null,
    DateTimeOffset? LastSynchronizedAt = null,
    string? SynchronizationError = null,
    bool RetryRequiresConfirmation = false,
    LinkedInOutreachStatus LinkedInStatus = LinkedInOutreachStatus.NotStarted);