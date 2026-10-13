using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// The EF Core model of the whole database. Only <see cref="UnitOfWork"/> calls
/// <c>SaveChanges</c>; repositories stage changes in this context and
/// <see cref="ReadDbContext"/> exposes untracked queries over it (ADR-0014).
/// </summary>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Applies every <see cref="IEntityTypeConfiguration{TEntity}"/> in this assembly, so an
    /// entity is mapped by adding its configuration class next to its repository.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
