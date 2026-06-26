using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Common.Exceptions;

namespace TwitterClone.Api.Common;

/// <summary>
/// Translates <see cref="EditWindowExpiredException"/> into an RFC 7807 <c>409 Conflict</c> response — used
/// when the author tries to edit a tweet after the edit window has closed. It's a Conflict (the tweet's age
/// conflicts with the edit), not a 403, because the caller is the legitimate author.
/// </summary>
public class EditWindowExpiredExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not EditWindowExpiredException expiredException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Edit window expired.",
            Detail = expiredException.Message,
        };

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
