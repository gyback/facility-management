using System.Reflection;

namespace FacilityManagementService.Application.Validation;

/// <summary>
/// The shared codes of <see cref="ValidationError"/>s, reused across fields so the frontend
/// translates a handful of codes rather than one per field (ADR-0017). Add a code, with a
/// factory on <see cref="ValidationError"/> and an entry in the API README's catalogue, only
/// when no existing code fits.
/// </summary>
public static class ValidationCodes
{
    public const string Required = "required";
    public const string TooLong = "too-long";
    public const string OutOfRange = "out-of-range";
    public const string MustBeAfter = "must-be-after";

    /// <summary>Every code declared in this class.</summary>
    public static IReadOnlyCollection<string> All { get; } = typeof(ValidationCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
