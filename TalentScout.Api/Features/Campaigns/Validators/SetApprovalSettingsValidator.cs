using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;

namespace TalentScout.Api.Features.Campaigns.Validators;

public sealed class SetApprovalSettingsValidator : AbstractValidator<SetApprovalSettingsRequest>
{
    public SetApprovalSettingsValidator()
    {
        RuleFor(x => x).Must(x => x.AutoApproveCandidates.HasValue || x.AutoApproveInterviewInvitations.HasValue)
            .WithMessage("Specify at least one approval setting.");
    }
}