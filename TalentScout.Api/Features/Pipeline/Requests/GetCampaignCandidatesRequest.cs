using TalentScout.Api.Shared.Requests;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Pipeline.Requests;

public sealed class GetCampaignCandidatesRequest : PaginationRequest
{
    public Guid? SearchBatchId { get; init; }
    public OutreachApprovalStatus? ApprovalStatus { get; init; }
    public InvitationApprovalStatus? InvitationApprovalStatus { get; init; }
    public CandidateStatus? Status { get; init; }
}