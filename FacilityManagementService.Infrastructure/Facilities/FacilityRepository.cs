using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Facilities;

internal sealed class FacilityRepository(AppDbContext context) : IFacilityRepository
{
    public Task<Facility?> GetAsync(FacilityId id, CancellationToken cancellationToken = default) =>
        context.Set<Facility>()
            .Include(facility => facility.Members)
            .FirstOrDefaultAsync(facility => facility.Id == id, cancellationToken);

    public void Add(Facility facility) => context.Set<Facility>().Add(facility);
}
