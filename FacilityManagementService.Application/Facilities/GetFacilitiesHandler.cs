using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Application.Facilities;

internal sealed class GetFacilitiesHandler(
    ICurrentUser currentUser,
    IReadDbContext dbContext) : IQueryHandler<GetFacilities, IReadOnlyList<FacilitySummary>>
{
    public async Task<IReadOnlyList<FacilitySummary>> HandleAsync(
        GetFacilities query,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;

        return await dbContext.Facilities
            .Where(facility => facility.Members.Any(member => member.UserId == userId))
            .OrderBy(facility => facility.Name)
            .ThenBy(facility => facility.Id)
            .Select(facility => new FacilitySummary(
                facility.Id,
                facility.Name,
                facility.Capacity,
                facility.Members.First(member => member.UserId == userId).Role))
            .ToListAsync(cancellationToken);
    }
}
