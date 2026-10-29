using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Infrastructure.Identity;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["Database:ConnectionString"] = "Server=localhost,1433;Database=FacilityManagement",
        ["Database:Authentication"] = "Password",
        ["Database:User"] = "sa",
        ["Database:Password"] = "secret",
    };

    [Fact]
    public void AddInfrastructure_builds_a_valid_container()
    {
        using var provider = BuildProvider(ValidSettings);
        using var scope = provider.CreateScope();

        Assert.IsType<UnitOfWork>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<ReadDbContext>(scope.ServiceProvider.GetRequiredService<IReadDbContext>());
        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
        Assert.Same(currentUser, scope.ServiceProvider.GetRequiredService<ICurrentUser>());
        Assert.IsType<UserIdentityLookup>(scope.ServiceProvider.GetRequiredService<IUserIdentityLookup>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void The_context_connects_with_the_configured_credentials()
    {
        using var provider = BuildProvider(ValidSettings);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connection = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal("localhost,1433", connection.DataSource);
        Assert.Equal("FacilityManagement", connection.InitialCatalog);
        Assert.Equal("sa", connection.UserID);
        Assert.Equal("secret", connection.Password);
    }

    [Fact]
    public void The_context_retries_transient_failures()
    {
        using var provider = BuildProvider(ValidSettings);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.IsType<SqlServerRetryingExecutionStrategy>(context.Database.CreateExecutionStrategy());
    }

    [Fact]
    public void Invalid_database_settings_fail_on_start()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = "Server=localhost;User ID=sa",
        });

        // What the host runs on start, because AddInfrastructure registers ValidateOnStart.
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Equal(typeof(DatabaseOptions), exception.OptionsType);
        Assert.Collection(
            exception.Failures,
            failure => Assert.StartsWith("Database:ConnectionString must not set 'User ID'", failure),
            failure => Assert.StartsWith("Database:Authentication is required", failure));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure()
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
    }
}
