using FacilityManagementService.Application.Messaging;

namespace FacilityManagementService.Application.Users;

internal sealed class UpdateCurrentUserHandler(ICurrentUser currentUser, IUserRepository users)
    : ICommandHandler<UpdateCurrentUser, Unit>
{
    public async Task<Unit> HandleAsync(
        UpdateCurrentUser command,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(currentUser.Id, cancellationToken)
            ?? throw new NotFoundException();

        user.Rename(command.DisplayName);

        return Unit.Value;
    }
}
