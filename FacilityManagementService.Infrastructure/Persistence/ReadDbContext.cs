using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Untracked queries over <see cref="AppDbContext"/> for query handlers.
/// </summary>
internal sealed class ReadDbContext(AppDbContext context) : IReadDbContext
{
    public IQueryable<Facility> Facilities => Query<Facility>();

    public IQueryable<FacilityInvitation> FacilityInvitations => Query<FacilityInvitation>();

    public IQueryable<User> Users => Query<User>();

    private IQueryable<T> Query<T>() where T : class => context.Set<T>().AsNoTracking();
}
