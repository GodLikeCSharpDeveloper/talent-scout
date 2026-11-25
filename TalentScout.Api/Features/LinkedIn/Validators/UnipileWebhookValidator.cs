using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;

namespace TalentScout.Api.Features.LinkedIn.Validators;

public sealed class UnipileWebhookValidator : AbstractValidator<UnipileWebhookRequest>
{
    public UnipileWebhookValidator()
    {
        RuleSet(
            "Relation",
            () =>
            {
                RuleFor(x => x.AccountId).NotEmpty().MaximumLength(100);
                RuleFor(x => x.UserProviderId).NotEmpty().MaximumLength(150);
            });
        RuleFor(x => x.AccountId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ChatId).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ProviderChatId).MaximumLength(1000);
        RuleFor(x => x.MessageId).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Sender).NotNull();
        RuleFor(x => x.Sender!.ProviderId).NotEmpty().MaximumLength(300).When(x => x.Sender is not null);
        RuleFor(x => x.Timestamp).NotEmpty().LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5));
        RuleFor(x => x.Message).MaximumLength(10000);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Message) || x.Attachments is { Count: > 0 })
            .WithMessage("A message must contain text or attachments.");
    }
}