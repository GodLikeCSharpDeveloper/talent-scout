using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;

namespace TalentScout.Api.Features.LinkedIn.Validators;

public sealed class SendConnectionValidator : AbstractValidator<SendConnectionRequest>
{
    public SendConnectionValidator()
    {
        RuleFor(x => x.Note).MaximumLength(300);
    }
}