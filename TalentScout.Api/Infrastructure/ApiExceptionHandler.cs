using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TalentScout.Data.Exceptions;

namespace TalentScout.Api.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validation)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            return await problems.TryWriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ValidationProblemDetails(
                        validation.Errors.GroupBy(x => x.PropertyName)
                            .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()))
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Extensions = { ["traceId"] = context.TraceIdentifier }
                    }
                });
        }

        var status = exception switch
        {
            ApiException api => api.Status,
            PersistenceConflictException => 409,
            BadHttpRequestException bad => bad.StatusCode,
            _ => 500
        };
        if (status == 500)
            logger.LogError(exception, "Unhandled API error {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;

        return await problems.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = status switch { 400 => "Invalid request", 404 => "Not found", 409 => "Conflict", 503 => "Provider unavailable", _ => "Request failed" },
                    Detail = exception is ApiException ? exception.Message : status == 409 ? "Concurrent change or duplicate record. Reload the resource before retrying." : "The request could not be completed.",
                    Extensions = { ["traceId"] = context.TraceIdentifier }
                }
            });
    }
}