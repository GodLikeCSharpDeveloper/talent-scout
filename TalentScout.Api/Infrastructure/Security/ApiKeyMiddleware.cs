using System.Security.Cryptography;
using System.Text;
using TalentScout.Api.Features.Realtime;
using TalentScout.Api.Integrations.LinkedIn.Options;

namespace TalentScout.Api.Infrastructure.Security;

public sealed class SecurityOptions
{
    public string ApiKey { get; set; } = "";
}

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<SecurityOptions> options, IOptions<LinkedInOptions> linkedIn, RealtimeTickets tickets)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);

            return;
        }

        if (HttpMethods.IsPost(context.Request.Method) &&
            (context.Request.Path.Value ?? "").TrimEnd('/').Equals("/api/webhooks/unipile", StringComparison.OrdinalIgnoreCase))
        {
            var settings = linkedIn.Value;
            var secret = context.Request.Headers["X-Unipile-Secret"].ToString();
            if (!settings.IsConfigured || settings.WebhookSecret.Length < 32 ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secret)), SHA256.HashData(Encoding.UTF8.GetBytes(settings.WebhookSecret))))
            {
                await Results.Problem(statusCode: 401, title: "A valid webhook secret is required.").ExecuteAsync(context);

                return;
            }

            await next(context);

            return;
        }

        var supplied = context.Request.Headers["X-Api-Key"].ToString();
        if (context.Request.Path.StartsWithSegments("/hubs/workspace"))
        {
            if (tickets.Validate(context.Request.Query["access_token"].ToString()))
            {
                await next(context);

                return;
            }

            await Results.Problem(statusCode: 401, title: "A valid realtime ticket is required.").ExecuteAsync(context);

            return;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.ApiKey));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
        {
            await Results.Problem(statusCode: 401, title: "A valid X-Api-Key header is required.").ExecuteAsync(context);

            return;
        }

        await next(context);
    }
}