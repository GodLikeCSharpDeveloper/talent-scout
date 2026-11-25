using FluentValidation;
using TalentScout.Api.Integrations.Ai.Abstractions;
using TalentScout.Api.Integrations.Ai.Models;
using TalentScout.Api.Integrations.Ai.Options;
using TalentScout.Api.Integrations.Ai.Providers.Disabled;
using TalentScout.Api.Integrations.Ai.Validators;

namespace TalentScout.Api.Integrations.Ai;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddAiIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>().Bind(configuration.GetSection("Ai"))
            .Validate(x => x.Provider == "Disabled", "No AI adapter is registered for the configured provider.")
            .Validate(x => x.MinimumInterestConfidence is >= 0 and <= 1, "MinimumInterestConfidence must be 0..1.")
            .ValidateOnStart();
        services.AddSingleton<IRecruitmentAi, DisabledRecruitmentAi>();
        services.AddScoped<IValidator<ProfileAssessment>, ProfileAssessmentValidator>();
        services.AddScoped<IValidator<ReplyAssessment>, ReplyAssessmentValidator>();

        return services;
    }
}