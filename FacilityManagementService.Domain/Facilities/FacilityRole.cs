namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// A member's role in a <see cref="Facility"/> (ADR-0004, ADR-0015).
/// </summary>
public enum FacilityRole
{
    /// <summary>A family member who can use the facility and see its data.</summary>
    Member,

    /// <summary>A co-owner of the facility who participates in upkeep cost sharing.</summary>
    Owner,

    /// <summary>May manage the facility, its members, invitations, and moderate comments.</summary>
    Administrator,
}
