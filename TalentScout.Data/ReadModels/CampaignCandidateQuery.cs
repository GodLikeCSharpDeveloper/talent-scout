using TalentScout.Data.Entities;

namespace TalentScout.Data.ReadModels;

public enum CandidateSortField
{
    Name,
    FitScore,
    Status,
    FirstContact,
    LinkedInStatus
}

public enum LinkedInOutreachStatus
{
    NotStarted,
    Sending,
    Pending,
    Connected,
    Conversation,
    Withdrawn,
    Declined,
    Inactive,
    Failed
}

public sealed record CampaignCandidateQuery(Guid CampaignId)
{
    public CandidateStatus? Status { get; init; }
    public Guid? SearchBatchId { get; init; }
    public OutreachApprovalStatus? ApprovalStatus { get; init; }
    public InvitationApprovalStatus? InvitationApprovalStatus { get; init; }
    public string? Search { get; init; }
    public bool NeedsDecision { get; init; }
    public CandidateSortField SortBy { get; init; } = CandidateSortField.FitScore;
    public bool Descending { get; init; } = true;
}

public sealed record CampaignCandidatePage(List<CampaignCandidateRecord> Items, int Total, int Page, int PageSize);