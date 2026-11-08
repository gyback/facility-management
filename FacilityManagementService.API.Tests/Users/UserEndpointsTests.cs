using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityManagementService.API.Tests.Http.Authentication;
using FacilityManagementService.API.Tests.Http.Problems;
using FacilityManagementService.API.Users;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityManagementService.API.Tests.Users;

public sealed class UserEndpointsTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private CurrentUserProfile _profile = new(
        IsRegistered: true,
        UserId: TestTokens.RegisteredUserId,
        DisplayName: "Alice",
        CanCreateFacilities: true);
    private UpdateCurrentUser? _updatedCommand;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(
            app => app.MapUserEndpoints(),
            configureServices: services =>
            {
                services.AddScoped<IQueryHandler<GetCurrentUser, CurrentUserProfile>>(_ =>
                    new DelegateQueryHandler<GetCurrentUser, CurrentUserProfile>((_, _) => Task.FromResult(_profile)));
                services.AddScoped<ICommandHandler<UpdateCurrentUser, Unit>>(_ =>
                    new DelegateCommandHandler<UpdateCurrentUser, Unit>((command, _) =>
                    {
                        _updatedCommand = command;
                        return Task.FromResult(Unit.Value);
                    }));
            });
        _client = _app.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Get_current_user_returns_registered_profile()
    {
        var json = await _client.GetFromJsonAsync<JsonElement>("/users/me", TestContext.Current.CancellationToken);

        Assert.True(json.GetProperty("isRegistered").GetBoolean());
        Assert.Equal(TestTokens.RegisteredUserId.Value, json.GetProperty("userId").GetGuid());
        Assert.Equal("Alice", json.GetProperty("displayName").GetString());
        Assert.True(json.GetProperty("canCreateFacilities").GetBoolean());
    }

    [Fact]
    public async Task Get_current_user_allows_unregistered_caller()
    {
        _profile = new CurrentUserProfile(
            IsRegistered: false,
            UserId: null,
            DisplayName: "Newbie",
            CanCreateFacilities: false);
        var token = TestTokens.Issue(subject: TestTokens.UnregisteredSubject, displayName: "Newbie");
        using var client = _app.CreateClient(token);

        var json = await client.GetFromJsonAsync<JsonElement>("/users/me", TestContext.Current.CancellationToken);

        Assert.False(json.GetProperty("isRegistered").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("userId").ValueKind);
        Assert.Equal("Newbie", json.GetProperty("displayName").GetString());
        Assert.False(json.GetProperty("canCreateFacilities").GetBoolean());
    }

    [Fact]
    public async Task Update_current_user_requires_registered_caller()
    {
        var token = TestTokens.Issue(subject: TestTokens.UnregisteredSubject);
        using var client = _app.CreateClient(token);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/users/me")
        {
            Content = JsonContent.Create(new { displayName = "Bob" }),
        };

        await ProblemResponseTests.SendForProblemAsync(
            client,
            request,
            HttpStatusCode.Forbidden,
            "user-not-registered");
    }

    [Fact]
    public async Task Update_current_user_maps_request_and_returns_command_response()
    {
        using var response = await _client.PutAsJsonAsync(
            "/users/me",
            new { displayName = "Alice Updated" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new UpdateCurrentUser("Alice Updated"), _updatedCommand);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("[]", body.GetProperty("warnings").GetRawText());
    }
}

internal sealed class DelegateCommandHandler<TCommand, TResult>(
    Func<TCommand, CancellationToken, Task<TResult>> handle) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default) =>
        handle(command, cancellationToken);
}

internal sealed class DelegateQueryHandler<TQuery, TResult>(
    Func<TQuery, CancellationToken, Task<TResult>> handle) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default) =>
        handle(query, cancellationToken);
}
