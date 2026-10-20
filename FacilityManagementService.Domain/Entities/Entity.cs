namespace FacilityManagementService.Domain.Entities;

/// <summary>
/// Something with an identity of its own: an aggregate root, or an entity that a route or
/// another table refers to. Rows identified by their values (such as ADR-0005's exclusive
/// nights) have a composite key instead and do not derive from this.
/// </summary>
/// <remarks>
/// Equality is reference equality; code that asks whether two entities are the same compares
/// their ids (<c>a.Id == b.Id</c>), per ADR-0018.
/// </remarks>
public abstract class Entity<TId>
    where TId : struct, IEntityId<TId>
{
    /// <param name="id">
    /// The entity's id, from the id type's <c>New()</c> when the entity is created. Cannot be
    /// empty, which also rules out <c>default</c>.
    /// </param>
    protected Entity(TId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException($"An entity's id cannot be empty ({typeof(TId).Name}).", nameof(id));
        }

        Id = id;
    }

    /// <summary>
    /// For EF Core, which sets <see cref="Id"/> when it materialises the entity.
    /// </summary>
    protected Entity()
    {
    }

    public TId Id { get; private init; }
}
