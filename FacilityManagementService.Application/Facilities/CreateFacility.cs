using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Creates a new facility and makes the caller its initial administrator (ADR-0015).
/// </summary>
public sealed record CreateFacility(string Name, int Capacity) : ICommand<FacilityId>;
