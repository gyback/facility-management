using System.Reflection;

namespace FacilityManagementService.Domain.Rules;

/// <summary>
/// The codes of the domain rules a <see cref="DomainRuleException"/> can carry. Add a
/// <c>public const string</c> per rule, together with its entry in the catalogue in the API
/// README; a test checks that the two match.
/// </summary>
public static class RuleCodes
{
    /// <summary>Every code declared in this class.</summary>
    public static IReadOnlyCollection<string> All { get; } = typeof(RuleCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();
}
