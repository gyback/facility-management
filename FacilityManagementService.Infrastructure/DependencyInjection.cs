using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Infrastructure.Facilities;
using FacilityManagementService.Infrastructure.Identity;
using FacilityManagementService.Infrastructure.Persistence;
using FacilityManagementService.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database, <see cref="IUnitOfWork"/>, <see cref="IReadDbContext"/>,
    /// the aggregate repositories, <see cref="ICurrentUser"/>, <see cref="IUserIdentityLookup"/>
    /// and <see cref="TimeProvider.System"/> as the application clock.
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
        services.TryAddScoped<IUserRepository, UserRepository>();
        services.TryAddScoped<IFacilityRepository, FacilityRepository>();
        services.TryAddScoped<IFacilityInvitationRepository, FacilityInvitationRepository>();

        // Both resolve to the same scoped instance: the API populates CurrentUser and handlers
        // read ICurrentUser.
        services.TryAddScoped<CurrentUser>();
        services.TryAddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.TryAddScoped<IUserIdentityLookup, UserIdentityLookup>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
