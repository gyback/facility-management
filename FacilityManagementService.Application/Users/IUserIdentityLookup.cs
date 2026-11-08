using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Users;

/// <summary>
/// Resolves an <see cref="ExternalIdentity"/> to the <see cref="UserId"/> it is linked to
/// (ADR-0015). Called once per authenticated request by the API's current-user middleware.
/// </summary>
public interface IUserIdentityLookup
{
    /// <summary>
    /// Returns the <see cref="UserId"/> linked to <paramref name="provider"/> and
    /// <paramref name="subject"/>, or <see langword="null"/> if the identity is not linked.
    /// </summary>
    Task<UserId?> FindUserIdAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default);
}
