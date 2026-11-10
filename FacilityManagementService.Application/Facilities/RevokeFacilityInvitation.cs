using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

/// <summary>
/// Revokes a pending invitation to a facility (#45). Only an administrator of the facility
/// may do so.
/// </summary>
public sealed record RevokeFacilityInvitation(
    FacilityId FacilityId,
    FacilityInvitationId InvitationId) : ICommand<Unit>;
