using System.Reflection;

namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// The codes of every <see cref="Warning"/> subtype. Add a <c>public const string</c> per kind
/// of warning, together with its entry in the API README's catalogue.
/// </summary>
public static class WarningCodes
{
    /// <summary>Every code declared in this class.</summary>
    public static IReadOnlyCollection<string> All { get; } = typeof(WarningCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
