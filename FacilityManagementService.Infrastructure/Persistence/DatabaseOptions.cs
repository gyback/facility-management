using Microsoft.Data.SqlClient;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// How the API connects to the database, bound from the <c>Database</c> configuration section
/// (e.g. <c>Database__ConnectionString</c> in the environment). The connection string names the
/// server and database only; <see cref="Authentication"/> selects how to sign in (ADR-0007,
/// constraint 5).
/// </summary>
internal sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>The server, database and transport settings, without credentials.</summary>
    public string? ConnectionString { get; set; }

    public DatabaseAuthentication? Authentication { get; set; }

    /// <summary>The SQL login, for <see cref="DatabaseAuthentication.Password"/>.</summary>
    public string? User { get; set; }

    /// <summary>The SQL login's password, for <see cref="DatabaseAuthentication.Password"/>.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// <see cref="ConnectionString"/> with the credentials for <see cref="Authentication"/> added.
    /// Call only on options that passed <see cref="DatabaseOptionsValidator"/>.
    /// </summary>
    public string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);

        switch (Authentication)
        {
            case DatabaseAuthentication.Password:
                builder.UserID = User;
                builder.Password = Password;
                break;
            default:
                throw new InvalidOperationException($"Unsupported database authentication '{Authentication}'.");
        }

        return builder.ConnectionString;
    }
}

/// <summary>How the API signs in to the database.</summary>
internal enum DatabaseAuthentication
{
    /// <summary>A SQL login and password, used on the homelab and in local development.</summary>
    Password,
}
