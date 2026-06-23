using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Common.Exceptions;

namespace TwitterClone.Api.Common;

/// <summary>
/// Translates <see cref="AccountLockedException"/> (thrown by the login handler when an account is
/// locked after too many failed attempts) into an RFC 7807 <c>423 Locked</c> response. The detail is
/// generic so it doesn't leak more than "this account is currently locked".
/// </summary>
public class AccountLockedExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AccountLockedException lockedException)
        {
            return false;
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status423Locked,
            Title = "Account locked.",
            Detail = lockedException.Message,
        };

        httpContext.Response.StatusCode = StatusCodes.Status423Locked;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
