using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Infrastructure.Identity;

/// <summary>
/// Looks up the <see cref="UserId"/> linked to an external identity (ADR-0015). Returns
/// <see langword="null"/> until the <c>User</c> aggregate and its external-identities table are
/// added (#45).
/// </summary>
internal sealed class UserIdentityLookup : IUserIdentityLookup
{
    public Task<UserId?> FindUserIdAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<UserId?>(null);
}

