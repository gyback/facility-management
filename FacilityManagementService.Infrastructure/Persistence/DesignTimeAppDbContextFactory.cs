using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations</c> build the model without starting the API or configuring a
/// database. Commands that connect (e.g. <c>database update</c>) take <c>--connection</c>.
/// </summary>
internal sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer().Options);
}
