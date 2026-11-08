using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Tests.Facilities;

public sealed class FacilityInvitationCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeFacilityRepository _facilities = new();
    private readonly FakeFacilityInvitationRepository _invitations = new();
    private readonly ManualTimeProvider _timeProvider = new(Now);

    [Fact]
    public async Task CreateFacilityInvitation_creates_and_stores_an_invitation()
    {
        var (admin, facility) = CreateSeededFacility();
        var handler = new CreateFacilityInvitationHandler(
            new FakeCurrentUser(admin.Id),
            _facilities,
            _invitations,
            _timeProvider);

        var created = await handler.HandleAsync(
            new CreateFacilityInvitation(facility.Id, FacilityRole.Owner),
            TestContext.Current.CancellationToken);

        var stored = Assert.Single(_invitations.All);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(FacilityInvitation.HashToken(created.Token), stored.TokenHash);
        Assert.Equal(Now.AddDays(14), created.ExpiresAt);
    }

    [Fact]
    public async Task CreateFacilityInvitation_throws_not_found_for_a_non_member_and_forbidden_for_a_non_administrator()
    {
        var (admin, facility) = CreateSeededFacility();
        var outsider = RegisterUser("Bob", "sub-bob");
        var member = AddMember(facility, admin.Id, FacilityRole.Member);

        var outsiderHandler = new CreateFacilityInvitationHandler(
            new FakeCurrentUser(outsider.Id),
            _facilities,
            _invitations,
            _timeProvider);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            outsiderHandler.HandleAsync(
                new CreateFacilityInvitation(facility.Id, FacilityRole.Member),
                TestContext.Current.CancellationToken));

        var memberHandler = new CreateFacilityInvitationHandler(
            new FakeCurrentUser(member.Id),
            _facilities,
            _invitations,
            _timeProvider);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            memberHandler.HandleAsync(
                new CreateFacilityInvitation(facility.Id, FacilityRole.Member),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeFacilityInvitation_removes_a_pending_invitation()
    {
        var (admin, facility) = CreateSeededFacility();
        var (invitation, _) = facility.CreateInvitation(admin.Id, FacilityRole.Member, Now);
        _invitations.Add(invitation);

        var handler = new RevokeFacilityInvitationHandler(
            new FakeCurrentUser(admin.Id),
            _facilities,
            _invitations,
            _timeProvider);

        await handler.HandleAsync(
            new RevokeFacilityInvitation(facility.Id, invitation.Id),
            TestContext.Current.CancellationToken);

        Assert.Empty(_invitations.All);
    }

    [Fact]
    public async Task RevokeFacilityInvitation_throws_not_found_for_non_member_or_missing_or_expired_invitation()
    {
        var (admin, facility) = CreateSeededFacility();
        var (_, otherFacility) = CreateSeededFacility();
        var (otherInvitation, _) = otherFacility.CreateInvitation(
            otherFacility.Members.Single().UserId,
            FacilityRole.Member,
            Now);
        _invitations.Add(otherInvitation);

        var outsider = RegisterUser("Bob", "sub-bob");
        var outsiderHandler = new RevokeFacilityInvitationHandler(
            new FakeCurrentUser(outsider.Id),
            _facilities,
            _invitations,
            _timeProvider);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            outsiderHandler.HandleAsync(
                new RevokeFacilityInvitation(facility.Id, otherInvitation.Id),
                TestContext.Current.CancellationToken));

        var adminHandler = new RevokeFacilityInvitationHandler(
            new FakeCurrentUser(admin.Id),
            _facilities,
            _invitations,
            _timeProvider);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            adminHandler.HandleAsync(
                new RevokeFacilityInvitation(facility.Id, otherInvitation.Id),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public async Task RevokeFacilityInvitation_throws_forbidden_for_a_non_administrator_member(FacilityRole role)
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, role);
        var (invitation, _) = facility.CreateInvitation(admin.Id, FacilityRole.Member, Now);
        _invitations.Add(invitation);

        var handler = new RevokeFacilityInvitationHandler(
            new FakeCurrentUser(member.Id),
            _facilities,
            _invitations,
            _timeProvider);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(
                new RevokeFacilityInvitation(facility.Id, invitation.Id),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcceptInvitation_registers_a_new_user_with_the_tokens_display_name_and_adds_membership()
    {
        var (admin, facility) = CreateSeededFacility();
        var (invitation, token) = facility.CreateInvitation(admin.Id, FacilityRole.Owner, Now);
        _invitations.Add(invitation);

        var currentUser = new FakeCurrentUser(
            userId: null,
            provider: "entra",
            subject: "new-subject",
            displayName: "Bob");
        var handler = new AcceptInvitationHandler(
            currentUser,
            _invitations,
            _facilities,
            _users,
            _timeProvider);

        var outcome = await handler.HandleAsync(
            new AcceptInvitation(token),
            TestContext.Current.CancellationToken);

        Assert.Equal(facility.Id, outcome.FacilityId);
        Assert.Equal(FacilityRole.Owner, outcome.Role);
        Assert.Equal(FacilityRole.Owner, facility.FindRole(outcome.UserId));

        var createdUser = await _users.GetAsync(outcome.UserId, TestContext.Current.CancellationToken);
        Assert.NotNull(createdUser);
        Assert.Equal("Bob", createdUser.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bob@example.com")]
    public async Task AcceptInvitation_requires_a_display_name_in_the_command_when_the_token_has_no_non_email_name(
        string? tokenName)
    {
        var (admin, facility) = CreateSeededFacility();
        var (invitation, token) = facility.CreateInvitation(admin.Id, FacilityRole.Member, Now);
        _invitations.Add(invitation);

        var currentUser = new FakeCurrentUser(
            userId: null,
            provider: "entra",
            subject: "new-subject",
            displayName: tokenName);
        var handler = new AcceptInvitationHandler(
            currentUser,
            _invitations,
            _facilities,
            _users,
            _timeProvider);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new AcceptInvitation(token), TestContext.Current.CancellationToken));

        Assert.Equal(
            [ValidationError.Required(nameof(AcceptInvitation.DisplayName))],
            exception.Errors);

        var outcome = await handler.HandleAsync(
            new AcceptInvitation(token, "  Bob  "),
            TestContext.Current.CancellationToken);

        var createdUser = await _users.GetAsync(outcome.UserId, TestContext.Current.CancellationToken);
        Assert.NotNull(createdUser);
        Assert.Equal("Bob", createdUser.DisplayName);
    }

    [Fact]
    public async Task AcceptInvitation_adds_an_existing_user_to_another_facility_without_creating_a_second_user()
    {
        var (admin, facility) = CreateSeededFacility();
        var existingUser = RegisterUser("Bob", "sub-bob");
        var (invitation, token) = facility.CreateInvitation(admin.Id, FacilityRole.Member, Now);
        _invitations.Add(invitation);

        var currentUser = new FakeCurrentUser(
            userId: existingUser.Id,
            provider: "entra",
            subject: "sub-bob",
            displayName: null);
        var handler = new AcceptInvitationHandler(
            currentUser,
            _invitations,
            _facilities,
            _users,
            _timeProvider);

        var outcome = await handler.HandleAsync(
            new AcceptInvitation(token),
            TestContext.Current.CancellationToken);

        Assert.Equal(existingUser.Id, outcome.UserId);
        Assert.Equal(FacilityRole.Member, facility.FindRole(existingUser.Id));
        Assert.Equal(2, _users.All.Count);
    }

    [Fact]
    public async Task AcceptInvitation_refuses_an_existing_member_with_already_facility_member()
    {
        var (admin, facility) = CreateSeededFacility();
        var (invitation, token) = facility.CreateInvitation(admin.Id, FacilityRole.Member, Now);
        _invitations.Add(invitation);

        var currentUser = new FakeCurrentUser(
            userId: admin.Id,
            provider: "entra",
            subject: admin.ExternalIdentities.Single().Subject,
            displayName: "Alice");
        var handler = new AcceptInvitationHandler(
            currentUser,
            _invitations,
            _facilities,
            _users,
            _timeProvider);

        await Assert.ThrowsAsync<AlreadyFacilityMemberException>(() =>
            handler.HandleAsync(new AcceptInvitation(token), TestContext.Current.CancellationToken));

        Assert.Equal(FacilityRole.Administrator, facility.FindRole(admin.Id));
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void AcceptInvitationValidator_checks_token_and_optional_display_name()
    {
        var validator = new AcceptInvitationValidator();

        Assert.Equal(
            [
                ValidationError.Required(nameof(AcceptInvitation.Token)),
                ValidationError.Required(nameof(AcceptInvitation.DisplayName)),
            ],
            validator.Validate(new AcceptInvitation(" ", "   ")));

        ValidationAssertions.Equal(
            [ValidationError.TooLong(nameof(AcceptInvitation.DisplayName), User.MaxDisplayNameLength)],
            validator.Validate(new AcceptInvitation("token", new string('a', User.MaxDisplayNameLength + 1))));

        Assert.Empty(validator.Validate(new AcceptInvitation("token", null)));
        Assert.Empty(validator.Validate(new AcceptInvitation("token", "Bob")));
    }

    private User RegisterUser(string displayName, string subject, bool canCreateFacilities = false)
    {
        var user = User.Register(displayName, "entra", subject);
        if (canCreateFacilities)
        {
            user.GrantFacilityCreation();
        }

        _users.Add(user);
        return user;
    }

    private (User Admin, Facility Facility) CreateSeededFacility()
    {
        var admin = RegisterUser("Alice", $"sub-{Guid.NewGuid()}", canCreateFacilities: true);
        var facility = Facility.Create(admin, "Lake Cabin", 12);
        _facilities.Add(facility);
        return (admin, facility);
    }

    private User AddMember(Facility facility, UserId adminId, FacilityRole role)
    {
        var user = RegisterUser("Member", $"sub-{Guid.NewGuid()}");
        var (invitation, _) = facility.CreateInvitation(adminId, role, Now);
        invitation.Accept(facility, user.Id, Now.AddMinutes(1));
        return user;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
