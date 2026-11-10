using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// Thrown when removing a member or changing their role would leave a facility without an
/// administrator.
/// </summary>
public sealed class LastFacilityAdministratorException()
    : DomainRuleException(
        RuleCodes.LastFacilityAdministrator,
        "The last administrator of a facility cannot be removed or given a non-administrator role.");
