using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Recognises a save rejected by a unique index or constraint, and finds which one. The SQL
/// Server error numbers stay here, in Infrastructure (ADR-0007).
/// </summary>
internal static partial class UniqueIndexViolation
{
    /// <summary>Cannot insert a duplicate key in an object with a unique constraint, including a primary key.</summary>
    private const int UniqueConstraintViolation = 2627;

    /// <summary>Cannot insert a duplicate key row in an object with a unique index.</summary>
    private const int UniqueIndexDuplicate = 2601;

    /// <summary>The name used when the message cannot be parsed, e.g. because it is localised.</summary>
    internal const string UnknownIndexName = "unknown";

    /// <returns>
    /// <see langword="true"/> when <paramref name="exception"/> was caused by SQL Server error 2627
    /// or 2601, with the index or constraint in <paramref name="indexName"/>.
    /// </returns>
    public static bool TryGetIndexName(DbUpdateException exception, [NotNullWhen(true)] out string? indexName)
    {
        if (exception.InnerException is SqlException { Number: UniqueConstraintViolation or UniqueIndexDuplicate } sqlException)
        {
            indexName = ParseIndexName(sqlException.Message) ?? UnknownIndexName;
            return true;
        }

        indexName = null;
        return false;
    }

    /// <summary>
    /// Finds the index or constraint name in the message of error 2601 (<c>... with unique index
    /// 'IX_...'</c>) or 2627 (<c>Violation of UNIQUE KEY constraint 'UQ_...'</c>, or of a
    /// PRIMARY KEY constraint).
    /// </summary>
    /// <returns>The name, or <see langword="null"/> when the message has neither form.</returns>
    internal static string? ParseIndexName(string message) =>
        IndexNamePattern().Match(message) is { Success: true } match ? match.Groups["name"].Value : null;

    [GeneratedRegex("(?:unique index|constraint) '(?<name>[^']+)'")]
    private static partial Regex IndexNamePattern();
}
