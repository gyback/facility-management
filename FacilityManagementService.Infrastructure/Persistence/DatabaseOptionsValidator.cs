using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Checks <see cref="DatabaseOptions"/> at startup, so a misconfigured environment fails to start
/// instead of failing on its first request.
/// </summary>
internal sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    /// <summary>
    /// Connection string keywords that sign in. They are configured through
    /// <see cref="DatabaseOptions.Authentication"/> instead, so it alone decides how to sign in.
    /// </summary>
    private static readonly string[] CredentialKeywords =
        ["User ID", "Password", "Authentication", "Integrated Security"];

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var failures = new List<string>();
        const string section = DatabaseOptions.SectionName;

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"{section}:ConnectionString is required.");
        }
        else
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(options.ConnectionString);
                failures.AddRange(CredentialKeywords
                    .Where(keyword => builder.ShouldSerialize(keyword))
                    .Select(keyword =>
                        $"{section}:ConnectionString must not set '{keyword}'; " +
                        $"set {section}:Authentication and its settings instead."));
            }
            catch (ArgumentException exception)
            {
                failures.Add($"{section}:ConnectionString is not a valid SQL Server connection string: {exception.Message}");
            }
        }

        switch (options.Authentication)
        {
            case null:
                failures.Add(
                    $"{section}:Authentication is required; one of: {string.Join(", ", Enum.GetNames<DatabaseAuthentication>())}.");
                break;
            case DatabaseAuthentication.Password:
                if (string.IsNullOrWhiteSpace(options.User))
                {
                    failures.Add($"{section}:User is required for Password authentication.");
                }

                if (string.IsNullOrEmpty(options.Password))
                {
                    failures.Add($"{section}:Password is required for Password authentication.");
                }

                break;
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
