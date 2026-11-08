using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class RemoveFacilityMemberHandler(
    ICurrentUser currentUser,
    IFacilityRepository facilities) : ICommandHandler<RemoveFacilityMember, Unit>
{
    public async Task<Unit> HandleAsync(
        RemoveFacilityMember command,
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

        facility.RemoveMember(currentUser.Id, command.UserId);

        return Unit.Value;
    }
}
