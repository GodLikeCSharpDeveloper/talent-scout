using FluentValidation;
using TalentScout.Api.Features.Pipeline.Requests;
using TalentScout.Api.Shared.Validation;

namespace TalentScout.Api.Features.Pipeline.Validators;

public sealed class GetCampaignCandidatesValidator : PaginationValidator<GetCampaignCandidatesRequest>
{
    public GetCampaignCandidatesValidator()
    {
        RuleFor(x => x.ApprovalStatus!.Value).IsInEnum().When(x => x.ApprovalStatus.HasValue);
        RuleFor(x => x.SearchBatchId!.Value).NotEmpty().When(x => x.SearchBatchId.HasValue);
        RuleFor(x => x.InvitationApprovalStatus!.Value).IsInEnum().When(x => x.InvitationApprovalStatus.HasValue);
        RuleFor(x => x.Status!.Value).IsInEnum().When(x => x.Status.HasValue);
    }
}