using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Issues a single-use invitation to join a facility with <paramref name="Role"/> (ADR-0015).
/// Only an administrator of the facility may do so.
/// </summary>
public sealed record CreateFacilityInvitation(
    FacilityId FacilityId,
    FacilityRole Role) : ICommand<CreatedFacilityInvitation>;

/// <summary>
/// The issued invitation's id, its raw one-time bearer token (returned once and never stored),
/// and when it expires.
/// </summary>
public sealed record CreatedFacilityInvitation(
    FacilityInvitationId Id,
    string Token,
    DateTimeOffset ExpiresAt);
