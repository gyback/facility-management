namespace FacilityManagementService.Domain.Rules;

/// <summary>
/// Thrown when a domain rule refuses a change given the current state, e.g. an exclusive
/// stay that overlaps another. The API answers it with <c>409 Conflict</c> and the rule's
/// <see cref="Code"/> (ADR-0017).
/// </summary>
/// <remarks>
/// Each rule is a named subclass, e.g. <c>ExclusiveStayOverlapsException</c>, passing a
/// <see cref="RuleCodes"/> constant. The message is English for developers and logs: it is
/// never shown to users and must not quote user input.
/// </remarks>
public abstract class DomainRuleException : Exception
{
    /// <param name="code">The rule's code, a <see cref="RuleCodes"/> constant.</param>
    /// <param name="message">An English description for developers, without user input.</param>
    /// <param name="parameters">
    /// The values the frontend needs to describe the refusal, keyed by camelCase name. Values
    /// are typed (dates, numbers, strings), never formatted text.
    /// </param>
    protected DomainRuleException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? parameters = null)
        : base(message)
    {
        Code = code;
        Parameters = parameters ?? new Dictionary<string, object?>();
    }

    /// <summary>The rule's stable, kebab-case code; the frontend's translation key.</summary>
    public string Code { get; }

    /// <summary>The values that describe the refusal; empty when there are none.</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}
