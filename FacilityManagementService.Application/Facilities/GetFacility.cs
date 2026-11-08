using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Reads a facility and its members, scoped to facilities where the caller is a member
/// (ADR-0015).
/// </summary>
public sealed record GetFacility(FacilityId FacilityId) : IQuery<FacilityDetails>;

/// <summary>
/// A facility's details and its members.
/// </summary>
public sealed record FacilityDetails(
    FacilityId Id,
    string Name,
    int Capacity,
    IReadOnlyList<FacilityMemberDetails> Members);

/// <summary>
/// A member of a facility, with their display name and role.
/// </summary>
public sealed record FacilityMemberDetails(
    UserId UserId,
    string DisplayName,
    FacilityRole Role);
