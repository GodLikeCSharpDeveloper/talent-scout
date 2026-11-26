using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;
using TalentScout.Data.ReadModels;

namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed class GetCampaignCandidatesRequest : PaginationRequest
{
    public Guid? SearchBatchId { get; init; }
    public OutreachApprovalStatus? ApprovalStatus { get; init; }
    public InvitationApprovalStatus? InvitationApprovalStatus { get; init; }
    public CandidateStatus? Status { get; init; }
    public string? Search { get; init; }
    public bool NeedsDecision { get; init; }
    public CandidateSortField SortBy { get; init; } = CandidateSortField.FitScore;
    public bool Descending { get; init; } = true;

    public CampaignCandidateQuery ToQuery(Guid campaignId)
    {
        return new CampaignCandidateQuery(campaignId)
        {
            Status = Status, SearchBatchId = SearchBatchId, ApprovalStatus = ApprovalStatus,
            InvitationApprovalStatus = InvitationApprovalStatus, Search = Search?.Trim(),
            NeedsDecision = NeedsDecision, SortBy = SortBy, Descending = Descending
        };
    }
}