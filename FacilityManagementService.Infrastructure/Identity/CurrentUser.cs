using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Infrastructure.Identity;

/// <summary>
/// Holds the caller's identity and linked <see cref="UserId"/> for the duration of one HTTP
/// request (ADR-0015). Populated by the API's current-user middleware after authentication and
/// read by the fallback authorisation policy and handlers.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private ExternalIdentity? _identity;
    private UserId? _userId;

    /// <summary>
    /// Whether the signed-in identity is linked to a user. Read by the fallback authorisation
    /// policy before any endpoint runs.
    /// </summary>
    public bool IsRegistered => _userId is not null;

    public UserId Id => _userId ?? throw (_identity is not null
        ? new UnregisteredUserException()
        : new InvalidOperationException("There is no signed-in identity for the current request."));

    public ExternalIdentity Identity => _identity
        ?? throw new InvalidOperationException("There is no signed-in identity for the current request.");

    /// <summary>
    /// Records the caller's identity and the <see cref="UserId"/> it resolved to, if any.
    /// </summary>
    public void Set(ExternalIdentity identity, UserId? userId)
    {
        ArgumentNullException.ThrowIfNull(identity);

        _identity = identity;
        _userId = userId;
    }
}

