using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Tests.Persistence;

public sealed class MigrationTests
{
    [Fact]
    public void The_model_has_no_changes_missing_a_migration()
    {
        // The same factory dotnet ef uses, so this compares the model with the latest snapshot
        // without a database.
        using var context = new DesignTimeAppDbContextFactory().CreateDbContext([]);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The model differs from the migration snapshot; add a migration (see the Infrastructure README).");
    }
}
