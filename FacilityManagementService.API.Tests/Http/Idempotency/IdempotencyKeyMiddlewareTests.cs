using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Http.Responses;
using FacilityManagementService.API.Tests.Http.Authentication;
using FacilityManagementService.API.Tests.Http.Problems;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FacilityManagementService.API.Tests.Http.Idempotency;

public sealed class IdempotencyKeyMiddlewareTests : IAsyncLifetime
{
    private static readonly UserId SecondUserId =
        UserId.From(Guid.Parse("01933b4a-8c10-7000-8000-000000000002"));

    private readonly FakeIdempotencyStore _store = new();
    private readonly MutableIdentityLookup _identityLookup = new();

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private int _createCount;
    private int _acceptCount;
    private bool _failNextCreate;
    private TaskCompletionSource? _holdEntered;
    private TaskCompletionSource? _releaseHold;
    private ConflictException? _conflictToThrow;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(
            app =>
            {
                app.MapPost("/things", async (ThingPayload? payload, IdempotencyContext idempotency, CancellationToken ct) =>
                {
                    if (_conflictToThrow is { } conflict)
                    {
                        throw conflict;
                    }

                    if (_holdEntered is not null && _releaseHold is not null)
                    {
                        _holdEntered.TrySetResult();
                        await _releaseHold.Task.WaitAsync(ct);
                    }

                    var id = await _store.ExecuteAsync(
                        idempotency,
                        () =>
                        {
                            if (_failNextCreate)
                            {
                                _failNextCreate = false;
                                throw new InvalidOperationException("Boom.");
                            }

                            _createCount++;
                            return ThingId.New();
                        });

                    return TypedResults.Created($"/things/{id.Value}", CreatedResponse.From(id));
                });

                app.MapPost("/other-things", async (ThingPayload? payload, IdempotencyContext idempotency) =>
                {
                    var id = await _store.ExecuteAsync(
                        idempotency,
                        () =>
                        {
                            _createCount++;
                            return ThingId.New();
                        });

                    return TypedResults.Created($"/other-things/{id.Value}", CreatedResponse.From(id));
                });

                app.MapPost("/invitations/accept", async (ThingPayload? payload, IdempotencyContext idempotency) =>
                {
                    var id = await _store.ExecuteAsync(
                        idempotency,
                        () =>
                        {
                            _acceptCount++;
                            // Accepting an invitation links the caller's identity to a user.
                            _identityLookup.LinkUnregisteredSubject = true;
                            return ThingId.New();
                        });

                    return TypedResults.Ok(CreatedResponse.From(id));
                }).AllowUnregisteredUser();

                app.MapPut("/things", () => Results.Ok());
                app.MapDelete("/things", () => Results.Ok());
                app.MapGet("/things", () => Results.Ok());
            },
            identityLookup: _identityLookup,
            idempotencyLookup: _store);

