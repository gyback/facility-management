using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Renames a facility. Only an administrator of the facility may do so.
/// </summary>
public sealed record RenameFacility(FacilityId FacilityId, string Name) : ICommand<Unit>;
