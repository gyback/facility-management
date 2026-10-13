using FacilityManagementService.Infrastructure.Persistence;

namespace FacilityManagementService.Infrastructure.Tests.Persistence;

public sealed class DatabaseOptionsValidatorTests
{
    private readonly DatabaseOptionsValidator _validator = new();

    [Fact]
    public void Password_authentication_with_a_user_and_password_is_valid()
    {
        var result = _validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_connection_string_is_required(string? connectionString)
    {
        var options = ValidOptions();
        options.ConnectionString = connectionString;

        var result = _validator.Validate(null, options);

        Assert.Equal(["Database:ConnectionString is required."], result.Failures);
    }

    [Fact]
    public void A_malformed_connection_string_is_rejected()
    {
        var options = ValidOptions();
        options.ConnectionString = "Server=localhost;Not a keyword";

        var result = _validator.Validate(null, options);

        var failure = Assert.Single(result.Failures!);
        Assert.StartsWith("Database:ConnectionString is not a valid SQL Server connection string: ", failure);
    }

    [Theory]
    [InlineData("User ID=sa", "User ID")]
    [InlineData("UID=sa", "User ID")]
    [InlineData("Password=secret", "Password")]
    [InlineData("PWD=secret", "Password")]
    [InlineData("Authentication=ActiveDirectoryDefault", "Authentication")]
    [InlineData("Integrated Security=true", "Integrated Security")]
    [InlineData("Trusted_Connection=false", "Integrated Security")]
    public void Credentials_in_the_connection_string_are_rejected(string credential, string keyword)
    {
        var options = ValidOptions();
        options.ConnectionString = $"Server=localhost;Database=FacilityManagement;{credential}";

        var result = _validator.Validate(null, options);

        Assert.Equal(
            [
                $"Database:ConnectionString must not set '{keyword}'; " +
                "set Database:Authentication and its settings instead.",
            ],
            result.Failures);
    }

    [Fact]
    public void Every_credential_in_the_connection_string_is_reported()
    {
        var options = ValidOptions();
        options.ConnectionString = "Server=localhost;User ID=sa;Password=secret";

        var result = _validator.Validate(null, options);

        Assert.Equal(2, result.Failures!.Count());
    }

    [Fact]
    public void Authentication_is_required()
    {
        var options = ValidOptions();
        options.Authentication = null;

        var result = _validator.Validate(null, options);

        Assert.Equal(["Database:Authentication is required; one of: Password."], result.Failures);
    }

    [Fact]
    public void An_undefined_authentication_is_rejected()
    {
        var options = ValidOptions();
        options.Authentication = (DatabaseAuthentication)1;

        var result = _validator.Validate(null, options);

        Assert.Equal(["Database:Authentication '1' is not supported; one of: Password."], result.Failures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Password_authentication_requires_a_user(string? user)
    {
        var options = ValidOptions();
        options.User = user;

        var result = _validator.Validate(null, options);

        Assert.Equal(["Database:User is required for Password authentication."], result.Failures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Password_authentication_requires_a_password(string? password)
    {
        var options = ValidOptions();
        options.Password = password;

        var result = _validator.Validate(null, options);

        Assert.Equal(["Database:Password is required for Password authentication."], result.Failures);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var result = _validator.Validate(null, new DatabaseOptions { Authentication = DatabaseAuthentication.Password });

        Assert.Equal(
            [
                "Database:ConnectionString is required.",
                "Database:User is required for Password authentication.",
                "Database:Password is required for Password authentication.",
            ],
            result.Failures);
    }

    private static DatabaseOptions ValidOptions() => new()
    {
        ConnectionString = "Server=localhost,1433;Database=FacilityManagement;Encrypt=True;TrustServerCertificate=True",
        Authentication = DatabaseAuthentication.Password,
        User = "sa",
        Password = "secret",
    };
}
