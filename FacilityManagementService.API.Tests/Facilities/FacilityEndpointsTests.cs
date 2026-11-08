using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityManagementService.API.Facilities;
using FacilityManagementService.API.Tests.Http.Authentication;
using FacilityManagementService.API.Tests.Users;
using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityManagementService.API.Tests.Facilities;

public sealed class FacilityEndpointsTests : IAsyncLifetime
{
    private static readonly FacilityId SampleFacilityId =
        FacilityId.From(Guid.Parse("01933b4a-8c10-7000-8000-000000000010"));
    private static readonly FacilityInvitationId SampleInvitationId =
        FacilityInvitationId.From(Guid.Parse("01933b4a-8c10-7000-8000-000000000020"));
    private static readonly UserId SampleUserId =
        UserId.From(Guid.Parse("01933b4a-8c10-7000-8000-000000000030"));

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private CreateFacility? _createdFacility;
    private RenameFacility? _renamedFacility;
    private SetFacilityCapacity? _setCapacity;
    private CreateFacilityInvitation? _createdInvitation;
    private RevokeFacilityInvitation? _revokedInvitation;
    private SetFacilityMemberRole? _setMemberRole;
    private RemoveFacilityMember? _removedMember;
    private AcceptInvitation? _acceptedInvitation;

