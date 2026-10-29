namespace FacilityManagementService.Application.Persistence;

/// <summary>
/// Thrown by <see cref="IUnitOfWork"/> when a unique index rejects a command's changes,
/// typically because a concurrent request passed the same check first. The API answers it
/// with <c>409 Conflict</c> (ADR-0017).
/// </summary>
/// <param name="ruleCode">
/// The code of the domain rule the index enforces, so a race gives the same answer as the
/// handler's own check; <see langword="null"/> when the index guards no known rule.
/// </param>
/// <param name="indexName">The index or constraint that rejected the change.</param>
public sealed class ConflictException(string? ruleCode, string indexName, Exception innerException)
    : Exception($"The unique index {indexName} rejected the change.", innerException)
{
    public string? RuleCode { get; } = ruleCode;

    public string IndexName { get; } = indexName;
}
