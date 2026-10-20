using FacilityManagementService.Domain.Entities;
using FacilityManagementService.Infrastructure.Persistence.Conventions;
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

    public DbSet<ProbeShelf> Shelves => Set<ProbeShelf>();

    public DbSet<ProbeBook> Books => Set<ProbeBook>();

    // The same id convention as AppDbContext, over the probe's own ids.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.AddEntityIdConventions(typeof(ProbeDbContext).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeEntity>(probe =>
        {
            probe.Property(entity => entity.Name).HasMaxLength(100);
            probe.HasIndex(entity => entity.Name).IsUnique();
        });

        modelBuilder.Entity<ProbeShelf>()
            .HasMany(shelf => shelf.Books)
            .WithOne()
            .HasForeignKey(book => book.ShelfId);

        modelBuilder.Entity<ProbeBook>(book =>
        {
            book.Property(entity => entity.Title).HasMaxLength(100);
            book.HasOne<ProbeBook>().WithMany().HasForeignKey(entity => entity.PreviousEditionId);
        });
    }
}

internal sealed class ProbeEntity
{
    public Guid Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>
/// An aggregate with strongly typed ids (ADR-0018) and a collection of child entities.
/// </summary>
internal sealed class ProbeShelf : AggregateRoot<ProbeShelfId>
{
    private readonly List<ProbeBook> _books = [];

    // Also used by EF Core, which binds the id parameter to the key.
    private ProbeShelf(ProbeShelfId id)
        : base(id)
    {
    }

    public IReadOnlyCollection<ProbeBook> Books => _books;

    public static ProbeShelf Create() => new(ProbeShelfId.New());

    public ProbeBook Add(string title, ProbeBookId? previousEditionId = null)
    {
        var book = new ProbeBook(Id, title, previousEditionId);
        _books.Add(book);
        return book;
    }
}

internal sealed class ProbeBook : Entity<ProbeBookId>
{
    public ProbeBook(ProbeShelfId shelfId, string title, ProbeBookId? previousEditionId)
        : base(ProbeBookId.New())
    {
        ShelfId = shelfId;
        Title = title;
        PreviousEditionId = previousEditionId;
    }

    // For EF Core.
    private ProbeBook()
    {
    }

    public ProbeShelfId ShelfId { get; private init; }

    public string Title { get; private init; } = null!;

    public ProbeBookId? PreviousEditionId { get; private init; }
}

internal readonly record struct ProbeShelfId(Guid Value) : IEntityId<ProbeShelfId>
{
    public static ProbeShelfId New() => new(Guid.CreateVersion7());
    public static ProbeShelfId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

internal readonly record struct ProbeBookId(Guid Value) : IEntityId<ProbeBookId>
{
    public static ProbeBookId New() => new(Guid.CreateVersion7());
    public static ProbeBookId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the probe model; the connection is never opened.
/// </summary>
internal sealed class ProbeDbContextFactory : IDesignTimeDbContextFactory<ProbeDbContext>
{
    public ProbeDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ProbeDbContext>().UseSqlServer("Server=.").Options);
}
