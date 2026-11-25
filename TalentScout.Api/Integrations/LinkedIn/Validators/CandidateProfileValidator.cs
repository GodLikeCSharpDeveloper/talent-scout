using FluentValidation;
using TalentScout.Api.Integrations.LinkedIn.Models;

namespace TalentScout.Api.Integrations.LinkedIn.Validators;

public sealed class CandidateProfileValidator : AbstractValidator<CandidateProfile>
{
    public CandidateProfileValidator()
    {
        RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ProfileUrl).NotEmpty().MaximumLength(2000).Must(value =>
                Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
                string.IsNullOrEmpty(uri.UserInfo) &&
                (uri.Host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.linkedin.com", StringComparison.OrdinalIgnoreCase)) &&
                uri.AbsolutePath.StartsWith("/in/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("ProfileUrl must be an HTTPS LinkedIn /in/ profile without user information.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Experience).NotEmpty().MaximumLength(30000);
    }
}