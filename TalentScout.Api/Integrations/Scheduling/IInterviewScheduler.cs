using FluentValidation;
using TalentScout.Data.Entities;

namespace TalentScout.Api.Integrations.Scheduling;

public sealed class SchedulingOptions
{
    public string DefaultInterviewUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string WebhookSigningKey { get; set; } = "";
}

public interface IInterviewScheduler
{
    Task<string> GetBookingUrlAsync(Campaign campaign, Enrollment enrollment, CancellationToken ct);
}

/// <summary>
///     A configured Calendly or other booking URL. Native webhook verification belongs in the future provider
///     adapter.
/// </summary>
public sealed class ConfiguredInterviewScheduler(IOptions<SchedulingOptions> options, IValidator<string> validator) : IInterviewScheduler
{
    public async Task<string> GetBookingUrlAsync(Campaign campaign, Enrollment enrollment, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(campaign.InterviewUrl) ? options.Value.DefaultInterviewUrl : campaign.InterviewUrl;

        if (!(await validator.ValidateAsync(url, ct)).IsValid)
            throw new ApiException(503, "An interview booking URL is not configured.");

        return new Uri(url.Trim()).AbsoluteUri;
    }
}