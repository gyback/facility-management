using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class RevokeFacilityInvitationHandler(
    ICurrentUser currentUser,
    IFacilityRepository facilities,
    IFacilityInvitationRepository invitations,
    TimeProvider timeProvider) : ICommandHandler<RevokeFacilityInvitation, Unit>
{
    public async Task<Unit> HandleAsync(
        RevokeFacilityInvitation command,
        CancellationToken cancellationToken = default)
    {
        var facility = await facilities.GetAsync(command.FacilityId, cancellationToken);

        if (facility is null || !facility.IsMember(currentUser.Id))
        {
            throw new NotFoundException();
        }

        facility.EnsureAdministrator(currentUser.Id);

        var invitation = await invitations.GetAsync(command.InvitationId, cancellationToken);

        if (invitation is null ||
            invitation.FacilityId != command.FacilityId ||
            !invitation.IsPending(timeProvider.GetUtcNow()))
        {
            throw new NotFoundException();
        }

        invitations.Remove(invitation);

        return Unit.Value;
    }
}
