using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FacilityManagementService.Application.Messaging;

namespace FacilityManagementService.Application.Persistence;

/// <summary>
/// Serialises and deserialises command handler results (<c>TResult</c>) stored in
/// <c>CommandMarkers.ResultJson</c> for <c>Idempotency-Key</c> replay (ADR-0017, ADR-0019).
/// </summary>
public static class CommandResultSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<TResult>(TResult result) =>
        JsonSerializer.Serialize(result, Options);

    public static TResult Deserialize<TResult>(string json) =>
        JsonSerializer.Deserialize<TResult>(json, Options)
        ?? throw new InvalidOperationException(
            $"Stored command result JSON deserialised to null for {typeof(TResult).Name}.");

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(ConfigureWarningPolymorphism);

        return new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
        };
    }

    private static void ConfigureWarningPolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Warning))
        {
            return;
        }

        var derivedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Warning)))
            .ToArray();

        if (derivedTypes.Length == 0)
        {
            return;
        }

        var polymorphism = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$type",
        };

        foreach (var derivedType in derivedTypes)
        {
            polymorphism.DerivedTypes.Add(
                new JsonDerivedType(derivedType, derivedType.FullName ?? derivedType.Name));
        }

        typeInfo.PolymorphismOptions = polymorphism;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
