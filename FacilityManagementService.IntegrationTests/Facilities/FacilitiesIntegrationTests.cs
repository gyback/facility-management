using FacilityManagementService.Application;
using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;
using FacilityManagementService.Infrastructure;
using FacilityManagementService.Infrastructure.Identity;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityManagementService.IntegrationTests.Facilities;

public sealed class FacilitiesIntegrationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private TestDatabase<AppDbContext> _database = null!;
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _database = await sqlServer.CreateDatabaseAsync<AppDbContext>(options => new(options), cancellationToken);

        try
        {
            await using var context = _database.CreateContext();
            _services = BuildServices(context.Database.GetConnectionString()!);
        }
        catch
        {
            await _database.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task User_identity_lookup_and_get_current_user_reflect_registration_state()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = User.Register("Alice", "entra", "oid-alice");
        user.GrantFacilityCreation();

        await using (var seed = _database.CreateContext())
        {
            seed.Set<User>().Add(user);
            await seed.SaveChangesAsync(cancellationToken);
        }

        // Linked identity resolves UserId and returns registered profile.
        await using (var scope = CreateAuthenticatedScope(new ExternalIdentity("entra", "oid-alice", "alice@example.com"), user.Id))
        {
            var lookup = scope.ServiceProvider.GetRequiredService<IUserIdentityLookup>();
            Assert.Equal(user.Id, await lookup.FindUserIdAsync("entra", "oid-alice", cancellationToken));
            Assert.Null(await lookup.FindUserIdAsync("entra", "oid-other", cancellationToken));

            var getCurrentUser = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetCurrentUser, CurrentUserProfile>>();
            var profile = await getCurrentUser.HandleAsync(new GetCurrentUser(), cancellationToken);

            Assert.Equal(new CurrentUserProfile(true, user.Id, "Alice", true), profile);
        }

        // Unlinked identity with an email name claim hides the email and returns unregistered profile.
        await using (var scope = CreateAuthenticatedScope(new ExternalIdentity("entra", "oid-new", "newbie@example.com"), userId: null))
        {
            var getCurrentUser = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetCurrentUser, CurrentUserProfile>>();
            var profile = await getCurrentUser.HandleAsync(new GetCurrentUser(), cancellationToken);

            Assert.Equal(new CurrentUserProfile(false, null, null, false), profile);
        }
    }

    [Fact]
    public async Task Full_facility_and_invitation_lifecycle_persists_and_queries_through_sql_server()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var adminIdentity = new ExternalIdentity("entra", "oid-admin", "Alice");
        var admin = User.Register("Alice", adminIdentity.Provider, adminIdentity.Subject);
        admin.GrantFacilityCreation();

        await using (var seed = _database.CreateContext())
        {
            seed.Set<User>().Add(admin);
            await seed.SaveChangesAsync(cancellationToken);
        }

        // 1. Admin creates a facility and issues an Owner invitation and a Member invitation.
        FacilityId facilityId;
        CreatedFacilityInvitation ownerInvitation;
        CreatedFacilityInvitation memberInvitation;
        await using (var scope = CreateAuthenticatedScope(adminIdentity, admin.Id))
        {
            var createFacility = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateFacility, FacilityId>>();
            facilityId = await createFacility.HandleAsync(new CreateFacility("Summer House", 10), cancellationToken);

            var createInvitation = scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateFacilityInvitation, CreatedFacilityInvitation>>();
            ownerInvitation = await createInvitation.HandleAsync(
                new CreateFacilityInvitation(facilityId, FacilityRole.Owner),
                cancellationToken);
            memberInvitation = await createInvitation.HandleAsync(
                new CreateFacilityInvitation(facilityId, FacilityRole.Member),
                cancellationToken);

            var getInvitations = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>>>();
            var pending = await getInvitations.HandleAsync(new GetFacilityInvitations(facilityId), cancellationToken);
            Assert.Equal(2, pending.Count);
        }

        // 2. Unregistered caller accepts the Owner invitation, registering a new User and linking their identity.
        var bobIdentity = new ExternalIdentity("entra", "oid-bob", "bob@example.com");
        AcceptedInvitation accepted;
        await using (var scope = CreateAuthenticatedScope(bobIdentity, userId: null))
        {
            var accept = scope.ServiceProvider.GetRequiredService<ICommandHandler<AcceptInvitation, AcceptedInvitation>>();
            accepted = await accept.HandleAsync(new AcceptInvitation(ownerInvitation.Token, "Bob"), cancellationToken);

            Assert.Equal(facilityId, accepted.FacilityId);
            Assert.Equal(FacilityRole.Owner, accepted.Role);
        }

        // 3. Bob's external identity is now linked in the database and can query the facility.
        await using (var scope = CreateAuthenticatedScope(bobIdentity, accepted.UserId))
        {
            var lookup = scope.ServiceProvider.GetRequiredService<IUserIdentityLookup>();
            Assert.Equal(accepted.UserId, await lookup.FindUserIdAsync("entra", "oid-bob", cancellationToken));

            var getFacilities = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetFacilities, IReadOnlyList<FacilitySummary>>>();
            var facilities = await getFacilities.HandleAsync(new GetFacilities(), cancellationToken);
            Assert.Equal([new FacilitySummary(facilityId, "Summer House", 10, FacilityRole.Owner)], facilities);

            var getFacility = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetFacility, FacilityDetails>>();
            var details = await getFacility.HandleAsync(new GetFacility(facilityId), cancellationToken);
            Assert.Equal("Summer House", details.Name);
            Assert.Equal(10, details.Capacity);
            Assert.Equal(
                [
                    new FacilityMemberDetails(admin.Id, "Alice", FacilityRole.Administrator),
                    new FacilityMemberDetails(accepted.UserId, "Bob", FacilityRole.Owner),
                ],
                details.Members);

            // Non-administrators cannot list pending invitations.
            var getInvitations = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>>>();
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                getInvitations.HandleAsync(new GetFacilityInvitations(facilityId), cancellationToken));
        }

        // 4. Admin sees only the remaining unaccepted invitation and revokes it.
        await using (var scope = CreateAuthenticatedScope(adminIdentity, admin.Id))
        {
            var getInvitations = scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>>>();
            var pending = await getInvitations.HandleAsync(new GetFacilityInvitations(facilityId), cancellationToken);
            Assert.Equal(memberInvitation.Id, Assert.Single(pending).Id);

            var revoke = scope.ServiceProvider.GetRequiredService<ICommandHandler<RevokeFacilityInvitation, Unit>>();
            await revoke.HandleAsync(new RevokeFacilityInvitation(facilityId, memberInvitation.Id), cancellationToken);

            Assert.Empty(await getInvitations.HandleAsync(new GetFacilityInvitations(facilityId), cancellationToken));
        }
    }

    [Fact]
    public async Task Concurrent_invitation_acceptance_is_rejected_with_conflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = User.Register("Alice", "entra", "oid-admin");
        admin.GrantFacilityCreation();
        var bob = User.Register("Bob", "entra", "oid-bob");
        var charlie = User.Register("Charlie", "entra", "oid-charlie");
        var facility = Facility.Create(admin, "Summer House", 10);
        var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var (invitation, _) = facility.CreateInvitation(admin.Id, FacilityRole.Member, now);

        await using (var seed = _database.CreateContext())
        {
            seed.Set<User>().AddRange(admin, bob, charlie);
            seed.Set<Facility>().Add(facility);
            seed.Set<FacilityInvitation>().Add(invitation);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var firstScope = _services.CreateAsyncScope();
        await using var secondScope = _services.CreateAsyncScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstUow = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var secondUow = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Both scopes load the pending invitation and facility before either commits.
        var firstFacility = await firstContext.Set<Facility>()
            .Include(f => f.Members)
            .SingleAsync(f => f.Id == facility.Id, cancellationToken);
        var secondFacility = await secondContext.Set<Facility>()
            .Include(f => f.Members)
            .SingleAsync(f => f.Id == facility.Id, cancellationToken);
        var firstInvitation = await firstContext.Set<FacilityInvitation>()
            .SingleAsync(i => i.Id == invitation.Id, cancellationToken);
        var secondInvitation = await secondContext.Set<FacilityInvitation>()
            .SingleAsync(i => i.Id == invitation.Id, cancellationToken);

        await firstUow.ExecuteAsync(_ =>
        {
            firstInvitation.Accept(firstFacility, bob.Id, now.AddMinutes(1));
            return Task.FromResult(0);
        }, cancellationToken);

        await Assert.ThrowsAsync<ConflictException>(() => secondUow.ExecuteAsync(_ =>
        {
            secondInvitation.Accept(secondFacility, charlie.Id, now.AddMinutes(2));
            return Task.FromResult(0);
        }, cancellationToken));
    }

    [Fact]
    public async Task Duplicate_facility_member_key_maps_to_already_facility_member_rule_code()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var admin = User.Register("Alice", "entra", "oid-admin");
        admin.GrantFacilityCreation();
        var bob = User.Register("Bob", "entra", "oid-bob");
        var facility = Facility.Create(admin, "Summer House", 10);
        var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var (firstInvite, _) = facility.CreateInvitation(admin.Id, FacilityRole.Member, now);
        var (secondInvite, _) = facility.CreateInvitation(admin.Id, FacilityRole.Owner, now);

        await using (var seed = _database.CreateContext())
        {
            seed.Set<User>().AddRange(admin, bob);
            seed.Set<Facility>().Add(facility);
            seed.Set<FacilityInvitation>().AddRange(firstInvite, secondInvite);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var firstScope = _services.CreateAsyncScope();
        await using var secondScope = _services.CreateAsyncScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstUow = firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var secondUow = secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var firstFacility = await firstContext.Set<Facility>()
            .Include(f => f.Members)
            .SingleAsync(f => f.Id == facility.Id, cancellationToken);
        var secondFacility = await secondContext.Set<Facility>()
            .Include(f => f.Members)
            .SingleAsync(f => f.Id == facility.Id, cancellationToken);
        var loadedFirstInvite = await firstContext.Set<FacilityInvitation>()
            .SingleAsync(i => i.Id == firstInvite.Id, cancellationToken);
        var loadedSecondInvite = await secondContext.Set<FacilityInvitation>()
            .SingleAsync(i => i.Id == secondInvite.Id, cancellationToken);

        await firstUow.ExecuteAsync(_ =>
        {
            loadedFirstInvite.Accept(firstFacility, bob.Id, now.AddMinutes(1));
            return Task.FromResult(0);
        }, cancellationToken);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => secondUow.ExecuteAsync(_ =>
        {
            loadedSecondInvite.Accept(secondFacility, bob.Id, now.AddMinutes(2));
            return Task.FromResult(0);
        }, cancellationToken));

        Assert.Equal("PK_FacilityMembers", conflict.IndexName);
        Assert.Equal(RuleCodes.AlreadyFacilityMember, conflict.RuleCode);
    }

    private AsyncServiceScope CreateAuthenticatedScope(ExternalIdentity identity, UserId? userId)
    {
        var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().Set(identity, userId);
        return scope;
    }

    private static ServiceProvider BuildServices(string connectionString)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        var user = connection.UserID;
        var password = connection.Password;
        connection.Remove("User ID");
        connection.Remove("Password");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connection.ConnectionString,
                ["Database:Authentication"] = "Password",
                ["Database:User"] = user,
                ["Database:Password"] = password,
            })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddApplication()
            .AddInfrastructure()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
