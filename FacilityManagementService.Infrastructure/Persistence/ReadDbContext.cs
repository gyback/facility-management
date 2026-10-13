using FacilityManagementService.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Untracked queries over <see cref="AppDbContext"/> for query handlers.
/// </summary>
internal sealed class ReadDbContext(AppDbContext context) : IReadDbContext
{
    // Expose each aggregate root through Query, e.g. public IQueryable<Stay> Stays => Query<Stay>();

    // ReSharper disable once UnusedMember.Local
    private IQueryable<T> Query<T>() where T : class => context.Set<T>().AsNoTracking();
}
