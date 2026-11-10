using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// The identifier of a <see cref="Facility"/> (ADR-0018).
/// </summary>
public readonly record struct FacilityId(Guid Value) : IEntityId<FacilityId>
{
    public static FacilityId New() => new(Guid.CreateVersion7());
    public static FacilityId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