        _client = _app.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task A_post_without_a_key_is_rejected()
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, Post(), HttpStatusCode.BadRequest, "idempotency-key-missing");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("åäö")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public async Task A_post_with_a_malformed_key_is_rejected(string key)
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, Post(key), HttpStatusCode.BadRequest, "idempotency-key-invalid");
    }

    [Fact]
    public async Task A_post_with_two_keys_is_rejected()
    {
        var request = Post();
        request.Headers.TryAddWithoutValidation("Idempotency-Key", ["first", "second"]);

        await ProblemResponseTests.SendForProblemAsync(
            _client, request, HttpStatusCode.BadRequest, "idempotency-key-invalid");
    }

    [Theory]
    [InlineData(TestHost.IdempotencyKey)]
    [InlineData("a")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234")]
    public async Task A_post_with_a_valid_key_reaches_the_endpoint(string key)
    {
        using var response = await _client.SendAsync(Post(key, new ThingPayload("Sauna")), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Other_methods_need_no_key(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/things");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_post_to_an_unknown_route_is_not_found_rather_than_missing_a_key()
    {
        await ProblemResponseTests.SendForProblemAsync(
            _client, new HttpRequestMessage(HttpMethod.Post, "/nowhere"), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task Repeating_a_committed_post_with_the_same_key_replays_the_original_response_without_rerunning_the_handler()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await _client.SendAsync(Post("key-1", new ThingPayload("Sauna")), ct);
        using var second = await _client.SendAsync(Post("key-1", new ThingPayload("Sauna")), ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(1, _createCount);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(ct);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(ct);

        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(firstBody.GetProperty("id").GetGuid(), secondBody.GetProperty("id").GetGuid());
        Assert.Equal(firstBody.GetProperty("warnings").GetRawText(), secondBody.GetProperty("warnings").GetRawText());
    }

    [Fact]
    public async Task Repeating_a_failed_post_with_the_same_key_runs_the_handler_again()
    {
        var ct = TestContext.Current.CancellationToken;
        _failNextCreate = true;

        await ProblemResponseTests.SendForProblemAsync(
            _client, Post("key-fail", new ThingPayload("Sauna")), HttpStatusCode.InternalServerError, "internal-error");

        using var retry = await _client.SendAsync(Post("key-fail", new ThingPayload("Sauna")), ct);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, _createCount);
    }

    [Fact]
    public async Task Reusing_a_committed_key_with_a_different_body_is_rejected_as_idempotency_key_reused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await _client.SendAsync(Post("key-reused", new ThingPayload("Sauna")), ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await ProblemResponseTests.SendForProblemAsync(
            _client,
            Post("key-reused", new ThingPayload("Boat")),
            HttpStatusCode.UnprocessableEntity,
            "idempotency-key-reused");

        Assert.Equal(1, _createCount);
    }

    [Fact]
    public async Task Reusing_a_committed_key_on_a_different_route_is_rejected_as_idempotency_key_reused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await _client.SendAsync(Post("key-route", new ThingPayload("Sauna"), "/things"), ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await ProblemResponseTests.SendForProblemAsync(
            _client,
            Post("key-route", new ThingPayload("Sauna"), "/other-things"),
            HttpStatusCode.UnprocessableEntity,
            "idempotency-key-reused");

        Assert.Equal(1, _createCount);
    }

    [Fact]
    public async Task The_same_key_from_two_different_callers_runs_both_commands()
    {
        var ct = TestContext.Current.CancellationToken;
        using var secondClient = _app.CreateClient(TestTokens.Issue(subject: "second-subject"));

        using var first = await _client.SendAsync(Post("shared-key", new ThingPayload("Sauna")), ct);
        using var second = await secondClient.SendAsync(Post("shared-key", new ThingPayload("Boat")), ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(2, _createCount);
    }

    [Fact]
    public async Task An_endpoint_for_unregistered_users_scopes_keys_by_identity_even_after_the_identity_is_linked()
    {
        var ct = TestContext.Current.CancellationToken;
        using var unregisteredClient = _app.CreateClient(TestTokens.Issue(subject: TestTokens.UnregisteredSubject));

        using var first = await unregisteredClient.SendAsync(
            Post("accept-key", new ThingPayload("invite"), "/invitations/accept"), ct);
        using var second = await unregisteredClient.SendAsync(
            Post("accept-key", new ThingPayload("invite"), "/invitations/accept"), ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, _acceptCount);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(ct);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(firstBody.GetProperty("id").GetGuid(), secondBody.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_concurrent_post_with_the_same_key_is_rejected_as_idempotency_key_in_use_with_retry_after()
    {
        var ct = TestContext.Current.CancellationToken;
        _holdEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _releaseHold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = _client.SendAsync(Post("in-flight-key", new ThingPayload("Sauna")), ct);
        await _holdEntered.Task.WaitAsync(ct);

        using var duplicateResponse = await _client.SendAsync(Post("in-flight-key", new ThingPayload("Sauna")), ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("1", duplicateResponse.Headers.RetryAfter?.Delta?.TotalSeconds.ToString("0"));
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("idempotency-key-in-use", problem.GetProperty("code").GetString());

        _releaseHold.TrySetResult();
        using var firstResponse = await firstTask;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(1, _createCount);
    }

    [Fact]
    public async Task A_unique_index_race_on_command_markers_returns_idempotency_key_in_use_with_retry_after()
    {
        var ct = TestContext.Current.CancellationToken;
        _conflictToThrow = new ConflictException(null, "PK_CommandMarkers", new InvalidOperationException());

        using var response = await _client.SendAsync(Post("race-key", new ThingPayload("Sauna")), ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("1", response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString("0"));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("idempotency-key-in-use", problem.GetProperty("code").GetString());
    }

    private static HttpRequestMessage Post(string? key = null, ThingPayload? payload = null, string path = "/things")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        return request;
    }

    private sealed record ThingPayload(string Name);

    private sealed class FakeIdempotencyStore : IIdempotencyKeyLookup
    {
        private readonly ConcurrentDictionary<(string Scope, string Key), StoredIdempotencyKey> _entries = new();

        public Task<StoredIdempotencyKey?> FindAsync(
            string scope,
            string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.TryGetValue((scope, key), out var entry) ? entry : null);

        public Task<TResult> ExecuteAsync<TResult>(IdempotencyContext context, Func<TResult> action)
        {
            if (context.TryGetReplayedResult<TResult>(out var replayed))
            {
                return Task.FromResult(replayed);
            }

            var result = action();

            if (context.Request is { } request)
            {
                _entries[(request.Scope, request.Key)] = new StoredIdempotencyKey(
                    request.Fingerprint,
                    CommandResultSerializer.Serialize(result));
            }

            return Task.FromResult(result);
        }
    }

    private sealed class MutableIdentityLookup : IUserIdentityLookup
    {
        public bool LinkUnregisteredSubject { get; set; }

        public Task<UserId?> FindUserIdAsync(
            string provider,
            string subject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserId?>(subject switch
            {
                TestTokens.RegisteredSubject => TestTokens.RegisteredUserId,
                "second-subject" => SecondUserId,
                TestTokens.UnregisteredSubject when LinkUnregisteredSubject => SecondUserId,
                _ => null,
            });
    }
}
