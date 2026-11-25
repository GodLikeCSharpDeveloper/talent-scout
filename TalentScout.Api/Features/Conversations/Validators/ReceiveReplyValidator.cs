using FluentValidation;
using TalentScout.Api.Features.Conversations.Requests;

namespace TalentScout.Api.Features.Conversations.Validators;

public sealed class ReceiveReplyValidator : AbstractValidator<ReceiveReplyRequest>
{
    public ReceiveReplyValidator()
    {
        RuleFor(x => x.ExternalMessageId).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(10000);
    }
}