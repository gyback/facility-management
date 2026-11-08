using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Users;

/// <summary>
/// Reads the caller's sign-in state and profile (ADR-0015). May be called by an authenticated
/// identity that is not yet linked to a <see cref="User"/>.
/// </summary>
public sealed record GetCurrentUser : IQuery<CurrentUserProfile>;

/// <summary>
/// The caller's current sign-in and registration state.
/// </summary>
/// <param name="IsRegistered">Whether the caller's external identity is linked to a user.</param>
/// <param name="UserId">The linked user's id, or <see langword="null"/> when not registered.</param>
/// <param name="DisplayName">
/// The linked user's display name, or, when not registered, a non-email display name from the
/// token if one was provided; otherwise <see langword="null"/>.
/// </param>
/// <param name="CanCreateFacilities">Whether the user is allowed to create facilities.</param>
public sealed record CurrentUserProfile(
    bool IsRegistered,
    UserId? UserId,
    string? DisplayName,
    bool CanCreateFacilities);
