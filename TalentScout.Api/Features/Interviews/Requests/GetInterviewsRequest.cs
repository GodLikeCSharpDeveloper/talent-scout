using TalentScout.Api.Shared.Requests;

namespace TalentScout.Api.Features.Interviews.Requests;

public sealed class GetInterviewsRequest : PaginationRequest
{
    public Guid? CampaignId { get; init; }
}