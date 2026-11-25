using FluentValidation;
using TalentScout.Api.Features.LinkedIn.Requests;

namespace TalentScout.Api.Features.LinkedIn.Validators;

public sealed class RegisterLinkedInWebhooksValidator : AbstractValidator<RegisterLinkedInWebhooksRequest>
{
    public RegisterLinkedInWebhooksValidator()
    {
        RuleFor(x => x.Url).NotEmpty().Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                                                     uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
                                                     string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath.TrimEnd('/') == "/api/webhooks/unipile")
            .WithMessage("Set LinkedIn:WebhookUrl to the public HTTPS /api/webhooks/unipile endpoint.");
    }
}