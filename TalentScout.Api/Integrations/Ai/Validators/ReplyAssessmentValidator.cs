using FluentValidation;
using TalentScout.Api.Integrations.Ai.Models;

namespace TalentScout.Api.Integrations.Ai.Validators;

public sealed class ReplyAssessmentValidator : AbstractValidator<ReplyAssessment>
{
    public ReplyAssessmentValidator()
    {
        RuleFor(x => x.Interest).IsInEnum();
        RuleFor(x => x.Confidence).InclusiveBetween(0, 1);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(10000);
    }
}