    public async ValueTask InitializeAsync()
    {
        _app = await TestHost.StartAsync(
            app => app.MapFacilityEndpoints(),
            configureServices: services =>
            {
                services.AddScoped<ICommandHandler<CreateFacility, FacilityId>>(_ =>
                    new DelegateCommandHandler<CreateFacility, FacilityId>((cmd, _) =>
                    {
                        _createdFacility = cmd;
                        return Task.FromResult(SampleFacilityId);
                    }));
                services.AddScoped<IQueryHandler<GetFacilities, IReadOnlyList<FacilitySummary>>>(_ =>
                    new DelegateQueryHandler<GetFacilities, IReadOnlyList<FacilitySummary>>((_, _) =>
                        Task.FromResult<IReadOnlyList<FacilitySummary>>(
                        [
                            new FacilitySummary(SampleFacilityId, "Summer House", 10, FacilityRole.Administrator),
                        ])));
                services.AddScoped<IQueryHandler<GetFacility, FacilityDetails>>(_ =>
                    new DelegateQueryHandler<GetFacility, FacilityDetails>((query, _) =>
                        Task.FromResult(new FacilityDetails(
                            query.FacilityId,
                            "Summer House",
                            10,
                            [
                                new FacilityMemberDetails(SampleUserId, "Alice", FacilityRole.Administrator),
                                new FacilityMemberDetails(TestTokens.RegisteredUserId, "Bob", FacilityRole.Owner),
                            ]))));
                services.AddScoped<ICommandHandler<RenameFacility, Unit>>(_ =>
                    new DelegateCommandHandler<RenameFacility, Unit>((cmd, _) =>
                    {
                        _renamedFacility = cmd;
                        return Task.FromResult(Unit.Value);
                    }));
                services.AddScoped<ICommandHandler<SetFacilityCapacity, Unit>>(_ =>
                    new DelegateCommandHandler<SetFacilityCapacity, Unit>((cmd, _) =>
                    {
                        _setCapacity = cmd;
                        return Task.FromResult(Unit.Value);
                    }));
                services.AddScoped<ICommandHandler<CreateFacilityInvitation, CreatedFacilityInvitation>>(_ =>
                    new DelegateCommandHandler<CreateFacilityInvitation, CreatedFacilityInvitation>((cmd, _) =>
                    {
                        _createdInvitation = cmd;
                        var expiresAt = new DateTimeOffset(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(1));
                        return Task.FromResult(new CreatedFacilityInvitation(
                            SampleInvitationId,
                            "raw-secret-token",
                            expiresAt));
                    }));
                services.AddScoped<IQueryHandler<GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>>>(_ =>
                    new DelegateQueryHandler<GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>>((_, _) =>
                        Task.FromResult<IReadOnlyList<PendingFacilityInvitation>>(
                        [
                            new PendingFacilityInvitation(
                                SampleInvitationId,
                                FacilityRole.Owner,
                                new DateTimeOffset(2026, 11, 8, 14, 0, 0, TimeSpan.FromHours(1)),
                                new DateTimeOffset(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(1))),
                        ])));
                services.AddScoped<ICommandHandler<RevokeFacilityInvitation, Unit>>(_ =>
                    new DelegateCommandHandler<RevokeFacilityInvitation, Unit>((cmd, _) =>
                    {
                        _revokedInvitation = cmd;
                        return Task.FromResult(Unit.Value);
                    }));
                services.AddScoped<ICommandHandler<SetFacilityMemberRole, Unit>>(_ =>
                    new DelegateCommandHandler<SetFacilityMemberRole, Unit>((cmd, _) =>
                    {
                        _setMemberRole = cmd;
                        return Task.FromResult(Unit.Value);
                    }));
                services.AddScoped<ICommandHandler<RemoveFacilityMember, Unit>>(_ =>
                    new DelegateCommandHandler<RemoveFacilityMember, Unit>((cmd, _) =>
                    {
                        _removedMember = cmd;
                        return Task.FromResult(Unit.Value);
                    }));
                services.AddScoped<ICommandHandler<AcceptInvitation, AcceptedInvitation>>(_ =>
                    new DelegateCommandHandler<AcceptInvitation, AcceptedInvitation>((cmd, _) =>
                    {
                        _acceptedInvitation = cmd;
                        return Task.FromResult(new AcceptedInvitation(
                            SampleFacilityId,
                            SampleUserId,
                            FacilityRole.Member));
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
    public async Task Create_facility_returns_201_created_with_location_and_id()
    {
        using var request = Post("/facilities", new { name = "Summer House", capacity = 10 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/facilities/{SampleFacilityId.Value}", response.Headers.Location?.OriginalString);
        Assert.Equal(new CreateFacility("Summer House", 10), _createdFacility);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(SampleFacilityId.Value, body.GetProperty("id").GetGuid());
        Assert.Equal("[]", body.GetProperty("warnings").GetRawText());
    }

    [Fact]
    public async Task Get_facilities_returns_summaries_with_camel_case_role()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>("/facilities", TestContext.Current.CancellationToken);

        var item = Assert.Single(body.EnumerateArray());
        Assert.Equal(SampleFacilityId.Value, item.GetProperty("id").GetGuid());
        Assert.Equal("Summer House", item.GetProperty("name").GetString());
        Assert.Equal(10, item.GetProperty("capacity").GetInt32());
        Assert.Equal("administrator", item.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Get_facility_returns_details_and_members()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>(
            $"/facilities/{SampleFacilityId.Value}",
            TestContext.Current.CancellationToken);

        Assert.Equal(SampleFacilityId.Value, body.GetProperty("id").GetGuid());
        Assert.Equal("Summer House", body.GetProperty("name").GetString());
        Assert.Equal(10, body.GetProperty("capacity").GetInt32());

        var members = body.GetProperty("members").EnumerateArray().ToArray();
        Assert.Equal(2, members.Length);
        Assert.Equal("administrator", members[0].GetProperty("role").GetString());
        Assert.Equal("owner", members[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Rename_facility_maps_route_and_body()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/facilities/{SampleFacilityId.Value}/name",
            new { name = "Winter Cabin" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RenameFacility(SampleFacilityId, "Winter Cabin"), _renamedFacility);
    }

    [Fact]
    public async Task Set_facility_capacity_maps_route_and_body()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/facilities/{SampleFacilityId.Value}/capacity",
            new { capacity = 14 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SetFacilityCapacity(SampleFacilityId, 14), _setCapacity);
    }

    [Fact]
    public async Task Create_facility_invitation_returns_201_with_token_and_utc_expiry()
    {
        using var request = Post($"/facilities/{SampleFacilityId.Value}/invitations", new { role = "owner" });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/facilities/{SampleFacilityId.Value}/invitations/{SampleInvitationId.Value}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(new CreateFacilityInvitation(SampleFacilityId, FacilityRole.Owner), _createdInvitation);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(SampleInvitationId.Value, body.GetProperty("id").GetGuid());
        Assert.Equal("raw-secret-token", body.GetProperty("token").GetString());
        Assert.EndsWith("Z", body.GetProperty("expiresAt").GetString(), StringComparison.Ordinal);
        Assert.Equal("[]", body.GetProperty("warnings").GetRawText());
    }

    [Fact]
    public async Task Get_facility_invitations_returns_pending_invitations_with_utc_timestamps()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>(
            $"/facilities/{SampleFacilityId.Value}/invitations",
            TestContext.Current.CancellationToken);

        var invitation = Assert.Single(body.EnumerateArray());
        Assert.Equal(SampleInvitationId.Value, invitation.GetProperty("id").GetGuid());
        Assert.Equal("owner", invitation.GetProperty("role").GetString());
        Assert.EndsWith("Z", invitation.GetProperty("createdAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("Z", invitation.GetProperty("expiresAt").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_facility_invitation_maps_route_and_returns_command_response()
    {
        using var response = await _client.DeleteAsync(
            $"/facilities/{SampleFacilityId.Value}/invitations/{SampleInvitationId.Value}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RevokeFacilityInvitation(SampleFacilityId, SampleInvitationId), _revokedInvitation);
    }

    [Fact]
    public async Task Set_facility_member_role_maps_route_and_body()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/facilities/{SampleFacilityId.Value}/members/{SampleUserId.Value}",
            new { role = "owner" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SetFacilityMemberRole(SampleFacilityId, SampleUserId, FacilityRole.Owner), _setMemberRole);
    }

    [Fact]
    public async Task Remove_facility_member_maps_route_and_returns_command_response()
    {
        using var response = await _client.DeleteAsync(
            $"/facilities/{SampleFacilityId.Value}/members/{SampleUserId.Value}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemoveFacilityMember(SampleFacilityId, SampleUserId), _removedMember);
    }

    [Fact]
    public async Task Accept_invitation_allows_unregistered_caller_and_returns_outcome()
    {
        var token = TestTokens.Issue(subject: TestTokens.UnregisteredSubject);
        using var client = _app.CreateClient(token);
        using var request = Post("/invitations/accept", new { token = "invite-token", displayName = "Charlie" });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AcceptInvitation("invite-token", "Charlie"), _acceptedInvitation);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(SampleFacilityId.Value, body.GetProperty("facilityId").GetGuid());
        Assert.Equal(SampleUserId.Value, body.GetProperty("userId").GetGuid());
        Assert.Equal("member", body.GetProperty("role").GetString());
        Assert.Equal("[]", body.GetProperty("warnings").GetRawText());
    }

    [Fact]
    public async Task Integer_enum_value_in_request_body_is_rejected_as_malformed_request()
    {
        using var request = Post($"/facilities/{SampleFacilityId.Value}/invitations", new { role = 99 });

        await Http.Problems.ProblemResponseTests.SendForProblemAsync(
            _client,
            request,
            HttpStatusCode.BadRequest,
            "malformed-request");
    }

    private static HttpRequestMessage Post(string path, object body) => new(HttpMethod.Post, path)
    {
        Content = JsonContent.Create(body),
        Headers = { { "Idempotency-Key", TestHost.IdempotencyKey } },
    };
}
