using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Sets the role of a member of a facility. Only an administrator of the facility may do so,
/// and at least one administrator must remain.
/// </summary>
public sealed record SetFacilityMemberRole(
    FacilityId FacilityId,
    UserId UserId,
    FacilityRole Role) : ICommand<Unit>;
