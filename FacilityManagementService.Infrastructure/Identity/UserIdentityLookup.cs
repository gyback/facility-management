using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Identity;

/// <summary>
/// Looks up the <see cref="UserId"/> linked to an external identity (ADR-0015).
/// </summary>
internal sealed class UserIdentityLookup(AppDbContext context) : IUserIdentityLookup
{
    public async Task<UserId?> FindUserIdAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default) =>
        await context.Set<UserExternalIdentity>()
            .AsNoTracking()
            .Where(identity => identity.Provider == provider && identity.Subject == subject)
            .Select(identity => (UserId?)identity.UserId)
            .FirstOrDefaultAsync(cancellationToken);
}
