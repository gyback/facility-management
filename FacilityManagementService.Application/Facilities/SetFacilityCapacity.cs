using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Sets a facility's sleeping capacity in people (ADR-0005). Only an administrator of the
/// facility may do so.
/// </summary>
public sealed record SetFacilityCapacity(FacilityId FacilityId, int Capacity) : ICommand<Unit>;
