using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// A user's membership and role in a <see cref="Facility"/> (ADR-0004, ADR-0015).
/// Identified by the <c>(FacilityId, UserId)</c> composite key rather than an id of its own
/// (ADR-0018).
/// </summary>
public sealed class FacilityMember
{
    internal FacilityMember(FacilityId facilityId, UserId userId, FacilityRole role)
    {
        if (facilityId.Value == Guid.Empty)
        {
            throw new ArgumentException("A member's facility id cannot be empty.", nameof(facilityId));
        }

        if (userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A member's user id cannot be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "The facility role is not defined.");
        }

        FacilityId = facilityId;
        UserId = userId;
        Role = role;
    }

    // For EF Core.
    private FacilityMember()
    {
    }

    public FacilityId FacilityId { get; private init; }

    public UserId UserId { get; private init; }

    public FacilityRole Role { get; internal set; }
}
