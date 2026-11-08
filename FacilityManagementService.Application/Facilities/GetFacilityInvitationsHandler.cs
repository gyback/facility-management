using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Application.Facilities;

internal sealed class GetFacilityInvitationsHandler(
    ICurrentUser currentUser,
    IReadDbContext dbContext,
    TimeProvider timeProvider)
    : IQueryHandler<GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>>
{
    public async Task<IReadOnlyList<PendingFacilityInvitation>> HandleAsync(
        GetFacilityInvitations query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        var callerRole = await dbContext.Facilities
            .Where(facility => facility.Id == query.FacilityId)
            .SelectMany(facility => facility.Members)
            .Where(member => member.UserId == userId)
            .Select(member => (FacilityRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException();

        if (callerRole != FacilityRole.Administrator)
        {
            throw new ForbiddenException("Only a facility administrator is allowed to do this.");
        }

        var now = timeProvider.GetUtcNow();

        return await dbContext.FacilityInvitations
            .Where(invitation =>
                invitation.FacilityId == query.FacilityId &&
                invitation.AcceptedAt == null &&
                invitation.ExpiresAt > now)
            .OrderBy(invitation => invitation.CreatedAt)
            .ThenBy(invitation => invitation.Id)
            .Select(invitation => new PendingFacilityInvitation(
                invitation.Id,
                invitation.Role,
                invitation.CreatedAt,
                invitation.ExpiresAt))
            .ToListAsync(cancellationToken);
    }
}
