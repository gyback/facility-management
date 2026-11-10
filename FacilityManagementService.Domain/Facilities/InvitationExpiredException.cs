using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// Thrown when an invitation has expired or has already been used.
/// </summary>
public sealed class InvitationExpiredException()
    : DomainRuleException(
        RuleCodes.InvitationExpired,
        "The invitation has expired or has already been used.");
