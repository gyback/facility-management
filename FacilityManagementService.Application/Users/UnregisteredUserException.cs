namespace FacilityManagementService.Application.Users;

/// <summary>
/// Thrown when <see cref="ICurrentUser.Id"/> is read for a signed-in identity that is not
/// linked to a user (ADR-0015). The fallback authorisation policy normally rejects such
/// callers first; this is the backstop for code that runs outside the policy. The API
/// answers it with <c>403 user-not-registered</c> (ADR-0017).
/// </summary>
public sealed class UnregisteredUserException(string message = "The signed-in identity is not linked to a user.")
    : Exception(message);
