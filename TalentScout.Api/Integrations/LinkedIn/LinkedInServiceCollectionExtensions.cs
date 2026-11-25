using FluentValidation;
using TalentScout.Api.Integrations.LinkedIn.Abstractions;
using TalentScout.Api.Integrations.LinkedIn.Models;
using TalentScout.Api.Integrations.LinkedIn.Options;
using TalentScout.Api.Integrations.LinkedIn.Providers.Disabled;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Adapters;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Api;
using TalentScout.Api.Integrations.LinkedIn.Providers.Unipile.Http;
using TalentScout.Api.Integrations.LinkedIn.Validators;

namespace TalentScout.Api.Integrations.LinkedIn;

public static class LinkedInServiceCollectionExtensions
{
    public static IServiceCollection AddLinkedInIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LinkedInOptions>().Bind(configuration.GetSection("LinkedIn"))
            .Validate(x => x.Provider is "Disabled" or "Unipile", "LinkedIn:Provider must be Disabled or Unipile.")
            .Validate(
                x => !x.IsConfigured || (Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) &&
                                         uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" &&
                                         string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)),
                "LinkedIn:BaseUrl must be the HTTPS Unipile DSN origin, without an API path.")
            .Validate(
                x => !x.IsConfigured || (!string.IsNullOrWhiteSpace(x.AccountId) && x.WebhookSecret.Length >= 32),
                "Configure LinkedIn:AccountId and a WebhookSecret of at least 32 characters.")
            .ValidateOnStart();
        services.AddDataProtection();
        services.AddHttpClient<IUnipileHttpClient, UnipileHttpClient>((provider, http) =>
        {
            var settings = provider.GetRequiredService<IOptions<LinkedInOptions>>().Value;
            if (settings.IsConfigured)
            {
                http.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/api/v1/");
                http.DefaultRequestHeaders.Add("X-API-KEY", settings.ApiKey);
            }

            http.Timeout = TimeSpan.FromSeconds(30);
            http.MaxResponseContentBufferSize = 4 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IUnipileSearchApi, UnipileSearchApi>();
        services.AddScoped<IUnipileProfilesApi, UnipileProfilesApi>();
        services.AddScoped<IUnipileMessagingApi, UnipileMessagingApi>();
        services.AddScoped<IUnipileAdministrationApi, UnipileAdministrationApi>();
        services.AddScoped<ILinkedInAdministration, UnipileAdministrationAdapter>();
        services.AddScoped<DisabledLinkedInGateway>();
        var configured = configuration["LinkedIn:Provider"] == "Unipile" && !string.IsNullOrWhiteSpace(configuration["LinkedIn:ApiKey"]);
        if (configured)
        {
            services.AddScoped<ILinkedInGateway, UnipileLinkedInGateway>();
            services.AddScoped<ILinkedInProfiles, UnipileProfilesAdapter>();
            services.AddScoped<ILinkedInContacts, UnipileContactsAdapter>();
            services.AddScoped<ILinkedInConversations, UnipileConversationsAdapter>();
            services.AddScoped<ILinkedInSearchParameters, UnipileSearchParametersAdapter>();
        }
        else
        {
            services.AddScoped<ILinkedInGateway>(provider => provider.GetRequiredService<DisabledLinkedInGateway>());
            services.AddScoped<ILinkedInProfiles>(provider => provider.GetRequiredService<DisabledLinkedInGateway>());
            services.AddScoped<ILinkedInContacts>(provider => provider.GetRequiredService<DisabledLinkedInGateway>());
            services.AddScoped<ILinkedInConversations>(provider => provider.GetRequiredService<DisabledLinkedInGateway>());
            services.AddScoped<ILinkedInSearchParameters>(provider => provider.GetRequiredService<DisabledLinkedInGateway>());
        }

        services.AddScoped<IValidator<CandidateProfile>, CandidateProfileValidator>();
        services.AddScoped<IValidator<SentMessage>, SentMessageValidator>();

        return services;
    }
}