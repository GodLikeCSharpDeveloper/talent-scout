using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TalentScout.Api.Features.LinkedIn;
using Xunit;

namespace TalentScout.Api.Tests;

public sealed class LinkedInRoutesTests
{
    [Fact]
    public async Task Manual_webhook_registration_route_is_not_exposed()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(LinkedInIntegrationController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PutAsync("/api/integrations/linkedin/webhook", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}