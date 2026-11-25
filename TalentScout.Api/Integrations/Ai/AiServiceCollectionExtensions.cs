using System.ClientModel;
using FluentValidation;
using OpenAI;
using OpenAI.Chat;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.Ai.Options;
using TalentScout.Api.Integrations.Ai.Providers.Disabled;
using TalentScout.Api.Integrations.Ai.Providers.OpenAi;
using TalentScout.Api.Integrations.Ai.Validators;

namespace TalentScout.Api.Integrations.Ai;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAiIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>().Bind(configuration.GetSection("Ai"))
            .Validate(x => string.IsNullOrWhiteSpace(x.ApiKey) || !string.IsNullOrWhiteSpace(x.Model), "Ai:Model is required when Ai:ApiKey is configured.")
            .Validate(x => x.MinimumInterestConfidence is >= 0 and <= 1, "MinimumInterestConfidence must be 0..1.")
            .ValidateOnStart();
        if (string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]))
            services.AddScoped<IRecruitmentAi, DisabledRecruitmentAi>();
        else
        {
            services.AddSingleton<ChatClient>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AiOptions>>().Value;

                return new ChatClient(
                    options.Model,
                    new ApiKeyCredential(options.ApiKey),
                    new OpenAIClientOptions { NetworkTimeout = TimeSpan.FromSeconds(60) });
            });
            services.AddScoped<OpenAiCompletionClient>();
            services.AddScoped<IRecruitmentAi, OpenAiRecruitmentAi>();
        }

        services.AddScoped<IValidator<ProfileAssessment>, ProfileAssessmentValidator>();
        services.AddScoped<IValidator<ReplyAssessment>, ReplyAssessmentValidator>();

        return services;
    }
}