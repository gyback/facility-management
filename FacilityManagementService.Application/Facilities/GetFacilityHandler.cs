using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Application.Facilities;

internal sealed class GetFacilityHandler(
    ICurrentUser currentUser,
    IReadDbContext dbContext) : IQueryHandler<GetFacility, FacilityDetails>
{
    public async Task<FacilityDetails> HandleAsync(
        GetFacility query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        var facility = await dbContext.Facilities
            .Where(existing =>
                existing.Id == query.FacilityId &&
                existing.Members.Any(member => member.UserId == userId))
            .Select(existing => new
            {
                existing.Id,
                existing.Name,
                existing.Capacity,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException();

        var members = await dbContext.Facilities
            .Where(existing => existing.Id == query.FacilityId)
            .SelectMany(existing => existing.Members)
            .Join(
                dbContext.Users,
                member => member.UserId,
                user => user.Id,
                (member, user) => new
                {
                    member.UserId,
                    user.DisplayName,
                    member.Role,
                })
            .OrderBy(member => member.DisplayName)
            .ThenBy(member => member.UserId)
            .Select(member => new FacilityMemberDetails(
                member.UserId,
                member.DisplayName,
                member.Role))
            .ToListAsync(cancellationToken);

        return new FacilityDetails(facility.Id, facility.Name, facility.Capacity, members);
    }
}
