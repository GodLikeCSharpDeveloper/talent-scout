using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TalentScout.Data.Persistence;
using TalentScout.Data.Repositories;

namespace TalentScout.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connection))
            connection = (configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions()).ToConnectionString();
        services.AddDbContext<AppDbContext>((provider, options) => options.UseNpgsql(connection).AddInterceptors(provider.GetServices<ISaveChangesInterceptor>()));
        services.AddScoped<ISearchBatchRepository, SearchBatchRepository>();
        services.AddScoped<ILinkedInConversationRepository, LinkedInConversationRepository>();
        services.AddScoped<ILinkedInWebhookRepository, LinkedInWebhookRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<ILocationCacheRepository, LocationCacheRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        return services;
    }

    public static async Task MigrateDataAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
    }
}