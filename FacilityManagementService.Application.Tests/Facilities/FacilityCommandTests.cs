using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Tests.Facilities;

public sealed class FacilityCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeFacilityRepository _facilities = new();

    [Fact]
    public async Task CreateFacility_creates_a_facility_with_the_caller_as_administrator()
    {
        var creator = RegisterUser("Alice", canCreateFacilities: true);
        var handler = new CreateFacilityHandler(new FakeCurrentUser(creator.Id), _users, _facilities);

        var facilityId = await handler.HandleAsync(
            new CreateFacility("  Lake Cabin  ", 12),
            TestContext.Current.CancellationToken);

        var facility = await _facilities.GetAsync(facilityId, TestContext.Current.CancellationToken);
        Assert.NotNull(facility);
        Assert.Equal("Lake Cabin", facility.Name);
        Assert.Equal(12, facility.Capacity);
        Assert.Equal(FacilityRole.Administrator, facility.FindRole(creator.Id));
    }

    [Fact]
    public async Task CreateFacility_refuses_a_user_without_facility_creation_capability()
    {
        var user = RegisterUser("Alice", canCreateFacilities: false);
        var handler = new CreateFacilityHandler(new FakeCurrentUser(user.Id), _users, _facilities);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new CreateFacility("Lake Cabin", 12), TestContext.Current.CancellationToken));

        Assert.Empty(_facilities.All);
    }

    [Fact]
    public void CreateFacilityValidator_checks_name_and_capacity()
    {
        var validator = new CreateFacilityValidator();

        ValidationAssertions.Equal(
            [
                ValidationError.Required(nameof(CreateFacility.Name)),
                ValidationError.OutOfRange(nameof(CreateFacility.Capacity), Facility.MinCapacity, Facility.MaxCapacity),
            ],
            validator.Validate(new CreateFacility("   ", 0)));

        ValidationAssertions.Equal(
            [ValidationError.TooLong(nameof(CreateFacility.Name), Facility.MaxNameLength)],
            validator.Validate(new CreateFacility(new string('a', Facility.MaxNameLength + 1), 10)));

        Assert.Empty(validator.Validate(new CreateFacility("Lake Cabin", 10)));
    }

    [Fact]
    public async Task RenameFacility_renames_when_the_caller_is_an_administrator()
    {
        var (admin, facility) = CreateSeededFacility();
        var handler = new RenameFacilityHandler(new FakeCurrentUser(admin.Id), _facilities);

        await handler.HandleAsync(
            new RenameFacility(facility.Id, "  Forest Cabin  "),
            TestContext.Current.CancellationToken);

        Assert.Equal("Forest Cabin", facility.Name);
    }

    [Fact]
    public async Task RenameFacility_throws_not_found_for_a_missing_facility_or_non_member()
    {
        var (_, facility) = CreateSeededFacility();
        var outsider = RegisterUser("Bob");
        var handler = new RenameFacilityHandler(new FakeCurrentUser(outsider.Id), _facilities);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new RenameFacility(FacilityId.New(), "New"), TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new RenameFacility(facility.Id, "New"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public async Task RenameFacility_throws_forbidden_for_a_non_administrator_member(FacilityRole role)
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, role);
        var handler = new RenameFacilityHandler(new FakeCurrentUser(member.Id), _facilities);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new RenameFacility(facility.Id, "New"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RenameFacilityValidator_checks_name()
    {
        var validator = new RenameFacilityValidator();

        ValidationAssertions.Equal(
            [ValidationError.Required(nameof(RenameFacility.Name))],
            validator.Validate(new RenameFacility(FacilityId.New(), " ")));

        ValidationAssertions.Equal(
            [ValidationError.TooLong(nameof(RenameFacility.Name), Facility.MaxNameLength)],
            validator.Validate(new RenameFacility(FacilityId.New(), new string('a', Facility.MaxNameLength + 1))));

        Assert.Empty(validator.Validate(new RenameFacility(FacilityId.New(), "Forest Cabin")));
    }

    [Fact]
    public async Task SetFacilityCapacity_updates_capacity_when_the_caller_is_an_administrator()
    {
        var (admin, facility) = CreateSeededFacility();
        var handler = new SetFacilityCapacityHandler(new FakeCurrentUser(admin.Id), _facilities);

        await handler.HandleAsync(
            new SetFacilityCapacity(facility.Id, 20),
            TestContext.Current.CancellationToken);

        Assert.Equal(20, facility.Capacity);
    }

    [Fact]
    public async Task SetFacilityCapacity_throws_not_found_for_a_missing_facility_or_non_member()
    {
        var (_, facility) = CreateSeededFacility();
        var outsider = RegisterUser("Bob");
        var handler = new SetFacilityCapacityHandler(new FakeCurrentUser(outsider.Id), _facilities);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new SetFacilityCapacity(FacilityId.New(), 8), TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new SetFacilityCapacity(facility.Id, 8), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public async Task SetFacilityCapacity_throws_forbidden_for_a_non_administrator_member(FacilityRole role)
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, role);
        var handler = new SetFacilityCapacityHandler(new FakeCurrentUser(member.Id), _facilities);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new SetFacilityCapacity(facility.Id, 8), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SetFacilityCapacityValidator_checks_capacity()
    {
        var validator = new SetFacilityCapacityValidator();

        ValidationAssertions.Equal(
            [ValidationError.OutOfRange(nameof(SetFacilityCapacity.Capacity), Facility.MinCapacity, Facility.MaxCapacity)],
            validator.Validate(new SetFacilityCapacity(FacilityId.New(), 0)));

        Assert.Empty(validator.Validate(new SetFacilityCapacity(FacilityId.New(), 8)));
    }

    [Fact]
    public async Task SetFacilityMemberRole_updates_the_members_role()
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, FacilityRole.Member);
        var handler = new SetFacilityMemberRoleHandler(new FakeCurrentUser(admin.Id), _facilities);

        await handler.HandleAsync(
            new SetFacilityMemberRole(facility.Id, member.Id, FacilityRole.Owner),
            TestContext.Current.CancellationToken);

        Assert.Equal(FacilityRole.Owner, facility.FindRole(member.Id));
    }

    [Fact]
    public async Task SetFacilityMemberRole_throws_not_found_for_a_non_member_caller_or_missing_target_member()
    {
        var (admin, facility) = CreateSeededFacility();
        var outsider = RegisterUser("Bob");

        var outsiderHandler = new SetFacilityMemberRoleHandler(new FakeCurrentUser(outsider.Id), _facilities);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            outsiderHandler.HandleAsync(
                new SetFacilityMemberRole(facility.Id, admin.Id, FacilityRole.Owner),
                TestContext.Current.CancellationToken));

        var adminHandler = new SetFacilityMemberRoleHandler(new FakeCurrentUser(admin.Id), _facilities);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            adminHandler.HandleAsync(
                new SetFacilityMemberRole(facility.Id, outsider.Id, FacilityRole.Owner),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public async Task SetFacilityMemberRole_throws_forbidden_for_a_non_administrator_member(FacilityRole role)
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, role);
        var handler = new SetFacilityMemberRoleHandler(new FakeCurrentUser(member.Id), _facilities);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(
                new SetFacilityMemberRole(facility.Id, admin.Id, FacilityRole.Member),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetFacilityMemberRole_refuses_demoting_the_last_administrator()
    {
        var (admin, facility) = CreateSeededFacility();
        var handler = new SetFacilityMemberRoleHandler(new FakeCurrentUser(admin.Id), _facilities);

        await Assert.ThrowsAsync<LastFacilityAdministratorException>(() =>
            handler.HandleAsync(
                new SetFacilityMemberRole(facility.Id, admin.Id, FacilityRole.Owner),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveFacilityMember_removes_a_member()
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, FacilityRole.Member);
        var handler = new RemoveFacilityMemberHandler(new FakeCurrentUser(admin.Id), _facilities);

        await handler.HandleAsync(
            new RemoveFacilityMember(facility.Id, member.Id),
            TestContext.Current.CancellationToken);

        Assert.False(facility.IsMember(member.Id));
    }

    [Fact]
    public async Task RemoveFacilityMember_throws_not_found_for_a_non_member_caller_or_missing_target_member()
    {
        var (admin, facility) = CreateSeededFacility();
        var outsider = RegisterUser("Bob");

        var outsiderHandler = new RemoveFacilityMemberHandler(new FakeCurrentUser(outsider.Id), _facilities);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            outsiderHandler.HandleAsync(
                new RemoveFacilityMember(facility.Id, admin.Id),
                TestContext.Current.CancellationToken));

        var adminHandler = new RemoveFacilityMemberHandler(new FakeCurrentUser(admin.Id), _facilities);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            adminHandler.HandleAsync(
                new RemoveFacilityMember(facility.Id, outsider.Id),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public async Task RemoveFacilityMember_throws_forbidden_for_a_non_administrator_member(FacilityRole role)
    {
        var (admin, facility) = CreateSeededFacility();
        var member = AddMember(facility, admin.Id, role);
        var handler = new RemoveFacilityMemberHandler(new FakeCurrentUser(member.Id), _facilities);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(
                new RemoveFacilityMember(facility.Id, admin.Id),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveFacilityMember_refuses_removing_the_last_administrator()
    {
        var (admin, facility) = CreateSeededFacility();
        var handler = new RemoveFacilityMemberHandler(new FakeCurrentUser(admin.Id), _facilities);

        await Assert.ThrowsAsync<LastFacilityAdministratorException>(() =>
            handler.HandleAsync(
                new RemoveFacilityMember(facility.Id, admin.Id),
                TestContext.Current.CancellationToken));
    }

    private User RegisterUser(string displayName, bool canCreateFacilities = false)
    {
        var user = User.Register(displayName, "entra", $"sub-{Guid.NewGuid()}");
        if (canCreateFacilities)
        {
            user.GrantFacilityCreation();
        }

        _users.Add(user);
        return user;
    }

    private (User Admin, Facility Facility) CreateSeededFacility()
    {
        var admin = RegisterUser("Alice", canCreateFacilities: true);
        var facility = Facility.Create(admin, "Lake Cabin", 12);
        _facilities.Add(facility);
        return (admin, facility);
    }

    private User AddMember(Facility facility, UserId adminId, FacilityRole role)
    {
        var user = RegisterUser("Member");
        var (invitation, _) = facility.CreateInvitation(adminId, role, Now);
        invitation.Accept(facility, user.Id, Now.AddMinutes(1));
        return user;
    }
}
