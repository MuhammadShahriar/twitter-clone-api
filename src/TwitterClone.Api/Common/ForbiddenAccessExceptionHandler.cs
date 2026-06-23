using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Common.Exceptions;

namespace TwitterClone.Api.Common;

/// <summary>
/// Translates <see cref="ForbiddenAccessException"/> into an RFC 7807 <c>403 Forbidden</c> response — used
/// when an authenticated caller acts on a resource they don't own (e.g. deleting another user's tweet).
/// </summary>
public class ForbiddenAccessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ForbiddenAccessException forbiddenException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden.",
            Detail = forbiddenException.Message,
        };

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
