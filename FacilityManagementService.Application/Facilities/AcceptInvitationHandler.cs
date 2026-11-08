using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class AcceptInvitationHandler(
    ICurrentUser currentUser,
    IFacilityInvitationRepository invitations,
    IFacilityRepository facilities,
    IUserRepository users,
    TimeProvider timeProvider) : ICommandHandler<AcceptInvitation, AcceptedInvitation>
{
    public async Task<AcceptedInvitation> HandleAsync(
        AcceptInvitation command,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = FacilityInvitation.HashToken(command.Token);
        var invitation = await invitations.FindByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundException();

        var facility = await facilities.GetAsync(invitation.FacilityId, cancellationToken)
            ?? throw new NotFoundException();

        var identity = currentUser.Identity;
        var user = await users.FindByExternalIdentityAsync(
            identity.Provider,
            identity.Subject,
            cancellationToken);

        if (user is null)
        {
            var displayName = !string.IsNullOrWhiteSpace(command.DisplayName)
                ? command.DisplayName.Trim()
                : User.TryGetNonEmailDisplayName(identity.DisplayName);

            if (displayName is null)
            {
                throw new ValidationException(
                    [ValidationError.Required(nameof(AcceptInvitation.DisplayName))]);
            }

            user = User.Register(displayName, identity.Provider, identity.Subject);
            users.Add(user);
        }

        invitation.Accept(facility, user.Id, timeProvider.GetUtcNow());

        return new AcceptedInvitation(facility.Id, user.Id, invitation.Role);
    }
}
