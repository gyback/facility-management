using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Lists pending invitations for a facility (#45). Only an administrator of the facility may
/// read them.
/// </summary>
public sealed record GetFacilityInvitations(
    FacilityId FacilityId) : IQuery<IReadOnlyList<PendingFacilityInvitation>>;

/// <summary>
/// A pending facility invitation. Never includes the secret token or its hash.
/// </summary>
public sealed record PendingFacilityInvitation(
    FacilityInvitationId Id,
    FacilityRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
