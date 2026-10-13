using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FacilityManagementService.IntegrationTests.Probe;

/// <summary>
/// A minimal model with its own migration, used to check the test harness itself: that
/// migrations are applied and that the database enforces what the production model will
/// rely on. Feature tests use the application's <c>DbContext</c> instead.
/// </summary>
internal sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
{
    public DbSet<ProbeEntity> Probes => Set<ProbeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ProbeEntity>(probe =>
        {
            probe.Property(entity => entity.Name).HasMaxLength(100);
            probe.HasIndex(entity => entity.Name).IsUnique();
        });
}

internal sealed class ProbeEntity
{
    public Guid Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the probe model; the connection is never opened.
/// </summary>
internal sealed class ProbeDbContextFactory : IDesignTimeDbContextFactory<ProbeDbContext>
{
    public ProbeDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ProbeDbContext>().UseSqlServer("Server=.").Options);
}
