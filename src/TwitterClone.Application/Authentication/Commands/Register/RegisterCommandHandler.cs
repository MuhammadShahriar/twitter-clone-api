using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Authentication.Commands.Register;

public class RegisterCommandHandler(IIdentityService identityService)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var handle = request.Handle.Trim();
        var displayName = request.DisplayName.Trim();

        var result = await identityService.CreateUserAsync(
            email, handle, displayName, request.Password, cancellationToken);

        if (!result.Succeeded)
        {
            // Surface Identity's reasons (duplicate email/handle, weak password) as a 400 through the
            // existing ValidationException → ValidationExceptionHandler path.
            var failures = result.Errors.Select(e => new ValidationFailure(nameof(request.Email), e));
            throw new ValidationException(failures);
        }

        return new RegisterResult(result.UserId, email, handle, displayName);
    }
}
