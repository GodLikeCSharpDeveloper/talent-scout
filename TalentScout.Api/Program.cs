using TalentScout.Api.Features.Realtime;
using TalentScout.Api.Infrastructure;
using TalentScout.Api.Infrastructure.Security;
using TalentScout.Data;

namespace TalentScout.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddApplication(builder.Configuration);

        var app = builder.Build();
        await app.Services.MigrateDataAsync();

        app.UseExceptionHandler();
        app.UseMiddleware<ApiKeyMiddleware>();
        app.MapHealthChecks("/health");
        app.MapOpenApi();
        app.MapControllers();
        app.MapHub<WorkspaceHub>("/hubs/workspace");
        await app.RunAsync();
    }
}