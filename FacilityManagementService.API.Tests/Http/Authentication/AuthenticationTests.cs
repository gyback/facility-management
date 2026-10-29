using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Tests.Http.Problems;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace FacilityManagementService.API.Tests.Http.Authentication;

public sealed class AuthenticationTests : IAsyncLifetime
{
    private readonly CountingIdentityLookup _lookup = new();
    private WebApplication _app = null!;
    private bool _protectedEndpointRan;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(
            app =>
            {
                app.MapGet("/protected", (ICurrentUser currentUser) =>
                {
                    _protectedEndpointRan = true;
                    return Results.Ok(new CallerResponse(
                        currentUser.Id.Value,
                        currentUser.Identity.Provider,
                        currentUser.Identity.Subject,
                        currentUser.Identity.DisplayName));
                });

                app.MapGet("/identity", (ICurrentUser currentUser) =>
                    Results.Ok(currentUser.Identity))
                    .AllowUnregisteredUser();

                app.MapGet("/identity-reading-id", (ICurrentUser currentUser) =>
                    Results.Ok(currentUser.Id.Value))
                    .AllowUnregisteredUser();

                app.MapGet("/public", () => Results.Ok("ok"))
                    .AllowAnonymous();
            },
            identityLookup: _lookup);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    [Fact]
    public async Task A_request_without_a_token_is_unauthenticated()
    {
        using var client = _app.GetTestClient();

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");

        Assert.False(_protectedEndpointRan);
        Assert.Equal(0, _lookup.Calls);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJnoneifQ.eyJzdWIiOiIxIn0.")]
    public async Task A_malformed_token_is_unauthenticated(string token)
    {
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");
    }

    [Fact]
    public async Task An_expired_token_is_unauthenticated()
    {
        var now = DateTimeOffset.UtcNow;
        var token = TestTokens.Issue(notBefore: now.AddMinutes(-10), expires: now.AddMinutes(-5));
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_unauthenticated()
    {
        var token = TestTokens.Issue(signingKey: TestTokens.UntrustedSigningKey);
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");
    }

    [Fact]
    public async Task A_token_for_another_audience_is_unauthenticated()
    {
        var token = TestTokens.Issue(audience: "another-api");
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_unauthenticated()
    {
        var token = TestTokens.Issue(issuer: "https://other.example.test");
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_token_without_the_configured_subject_claim_is_unauthenticated(string? subject)
    {
        var token = TestTokens.Issue(subject: subject);
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Unauthorized,
            "unauthenticated");

        Assert.Equal(0, _lookup.Calls);
    }

    [Fact]
    public async Task A_signed_in_identity_that_is_not_linked_to_a_user_is_rejected_before_the_endpoint_runs()
    {
        var token = TestTokens.Issue(subject: TestTokens.UnregisteredSubject);
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/protected"),
            HttpStatusCode.Forbidden,
            "user-not-registered");

        Assert.False(_protectedEndpointRan);
        Assert.Equal(1, _lookup.Calls);
    }

    [Fact]
    public async Task An_unlinked_identity_can_call_an_endpoint_that_opts_out_of_the_fallback_policy()
    {
        var token = TestTokens.Issue(subject: TestTokens.UnregisteredSubject, displayName: "Bob");
        using var client = _app.CreateClient(token);

        var identity = await client.GetFromJsonAsync<ExternalIdentity>(
            "/identity", TestContext.Current.CancellationToken);

        Assert.Equal(new ExternalIdentity(TestTokens.Provider, TestTokens.UnregisteredSubject, "Bob"), identity);
    }

    [Fact]
    public async Task Reading_the_user_id_for_an_unlinked_identity_is_a_user_not_registered_problem()
    {
        var token = TestTokens.Issue(subject: TestTokens.UnregisteredSubject);
        using var client = _app.CreateClient(token);

        await ProblemResponseTests.SendForProblemAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/identity-reading-id"),
            HttpStatusCode.Forbidden,
            "user-not-registered");
    }

    [Fact]
    public async Task A_linked_identity_resolves_the_current_user_once_for_the_request()
    {
        using var client = _app.CreateClient();

        var caller = await client.GetFromJsonAsync<CallerResponse>(
            "/protected", TestContext.Current.CancellationToken);

        Assert.Equal(
            new CallerResponse(
                TestTokens.RegisteredUserId.Value,
                TestTokens.Provider,
                TestTokens.RegisteredSubject,
                TestTokens.DisplayName),
            caller);
        Assert.Equal(1, _lookup.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_missing_or_blank_name_claim_gives_a_null_display_name(string? name)
    {
        var token = TestTokens.Issue(displayName: name);
        using var client = _app.CreateClient(token);

        var caller = await client.GetFromJsonAsync<CallerResponse>(
            "/protected", TestContext.Current.CancellationToken);

        Assert.Null(caller!.DisplayName);
    }

    [Fact]
    public async Task The_configured_provider_key_and_subject_claim_are_used_without_inbound_claim_mapping()
    {
        await using var app = await TestHost.StartAsync(
             endpoints => endpoints.MapGet("/identity", (ICurrentUser currentUser) => Results.Ok(currentUser.Identity))
                .AllowUnregisteredUser(),
            settings: new Dictionary<string, string?>
            {
                ["Authentication:Provider"] = "entra",
                ["Authentication:SubjectClaim"] = "oid",
            });

        var token = TestTokens.Issue(subject: "entra-object-id", subjectClaim: "oid");
        using var client = app.CreateClient(token);

        var identity = await client.GetFromJsonAsync<ExternalIdentity>(
            "/identity", TestContext.Current.CancellationToken);

        Assert.Equal(new ExternalIdentity("entra", "entra-object-id", TestTokens.DisplayName), identity);
    }

    [Fact]
    public async Task An_anonymous_endpoint_needs_no_token_and_does_not_look_up_a_user_when_given_one()
    {
        using var unauthenticated = _app.GetTestClient();
        using var anonymousResponse = await unauthenticated.GetAsync("/public", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);

        using var authenticated = _app.CreateClient();
        using var authenticatedResponse = await authenticated.GetAsync("/public", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);

        Assert.Equal(0, _lookup.Calls);
    }

    private sealed record CallerResponse(
        Guid UserId,
        string Provider,
        string Subject,
        string? DisplayName);

    private sealed class CountingIdentityLookup : IUserIdentityLookup
    {
        public int Calls { get; private set; }

        public Task<UserId?> FindUserIdAsync(
            string provider,
            string subject,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<UserId?>(
                subject == TestTokens.RegisteredSubject ? TestTokens.RegisteredUserId : null);
        }
    }
}

