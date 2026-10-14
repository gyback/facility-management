namespace FacilityManagementService.Application.Persistence;

/// <summary>
/// Read-only access to the persisted aggregates for query handlers, which project straight
/// into their read models (ADR-0014). Implemented in Infrastructure.
/// </summary>
/// <remarks>
/// Exposes one <see cref="IQueryable{T}"/> per aggregate root, added with the feature that
/// introduces the aggregate, e.g. <c>IQueryable&lt;Stay&gt; Stays { get; }</c>. The queries are
/// not tracked and nothing here can write; command handlers use their aggregate's repository.
/// </remarks>
public interface IReadDbContext;
