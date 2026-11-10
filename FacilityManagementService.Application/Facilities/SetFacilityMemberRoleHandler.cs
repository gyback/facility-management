using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class SetFacilityMemberRoleHandler(
    ICurrentUser currentUser,
    IFacilityRepository facilities) : ICommandHandler<SetFacilityMemberRole, Unit>
{
    public async Task<Unit> HandleAsync(
        SetFacilityMemberRole command,
        CancellationToken cancellationToken = default)
    {
        var facility = await facilities.GetAsync(command.FacilityId, cancellationToken);

        if (facility is null || !facility.IsMember(currentUser.Id))
        {
            throw new NotFoundException();
        }

        facility.EnsureAdministrator(currentUser.Id);

        if (!facility.IsMember(command.UserId))
        {
            throw new NotFoundException();
        }

        facility.SetMemberRole(currentUser.Id, command.UserId, command.Role);

        return Unit.Value;
    }
}
