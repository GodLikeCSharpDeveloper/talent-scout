using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TalentScout.Api.Infrastructure.Http;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info.Title = "TalentScout API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            Name = "X-Api-Key",
            In = ParameterLocation.Header
        };
        document.Components.SecuritySchemes["UnipileWebhook"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey, Name = "X-Unipile-Secret", In = ParameterLocation.Header
        };
        if (document.Paths.TryGetValue("/api/webhooks/unipile", out var webhookPath) && webhookPath.Operations is not null)
        {
            foreach (var operation in webhookPath.Operations.Values)
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("UnipileWebhook", document)] = [] }];
        }

        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
            }
        ];

        return Task.CompletedTask;
    }
}