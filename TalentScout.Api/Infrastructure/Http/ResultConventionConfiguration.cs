using System.Net;
using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using IResult = Ardalis.Result.IResult;

namespace TalentScout.Api.Infrastructure.Http;

public static class ResultConventionConfiguration
{
    public static void Configure(ResultStatusMap map)
    {
        map.AddDefaultMap();
        var failures = new (ResultStatus Status, HttpStatusCode HttpStatus)[]
        {
            (ResultStatus.Invalid, HttpStatusCode.BadRequest),
            (ResultStatus.Unauthorized, HttpStatusCode.Unauthorized),
            (ResultStatus.Forbidden, HttpStatusCode.Forbidden),
            (ResultStatus.NotFound, HttpStatusCode.NotFound),
            (ResultStatus.Conflict, HttpStatusCode.Conflict),
            (ResultStatus.Error, HttpStatusCode.BadGateway),
            (ResultStatus.CriticalError, HttpStatusCode.InternalServerError),
            (ResultStatus.Unavailable, HttpStatusCode.ServiceUnavailable)
        };
        foreach (var (status, httpStatus) in failures)
        {
            map.For(
                status,
                httpStatus,
                options => options.With((controller, result) =>
                    CreateProblem(controller, result, (int)httpStatus)));
        }
    }

    private static ProblemDetails CreateProblem(ControllerBase controller, IResult result, int status)
    {
        var factory = controller.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        if (result.Status == ResultStatus.Invalid)
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in result.ValidationErrors)
                modelState.AddModelError(error.Identifier ?? string.Empty, error.ErrorMessage);

            return factory.CreateValidationProblemDetails(controller.HttpContext, modelState, status);
        }

        return factory.CreateProblemDetails(
            controller.HttpContext,
            status,
            result.Status.ToString(),
            detail: string.Join(" ", result.Errors));
    }
}