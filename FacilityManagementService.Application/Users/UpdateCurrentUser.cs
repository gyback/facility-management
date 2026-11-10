using FacilityManagementService.Application.Messaging;

namespace FacilityManagementService.Application.Users;

/// <summary>
/// Updates the signed-in user's display name (ADR-0015).
/// </summary>
public sealed record UpdateCurrentUser(string DisplayName) : ICommand<Unit>;
