using FluentValidation;
using TalentScout.Api.Features.Campaigns.Requests;

namespace TalentScout.Api.Features.Campaigns.Validators;

public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignRequest>
{
    public CreateCampaignValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(20000);
        RuleFor(x => x.Requirements).NotEmpty().MaximumLength(20000);
        RuleFor(x => x.SystemPrompt).NotNull().MaximumLength(20000);
        RuleFor(x => x.SearchQuery).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Recruiter).NotEmpty().MaximumLength(300);
        RuleFor(x => x.MinimumFitScore).InclusiveBetween(0, 1);
        RuleFor(x => x.InterviewUrl).MaximumLength(2000).Must(BeHttpsUrl)
            .WithMessage("InterviewUrl must be an absolute HTTPS URL without user information.");
    }

    private static bool BeHttpsUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo));
    }
}