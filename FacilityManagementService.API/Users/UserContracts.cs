using FacilityManagementService.Application.Users;

namespace FacilityManagementService.API.Users;

internal sealed record UpdateCurrentUserRequest(string DisplayName)
{
    public UpdateCurrentUser ToCommand() => new(DisplayName);
}

internal sealed record CurrentUserResponse(
    bool IsRegistered,
    Guid? UserId,
    string? DisplayName,
    bool CanCreateFacilities)
{
    public static CurrentUserResponse From(CurrentUserProfile profile) => new(
        profile.IsRegistered,
        profile.UserId?.Value,
        profile.DisplayName,
        profile.CanCreateFacilities);
}
