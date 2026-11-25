using System.Security.Cryptography;
using System.Text;

namespace TalentScout.Api.Infrastructure.Security;

public sealed class SecurityOptions
{
    public string ApiKey { get; set; } = "";
}

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<SecurityOptions> options)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);

            return;
        }

        var supplied = context.Request.Headers["X-Api-Key"].ToString();
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