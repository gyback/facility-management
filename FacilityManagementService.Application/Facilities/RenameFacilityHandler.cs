using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class RenameFacilityHandler(
    ICurrentUser currentUser,
    IFacilityRepository facilities) : ICommandHandler<RenameFacility, Unit>
{
    public async Task<Unit> HandleAsync(
        RenameFacility command,
        CancellationToken cancellationToken = default)
    {
        var facility = await facilities.GetAsync(command.FacilityId, cancellationToken);

        if (facility is null || !facility.IsMember(currentUser.Id))
        {
            throw new NotFoundException();
        }

        facility.Rename(currentUser.Id, command.Name);

        return Unit.Value;
    }
}
