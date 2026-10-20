using FacilityManagementService.Domain.Entities;
using FacilityManagementService.Infrastructure.Persistence;
using FacilityManagementService.Infrastructure.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FacilityManagementService.Infrastructure.Tests.Persistence;

/// <summary>
/// Checks the strongly typed id convention (ADR-0018) on a model built without a connection.
/// </summary>
public sealed class EntityIdConventionTests
{
    // A [Fact] over the whole model rather than a [Theory] per property: xUnit v3 fails a theory
    // with no data, and AppDbContext has no entities until the first feature adds one.
    [Fact]
    public void Every_id_in_the_application_model_is_converted_and_never_generated()
    {
        using var context = new DesignTimeAppDbContextFactory().CreateDbContext([]);

        Assert.Empty(Problems(DesignTimeModel(context)));
    }

    [Fact]
    public void Every_id_in_a_model_using_the_convention_is_converted_and_never_generated()
    {
        using var context = new LibraryContext();

        Assert.Empty(Problems(DesignTimeModel(context)));
    }

    [Theory]
    [InlineData(typeof(Shelf), nameof(Shelf.Id))]
    [InlineData(typeof(Book), nameof(Book.Id))]
    [InlineData(typeof(Book), nameof(Book.ShelfId))]
    [InlineData(typeof(Book), nameof(Book.PreviousEditionId))]
    public void An_id_property_is_stored_as_a_uniqueidentifier(Type entity, string propertyName)
    {
        using var context = new LibraryContext();

        var property = DesignTimeModel(context).FindEntityType(entity)!.FindProperty(propertyName)!;

        Assert.Equal("uniqueidentifier", property.GetColumnType());
    }

    [Fact]
    public void A_nullable_id_is_converted_and_stays_nullable()
    {
        using var context = new LibraryContext();

        var property = DesignTimeModel(context).FindEntityType(typeof(Book))!.FindProperty(nameof(Book.PreviousEditionId))!;

        Assert.IsType<EntityIdConverter<BookId>>(property.GetValueConverter());
        Assert.True(property.IsNullable);
    }

    [Fact]
    public void An_id_key_is_never_generated()
    {
        using var context = new LibraryContext();

        var key = DesignTimeModel(context).FindEntityType(typeof(Shelf))!.FindPrimaryKey()!.Properties.Single();

        Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
    }

    [Fact]
    public void The_converter_round_trips_an_id()
    {
        var converter = new EntityIdConverter<BookId>();
        var id = BookId.New();

        Assert.Equal(id.Value, converter.ConvertToProvider(id));
        Assert.Equal(id, converter.ConvertFromProvider(id.Value));
    }

    // The design-time model keeps every annotation, such as column types, that the runtime
    // model may drop.
    private static IModel DesignTimeModel(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static List<string> Problems(IModel model)
    {
        var problems = new List<string>();
        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var property in entityType.GetDeclaredProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!EntityIdConventions.IsEntityId(type))
                {
                    continue;
                }

                var name = $"{entityType.ShortName()}.{property.Name}";
                if (property.GetValueConverter()?.GetType() != typeof(EntityIdConverter<>).MakeGenericType(type))
                {
                    problems.Add($"{name} does not use {nameof(EntityIdConverter<>)}");
                }

                if (property.IsPrimaryKey() && property.ValueGenerated != ValueGenerated.Never)
                {
                    problems.Add($"{name} is a key generated {property.ValueGenerated}");
                }
            }
        }

        return problems;
    }

    private sealed class LibraryContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlServer();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            configurationBuilder.AddEntityIdConventions(typeof(LibraryContext).Assembly);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Shelf>().HasMany(shelf => shelf.Books).WithOne().HasForeignKey(book => book.ShelfId);
            modelBuilder.Entity<Book>().HasOne<Book>().WithMany().HasForeignKey(book => book.PreviousEditionId);
        }
    }

    private sealed class Shelf : AggregateRoot<ShelfId>
    {
        private Shelf()
        {
        }

        public List<Book> Books { get; } = [];
    }

    private sealed class Book : Entity<BookId>
    {
        private Book()
        {
        }

        public ShelfId ShelfId { get; private init; }

        public BookId? PreviousEditionId { get; private init; }
    }
}

internal readonly record struct ShelfId(Guid Value) : IEntityId<ShelfId>
{
    public static ShelfId New() => new(Guid.CreateVersion7());
    public static ShelfId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

internal readonly record struct BookId(Guid Value) : IEntityId<BookId>
{
    public static BookId New() => new(Guid.CreateVersion7());
    public static BookId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
