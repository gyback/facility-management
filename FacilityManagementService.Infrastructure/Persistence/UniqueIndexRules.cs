using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Maps the unique indexes that enforce a domain rule to the rule's code, so that a race
/// caught by the index gives the same <c>409</c> as the handler's own check (ADR-0017).
/// </summary>
/// <remarks>
/// When a feature adds an index that guards a rule, it names the index explicitly with
/// <c>HasDatabaseName(...)</c>, using a constant shared with this map, and adds the pair here,
/// e.g. the exclusive-nights index to <c>RuleCodes.ExclusiveStayOverlaps</c>. An index not
/// listed gives the generic <c>conflict</c> problem.
/// </remarks>
internal static class UniqueIndexRules
{
    /// <summary>Index name to <see cref="RuleCodes"/> constant.</summary>
    internal static readonly IReadOnlyDictionary<string, string> RuleCodeByIndexName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <returns>The code of the rule <paramref name="indexName"/> enforces, or <see langword="null"/>.</returns>
    public static string? FindRuleCode(string indexName) =>
        RuleCodeByIndexName.GetValueOrDefault(indexName);
}
