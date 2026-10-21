using System.Net;
using FacilityManagementService.API.Tests.Http.Problems;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace FacilityManagementService.API.Tests;

/// <summary>
/// The real host, as <c>Program</c> composes it, in Production. It starts with
/// <c>ValidateOnBuild</c>, so every registration is checked. The database settings only need to
/// pass validation; no test opens a connection.
/// </summary>
public sealed class ProgramTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseEnvironment(Environments.Production)
            .UseSetting("Database:ConnectionString", "Server=localhost;Database=Unused")
            .UseSetting("Database:Authentication", "Password")
            .UseSetting("Database:User", "user")
            .UseSetting("Database:Password", "password")
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            }));
        _client = _factory.CreateClient();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task The_openapi_document_is_served_outside_Development()
    {
        using var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_route_is_a_not_found_problem()
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, new HttpRequestMessage(HttpMethod.Get, "/nowhere"), HttpStatusCode.NotFound, "not-found");
    }
}
