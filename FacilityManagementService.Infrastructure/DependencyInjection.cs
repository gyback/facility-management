using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database, <see cref="IUnitOfWork"/>, <see cref="IReadDbContext"/> and
    /// <see cref="TimeProvider.System"/> as the application clock.
    /// </summary>
    /// <remarks>
    /// The connection is configured from the <c>Database</c> section, which is validated when the
    /// host starts; see the Infrastructure README.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>());

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseSqlServer(
                provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.BuildConnectionString(),
                sqlServer => sqlServer.EnableRetryOnFailure()));

        // Scoped like the AppDbContext they share, so a command's repositories and its unit of
        // work see the same tracked changes.
        services.TryAddScoped<IUnitOfWork, UnitOfWork>();
        services.TryAddScoped<IReadDbContext, ReadDbContext>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
