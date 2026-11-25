using Ardalis.Result;
using TalentScout.Api.Features.LinkedIn.Responses;
using TalentScout.Api.Integrations.LinkedIn.Options;

namespace TalentScout.Api.Features.LinkedIn.Handlers;

public sealed class GetLinkedInIntegrationStatusHandler(IOptions<LinkedInOptions> options)
{
    public Result<LinkedInIntegrationStatusResponse> Handle()
    {
        return Result<LinkedInIntegrationStatusResponse>.Success(
            new LinkedInIntegrationStatusResponse(
                options.Value.IsConfigured,
                options.Value.Provider,
                "v1",
                options.Value.IsConfigured ? options.Value.AccountId : null,
                options.Value.UseInMail,
                !string.IsNullOrWhiteSpace(options.Value.WebhookUrl)));
    }
}