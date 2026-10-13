using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace FacilityManagementService.Infrastructure.Tests.Persistence;

public sealed class DatabaseOptionsTests
{
    [Fact]
    public void Password_authentication_adds_the_user_and_password_to_the_connection_string()
    {
        var options = new DatabaseOptions
        {
            ConnectionString = "Server=localhost,1433;Database=FacilityManagement;Encrypt=True",
            Authentication = DatabaseAuthentication.Password,
            User = "sa",
            Password = "p;ss=word",
        };

        var builder = new SqlConnectionStringBuilder(options.BuildConnectionString());

        Assert.Equal("localhost,1433", builder.DataSource);
        Assert.Equal("FacilityManagement", builder.InitialCatalog);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
        Assert.Equal("sa", builder.UserID);
        Assert.Equal("p;ss=word", builder.Password);
    }

    [Fact]
    public void An_unsupported_authentication_is_rejected()
    {
        var options = new DatabaseOptions { ConnectionString = "Server=localhost" };

        Assert.Throws<InvalidOperationException>(() => options.BuildConnectionString());
    }
}
