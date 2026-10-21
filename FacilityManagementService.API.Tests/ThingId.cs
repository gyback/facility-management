using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.API.Tests;

/// <summary>A strongly typed id (ADR-0018) for the test endpoints' made-up resource.</summary>
internal readonly record struct ThingId(Guid Value) : IEntityId<ThingId>
{
    public static ThingId New() => new(Guid.CreateVersion7());
    public static ThingId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
