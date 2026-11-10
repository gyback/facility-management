using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class CreateFacilityInvitationHandler(
    ICurrentUser currentUser,
    IFacilityRepository facilities,
    IFacilityInvitationRepository invitations,
    TimeProvider timeProvider) : ICommandHandler<CreateFacilityInvitation, CreatedFacilityInvitation>
{
    public async Task<CreatedFacilityInvitation> HandleAsync(
        CreateFacilityInvitation command,
        CancellationToken cancellationToken = default)
    {
        var facility = await facilities.GetAsync(command.FacilityId, cancellationToken);

        if (facility is null || !facility.IsMember(currentUser.Id))
        {
            throw new NotFoundException();
        }

        var (invitation, token) = facility.CreateInvitation(
            currentUser.Id,
            command.Role,
            timeProvider.GetUtcNow());

        invitations.Add(invitation);

        return new CreatedFacilityInvitation(invitation.Id, token, invitation.ExpiresAt);
    }
}
