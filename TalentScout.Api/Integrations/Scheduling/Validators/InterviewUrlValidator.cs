using FluentValidation;

namespace TalentScout.Api.Integrations.Scheduling.Validators;

public sealed class InterviewUrlValidator : AbstractValidator<string>
{
    public InterviewUrlValidator()
    {
        RuleFor(x => x).NotEmpty().MaximumLength(2000).Must(value =>
                Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo))
            .WithMessage("InterviewUrl must be an absolute HTTPS URL without user information.");
    }
}