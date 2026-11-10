using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// Thrown when a user is already a member of the facility they are being added to.
/// </summary>
public sealed class AlreadyFacilityMemberException()
    : DomainRuleException(
        RuleCodes.AlreadyFacilityMember,
        "The user is already a member of this facility.");
