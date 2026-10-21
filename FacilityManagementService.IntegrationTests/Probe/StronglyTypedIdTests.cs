using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.IntegrationTests.Probe;

/// <summary>
/// Checks the strongly typed id convention (ADR-0018) against SQL Server, through the probe
/// model's shelves and books.
/// </summary>
public sealed class StronglyTypedIdTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TestDatabase<ProbeDbContext> _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await sqlServer.CreateDatabaseAsync<ProbeDbContext>(
            options => new(options),
            TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task An_entity_is_saved_and_reloaded_by_its_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var shelf = ProbeShelf.Create();
        var book = shelf.Add("Logbook");
        await SaveAsync(shelf, cancellationToken);

        await using var assert = _database.CreateContext();
        var reloaded = await assert.Books.SingleAsync(entity => entity.Id == book.Id, cancellationToken);

        Assert.Equal(book.Id, reloaded.Id);
        Assert.Equal(shelf.Id, reloaded.ShelfId);
        Assert.Equal("Logbook", reloaded.Title);
    }

    [Fact]
    public async Task A_query_by_a_list_of_ids_is_translated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var shelf = ProbeShelf.Create();
        var first = shelf.Add("First");
        var second = shelf.Add("Second");
        shelf.Add("Third");
        await SaveAsync(shelf, cancellationToken);

        await using var assert = _database.CreateContext();
        var ids = new[] { first.Id, second.Id };
        var titles = await assert.Books
            .Where(book => ids.Contains(book.Id))
            .OrderBy(book => book.Title)
            .Select(book => book.Title)
            .ToListAsync(cancellationToken);

        Assert.Equal(["First", "Second"], titles);
    }

    [Fact]
    public async Task A_nullable_id_round_trips_null_and_a_value()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var shelf = ProbeShelf.Create();
        var firstEdition = shelf.Add("Handbook");
        var secondEdition = shelf.Add("Handbook, 2nd edition", firstEdition.Id);
        await SaveAsync(shelf, cancellationToken);

        await using var assert = _database.CreateContext();

        Assert.Null((await assert.Books.SingleAsync(book => book.Id == firstEdition.Id, cancellationToken)).PreviousEditionId);
        Assert.Equal(
            firstEdition.Id,
            (await assert.Books.SingleAsync(book => book.Id == secondEdition.Id, cancellationToken)).PreviousEditionId);
        Assert.Equal(
            secondEdition.Id,
            (await assert.Books.SingleAsync(book => book.PreviousEditionId == firstEdition.Id, cancellationToken)).Id);
    }

    // If EF Core believed the key were generated, a child that already has its id would be
    // taken for an existing row and updated, and SaveChanges would fail (ADR-0018).
    [Fact]
    public async Task A_new_child_added_to_a_tracked_parent_is_inserted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var shelf = ProbeShelf.Create();
        await SaveAsync(shelf, cancellationToken);

        ProbeBook book;
        await using (var act = _database.CreateContext())
        {
            var tracked = await act.Shelves.Include(entity => entity.Books)
                .SingleAsync(entity => entity.Id == shelf.Id, cancellationToken);
            book = tracked.Add("Guestbook");

            act.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Added, act.Entry(book).State);
            await act.SaveChangesAsync(cancellationToken);
        }

        await using var assert = _database.CreateContext();
        Assert.True(await assert.Books.AnyAsync(entity => entity.Id == book.Id && entity.ShelfId == shelf.Id, cancellationToken));
    }

    private async Task SaveAsync(ProbeShelf shelf, CancellationToken cancellationToken)
    {
        await using var context = _database.CreateContext();
        context.Shelves.Add(shelf);
        await context.SaveChangesAsync(cancellationToken);
    }
}
