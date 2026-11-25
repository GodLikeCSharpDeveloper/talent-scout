using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;

namespace TalentScout.Api.Features.Campaigns.Validators;

public sealed class SetCampaignAiSettingsValidator : AbstractValidator<SetCampaignAiSettingsRequest>
{
    public SetCampaignAiSettingsValidator()
    {
        RuleFor(x => x.SystemPrompt).NotNull().MaximumLength(20000);
    }
}