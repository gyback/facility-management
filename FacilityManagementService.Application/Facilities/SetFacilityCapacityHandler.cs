using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class SetFacilityCapacityHandler(
    ICurrentUser currentUser,
    IFacilityRepository facilities) : ICommandHandler<SetFacilityCapacity, Unit>
{
    public async Task<Unit> HandleAsync(
        SetFacilityCapacity command,
        CancellationToken cancellationToken = default)
    {
        var facility = await facilities.GetAsync(command.FacilityId, cancellationToken);

        if (facility is null || !facility.IsMember(currentUser.Id))
        {
            throw new NotFoundException();
        }

        facility.SetCapacity(currentUser.Id, command.Capacity);

        return Unit.Value;
    }
}
