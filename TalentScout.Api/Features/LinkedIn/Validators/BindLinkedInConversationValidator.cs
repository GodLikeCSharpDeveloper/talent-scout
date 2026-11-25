using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;

namespace TalentScout.Api.Features.LinkedIn.Validators;

public sealed class BindLinkedInConversationValidator : AbstractValidator<BindLinkedInConversationRequest>
{
    public BindLinkedInConversationValidator()
    {
        RuleFor(x => x.ChatId).NotEmpty().MaximumLength(300);
    }
}