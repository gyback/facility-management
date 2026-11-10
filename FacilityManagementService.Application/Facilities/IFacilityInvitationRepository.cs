using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Persistence of <see cref="FacilityInvitation"/> aggregates for command handlers (ADR-0014).
/// Never calls <c>SaveChanges</c>.
/// </summary>
public interface IFacilityInvitationRepository
{
    Task<FacilityInvitation?> GetAsync(
        FacilityInvitationId id,
        CancellationToken cancellationToken = default);

    Task<FacilityInvitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    void Add(FacilityInvitation invitation);

    void Remove(FacilityInvitation invitation);
}
