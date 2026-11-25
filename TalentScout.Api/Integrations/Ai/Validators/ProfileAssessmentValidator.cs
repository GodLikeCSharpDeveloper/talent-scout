using FluentValidation;
using TalentScout.Api.Integrations.Ai.Models;

namespace TalentScout.Api.Integrations.Ai.Validators;

public sealed class ProfileAssessmentValidator : AbstractValidator<ProfileAssessment>
{
    public ProfileAssessmentValidator()
    {
        RuleFor(x => x.Score).InclusiveBetween(0, 1);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(10000);
    }
}