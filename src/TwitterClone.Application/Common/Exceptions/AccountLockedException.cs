namespace TwitterClone.Application.Common.Exceptions;

/// <summary>
/// Thrown when a login is rejected because the account is temporarily locked after too many failed
/// attempts. The API maps this to <c>423 Locked</c>. The message is deliberately generic.
/// </summary>
public class AccountLockedException : Exception
{
    public AccountLockedException()
        : base("Account temporarily locked due to too many failed login attempts. Please try again later.")
    {
    }
}
