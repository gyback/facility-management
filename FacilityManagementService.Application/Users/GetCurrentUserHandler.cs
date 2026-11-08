using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Application.Users;

internal sealed class GetCurrentUserHandler(ICurrentUser currentUser, IReadDbContext dbContext)
    : IQueryHandler<GetCurrentUser, CurrentUserProfile>
{
    public async Task<CurrentUserProfile> HandleAsync(
        GetCurrentUser query,
        CancellationToken cancellationToken = default)
    {
        var identity = currentUser.Identity;

        var registered = await dbContext.Users
            .Where(user => user.ExternalIdentities.Any(linked =>
                linked.Provider == identity.Provider && linked.Subject == identity.Subject))
            .Select(user => new CurrentUserProfile(
                true,
                user.Id,
                user.DisplayName,
                user.CanCreateFacilities))
            .FirstOrDefaultAsync(cancellationToken);

        return registered ?? new CurrentUserProfile(
            false,
            null,
            User.TryGetNonEmailDisplayName(identity.DisplayName),
            false);
    }
}
