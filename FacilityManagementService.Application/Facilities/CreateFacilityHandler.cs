using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Application.Facilities;

internal sealed class CreateFacilityHandler(
    ICurrentUser currentUser,
    IUserRepository users,
    IFacilityRepository facilities) : ICommandHandler<CreateFacility, FacilityId>
{
    public async Task<FacilityId> HandleAsync(
        CreateFacility command,
        CancellationToken cancellationToken = default)
    {
        var creator = await users.GetAsync(currentUser.Id, cancellationToken)
            ?? throw new ForbiddenException();

        var facility = Facility.Create(creator, command.Name, command.Capacity);
        facilities.Add(facility);

        return facility.Id;
    }
}
