using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Facilities;

internal sealed class FacilityInvitationRepository(AppDbContext context) : IFacilityInvitationRepository
{
    public Task<FacilityInvitation?> GetAsync(
        FacilityInvitationId id,
        CancellationToken cancellationToken = default) =>
        context.Set<FacilityInvitation>()
            .FirstOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);

    public Task<FacilityInvitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        context.Set<FacilityInvitation>()
            .FirstOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);

    public void Add(FacilityInvitation invitation) => context.Set<FacilityInvitation>().Add(invitation);

    public void Remove(FacilityInvitation invitation) => context.Set<FacilityInvitation>().Remove(invitation);
}
