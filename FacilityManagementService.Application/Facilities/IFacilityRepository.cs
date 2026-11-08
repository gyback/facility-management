using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Persistence of <see cref="Facility"/> aggregates for command handlers (ADR-0014).
/// Always loads the facility with its members, and never calls <c>SaveChanges</c>.
/// </summary>
public interface IFacilityRepository
{
    Task<Facility?> GetAsync(FacilityId id, CancellationToken cancellationToken = default);

    void Add(Facility facility);
}
