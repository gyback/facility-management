using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FacilityManagementService.API.Tests;

/// <summary>
/// An in-memory host with the API's conventions, <c>AddApi()</c> and <c>UseApi()</c> as
/// <c>Program</c> calls them, and the endpoints a test maps. Logs go to a fake collector.
/// </summary>
internal static class TestHost
{
    public const string IdempotencyKey = "6f1c9d0e-4b9a-4f57-9c1e-2a4b8f3d7e10";

    public static async Task<WebApplication> StartAsync(
        Action<WebApplication> mapEndpoints,
        string environment = "Production",
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
        builder.Logging.ClearProviders().AddFakeLogging();
        builder.Services.AddApi();

        var app = builder.Build();
        app.UseApi();
        mapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }
}
