using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Lists every facility the caller is a member of (ADR-0015).
/// </summary>
public sealed record GetFacilities : IQuery<IReadOnlyList<FacilitySummary>>;

/// <summary>
/// A facility in the caller's list of facilities, including the caller's role in it.
/// </summary>
public sealed record FacilitySummary(
    FacilityId Id,
    string Name,
    int Capacity,
    FacilityRole Role);
