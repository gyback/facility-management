namespace FacilityManagementService.Domain.Entities;

/// <summary>
/// The strongly typed id of an entity: a <c>readonly record struct</c> wrapping a version 7
/// <see cref="Guid"/>, written by hand next to the entity it identifies (ADR-0018).
/// </summary>
/// <remarks>
/// Infrastructure maps every implementation to a <c>uniqueidentifier</c> column through one
/// convention, so entity configurations never mention an id's conversion. See the Domain README
/// for how to add one.
/// </remarks>
/// <typeparam name="TSelf">The id type itself.</typeparam>
public interface IEntityId<TSelf>
    where TSelf : struct, IEntityId<TSelf>
{
    Guid Value { get; }

    /// <summary>
    /// Wraps an existing id, e.g. from a route value or a database row. Creates nothing; a new
    /// entity gets its id from the id type's <c>New()</c>.
    /// </summary>
    static abstract TSelf From(Guid value);
}
