using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Accepts a single-use facility invitation (ADR-0015). If the caller's external identity is
/// not yet linked to a <see cref="User"/>, registers the user and links the identity.
/// </summary>
public sealed record AcceptInvitation(
    string Token,
    string? DisplayName = null) : ICommand<AcceptedInvitation>;

/// <summary>
/// The outcome of accepting an invitation: the facility joined, the caller's user id, and
/// the role granted.
/// </summary>
public sealed record AcceptedInvitation(
    FacilityId FacilityId,
    UserId UserId,
    FacilityRole Role);
