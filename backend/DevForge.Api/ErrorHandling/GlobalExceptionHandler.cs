using System.Text.Json;
using DevForge.Application.Common;
using DevForge.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevForge.Api.ErrorHandling;

/// <summary>
/// Translates exceptions into RFC 9457 problem details. Known application exceptions map to 4xx responses
/// with their message; anything else becomes an opaque 500 so internals never reach the client.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ConflictException or InvalidStateTransitionException =>
                Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DomainValidationException validation => ValidationProblem(validation),
            BadHttpRequestException badRequest => Problem(badRequest.StatusCode, "Bad request", badRequest.Message),
            _ => Problem(
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred. Please try again."),
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                "Request {Method} {Path} rejected with {StatusCode}: {Reason}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                problem.Status,
                exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ValidationProblemDetails ValidationProblem(DomainValidationException exception)
    {
        var field = JsonNamingPolicy.CamelCase.ConvertName(exception.Field);

        return new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [exception.Message] })
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
        };
    }
}
