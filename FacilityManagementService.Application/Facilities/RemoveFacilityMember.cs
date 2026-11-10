using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Removes a member from a facility. Only an administrator of the facility may do so, and at
/// least one administrator must remain.
/// </summary>
public sealed record RemoveFacilityMember(
    FacilityId FacilityId,
    UserId UserId) : ICommand<Unit>;
