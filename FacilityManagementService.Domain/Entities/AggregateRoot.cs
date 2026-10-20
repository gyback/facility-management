namespace FacilityManagementService.Domain.Entities;

/// <summary>
/// The root of an aggregate: what a repository loads and saves as a whole, and what
/// <c>IReadDbContext</c> exposes to queries (ADR-0014). Deliberately empty for now; domain
/// events would go here once #27 is decided.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : struct, IEntityId<TId>
{
    /// <inheritdoc cref="Entity{TId}(TId)"/>
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <inheritdoc cref="Entity{TId}()"/>
    protected AggregateRoot()
    {
    }
}
