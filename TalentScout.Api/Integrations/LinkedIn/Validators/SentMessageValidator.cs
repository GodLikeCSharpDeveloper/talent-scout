using FluentValidation;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Validators;

public sealed class SentMessageValidator : AbstractValidator<SentMessage>
{
    public SentMessageValidator()
    {
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(300);
    }
}