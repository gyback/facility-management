using System.Reflection;
using FacilityManagementService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence.Conventions;

internal static class EntityIdConventions
{
    /// <summary>
    /// Maps every strongly typed id declared in <paramref name="assemblies"/>, keys and foreign
    /// keys alike, to a <see cref="Guid"/> column with <see cref="EntityIdConverter{TId}"/>, and
    /// marks id keys as never generated with <see cref="EntityIdKeyConvention"/> (ADR-0018).
    /// Entity configurations therefore never mention an id's conversion.
    /// </summary>
    public static ModelConfigurationBuilder AddEntityIdConventions(
        this ModelConfigurationBuilder configurationBuilder,
        params Assembly[] assemblies)
    {
        var idTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsEntityId);

        foreach (var idType in idTypes)
        {
            configurationBuilder
                .Properties(idType)
                .HaveConversion(typeof(EntityIdConverter<>).MakeGenericType(idType));
        }

        configurationBuilder.Conventions.Add(_ => new EntityIdKeyConvention());

        return configurationBuilder;
    }

    /// <summary>
    /// Whether <paramref name="type"/> is a strongly typed id, i.e. implements
    /// <see cref="IEntityId{TSelf}"/> of itself.
    /// </summary>
    public static bool IsEntityId(Type type) =>
        type is { IsValueType: true, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(contract =>
            contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IEntityId<>)
            && contract.GenericTypeArguments[0] == type);
}
