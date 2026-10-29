using System.Net.Http.Headers;
using FacilityManagementService.API.Tests.Http.Authentication;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FacilityManagementService.API.Tests;

/// <summary>
/// An in-memory host with the API's conventions, <c>AddApi()</c> and <c>UseApi()</c> as
/// <c>Program</c> calls them, and the endpoints a test maps. Tokens are validated in-process
/// with <see cref="TestTokens"/>, and logs go to a fake collector.
/// </summary>
internal static class TestHost
{
    public const string IdempotencyKey = "6f1c9d0e-4b9a-4f57-9c1e-2a4b8f3d7e10";

    private static readonly Dictionary<string, string?> DefaultSettings = new()
    {
        ["Authentication:Authority"] = TestTokens.Authority,
        ["Authentication:Audience"] = TestTokens.Audience,
        ["Authentication:Provider"] = TestTokens.Provider,
        ["Authentication:SubjectClaim"] = TestTokens.SubjectClaim,
    };

    public static async Task<WebApplication> StartAsync(
        Action<WebApplication> mapEndpoints,
        string environment = "Production",
        IReadOnlyDictionary<string, string?>? settings = null,
        IUserIdentityLookup? identityLookup = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(DefaultSettings);
        builder.Configuration.AddInMemoryCollection(settings ?? new Dictionary<string, string?>());
        builder.Logging.ClearProviders().AddFakeLogging();
        builder.Services.AddApi();
        builder.Services.AddScoped<IUserIdentityLookup>(_ => identityLookup ?? new DefaultTestUserIdentityLookup());
        builder.Services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            TestTokens.ConfigureJwtBearer);

        var app = builder.Build();
        app.UseApi();
        mapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        return app;
    }

    /// <summary>
    /// Creates a test client that sends a bearer token for <see cref="TestTokens.RegisteredUserId"/>
    /// (or <paramref name="token"/> when given). Use <c>GetTestClient()</c> for an
    /// unauthenticated client.
    /// </summary>
    public static HttpClient CreateClient(this WebApplication app, string? token = null)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token ?? TestTokens.RegisteredUserToken);
        return client;
    }

    private sealed class DefaultTestUserIdentityLookup : IUserIdentityLookup
    {
        public Task<UserId?> FindUserIdAsync(
            string provider,
            string subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserId?>(
                subject == TestTokens.RegisteredSubject ? TestTokens.RegisteredUserId : null);
    }
}
