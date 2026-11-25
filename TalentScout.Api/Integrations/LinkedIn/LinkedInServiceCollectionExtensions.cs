using FluentValidation;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Disabled;
using TalentScout.Api.Integrations.LinkedIn.Validators;

namespace TalentScout.Api.Integrations.LinkedIn;

public static class LinkedInServiceCollectionExtensions
{
    public static IServiceCollection AddLinkedInIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LinkedInOptions>().Bind(configuration.GetSection("LinkedIn"))
            .Validate(x => x.Provider == "Disabled", "No LinkedIn adapter is registered for the configured provider.")
            .ValidateOnStart();
        services.AddSingleton<ILinkedInGateway, DisabledLinkedInGateway>();
        services.AddScoped<IValidator<CandidateProfile>, CandidateProfileValidator>();
        services.AddScoped<IValidator<SentMessage>, SentMessageValidator>();

        return services;
    }
}