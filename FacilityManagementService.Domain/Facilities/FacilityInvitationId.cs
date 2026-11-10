using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// The identifier of a <see cref="FacilityInvitation"/> (ADR-0018).
/// </summary>
public readonly record struct FacilityInvitationId(Guid Value) : IEntityId<FacilityInvitationId>
{
    public static FacilityInvitationId New() => new(Guid.CreateVersion7());
    public static FacilityInvitationId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
