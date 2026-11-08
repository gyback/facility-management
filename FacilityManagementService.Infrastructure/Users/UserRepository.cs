using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Users;

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<User?> GetAsync(UserId id, CancellationToken cancellationToken = default) =>
        context.Set<User>()
            .Include(user => user.ExternalIdentities)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindByExternalIdentityAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default) =>
        context.Set<User>()
            .Include(user => user.ExternalIdentities)
            .FirstOrDefaultAsync(
                user => user.ExternalIdentities.Any(identity =>
                    identity.Provider == provider && identity.Subject == subject),
                cancellationToken);

    public void Add(User user) => context.Set<User>().Add(user);
}
