using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Users;

/// <summary>
/// The caller making the current request (ADR-0015). Resolved once per request before the
/// endpoint runs, so reading <see cref="Id"/> never writes.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// The user making the request.
    /// </summary>
    /// <exception cref="UnregisteredUserException">
    /// The signed-in identity is not linked to a user.
    /// </exception>
    UserId Id { get; }

    /// <summary>
    /// The provider identity behind the request. Used only to link it.
    /// </summary>
    ExternalIdentity Identity { get; }
}
