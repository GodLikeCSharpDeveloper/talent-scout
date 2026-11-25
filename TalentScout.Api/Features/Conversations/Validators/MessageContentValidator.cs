using FluentValidation;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Features.Conversations.Validators;

public sealed class MessageContentValidator : AbstractValidator<Message>
{
    public MessageContentValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(10000);
    }
}