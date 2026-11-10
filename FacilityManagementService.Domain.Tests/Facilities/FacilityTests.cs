using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Domain.Tests.Facilities;

public sealed class FacilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_makes_the_creator_the_initial_administrator()
    {
        var creator = CreateAuthorizedCreator();

        var facility = Facility.Create(creator, "  Lake Cabin  ", 12);

        Assert.NotEqual(Guid.Empty, facility.Id.Value);
        Assert.Equal("Lake Cabin", facility.Name);
        Assert.Equal(12, facility.Capacity);
        Assert.True(facility.IsMember(creator.Id));
        Assert.Equal(FacilityRole.Administrator, facility.FindRole(creator.Id));
        var member = Assert.Single(facility.Members);
        Assert.Equal(facility.Id, member.FacilityId);
        Assert.Equal(creator.Id, member.UserId);
        Assert.Equal(FacilityRole.Administrator, member.Role);
    }

    [Fact]
    public void Create_refuses_a_user_without_facility_creation_capability()
    {
        var user = User.Register("Alice", "entra", "subject-1");

        Assert.Throws<ForbiddenException>(() => Facility.Create(user, "Lake Cabin", 12));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_missing_or_blank_name(string? name)
    {
        var creator = CreateAuthorizedCreator();

        Assert.ThrowsAny<ArgumentException>(() => Facility.Create(creator, name!, 12));
    }

    [Fact]
    public void Create_rejects_a_name_longer_than_the_limit()
    {
        var creator = CreateAuthorizedCreator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Facility.Create(creator, new string('a', Facility.MaxNameLength + 1), 12));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Facility.MaxCapacity + 1)]
    public void Create_rejects_an_out_of_range_capacity(int capacity)
    {
        var creator = CreateAuthorizedCreator();

        Assert.Throws<ArgumentOutOfRangeException>(() => Facility.Create(creator, "Lake Cabin", capacity));
    }

    [Fact]
    public void An_administrator_can_rename_and_set_the_capacity_of_a_facility()
    {
        var creator = CreateAuthorizedCreator();
        var facility = Facility.Create(creator, "Lake Cabin", 12);

        facility.Rename(creator.Id, "  Mountain Cabin  ");
        facility.SetCapacity(creator.Id, 8);

        Assert.Equal("Mountain Cabin", facility.Name);
        Assert.Equal(8, facility.Capacity);
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public void Non_administrators_cannot_manage_a_facility_or_its_members(FacilityRole role)
    {
        var creator = CreateAuthorizedCreator();
        var facility = Facility.Create(creator, "Lake Cabin", 12);
        var otherUserId = AddMemberViaInvitation(facility, creator.Id, role);

        Assert.Throws<ForbiddenException>(() => facility.Rename(otherUserId, "Other"));
        Assert.Throws<ForbiddenException>(() => facility.SetCapacity(otherUserId, 10));
        Assert.Throws<ForbiddenException>(() => facility.CreateInvitation(otherUserId, FacilityRole.Member, Now));
        Assert.Throws<ForbiddenException>(() => facility.SetMemberRole(otherUserId, creator.Id, FacilityRole.Owner));
        Assert.Throws<ForbiddenException>(() => facility.RemoveMember(otherUserId, creator.Id));
    }

    [Fact]
    public void An_administrator_can_change_a_members_role_and_remove_them()
    {
        var creator = CreateAuthorizedCreator();
        var facility = Facility.Create(creator, "Lake Cabin", 12);
        var memberId = AddMemberViaInvitation(facility, creator.Id, FacilityRole.Member);

        facility.SetMemberRole(creator.Id, memberId, FacilityRole.Owner);
        Assert.Equal(FacilityRole.Owner, facility.FindRole(memberId));

        facility.RemoveMember(creator.Id, memberId);
        Assert.False(facility.IsMember(memberId));
        Assert.Null(facility.FindRole(memberId));
    }

    [Theory]
    [InlineData(FacilityRole.Member)]
    [InlineData(FacilityRole.Owner)]
    public void The_last_administrator_cannot_be_demoted(FacilityRole newRole)
    {
        var creator = CreateAuthorizedCreator();
        var facility = Facility.Create(creator, "Lake Cabin", 12);

        var exception = Assert.Throws<LastFacilityAdministratorException>(() =>
            facility.SetMemberRole(creator.Id, creator.Id, newRole));

        Assert.Equal(RuleCodes.LastFacilityAdministrator, exception.Code);
        Assert.Equal(FacilityRole.Administrator, facility.FindRole(creator.Id));
    }

    [Fact]
    public void The_last_administrator_cannot_be_removed()
    {
        var creator = CreateAuthorizedCreator();
        var facility = Facility.Create(creator, "Lake Cabin", 12);

        var exception = Assert.Throws<LastFacilityAdministratorException>(() =>
            facility.RemoveMember(creator.Id, creator.Id));

        Assert.Equal(RuleCodes.LastFacilityAdministrator, exception.Code);
        Assert.True(facility.IsMember(creator.Id));
    }

    [Fact]
    public void An_administrator_can_be_demoted_or_removed_when_another_administrator_remains()
    {
        var creator = CreateAuthorizedCreator();
        var facility = Facility.Create(creator, "Lake Cabin", 12);
        var secondAdminId = AddMemberViaInvitation(facility, creator.Id, FacilityRole.Administrator);

        facility.SetMemberRole(secondAdminId, creator.Id, FacilityRole.Owner);
        Assert.Equal(FacilityRole.Owner, facility.FindRole(creator.Id));

        var thirdAdminId = AddMemberViaInvitation(facility, secondAdminId, FacilityRole.Administrator);
        facility.RemoveMember(thirdAdminId, secondAdminId);
        Assert.False(facility.IsMember(secondAdminId));
    }

    private static User CreateAuthorizedCreator()
    {
        var user = User.Register("Alice", "entra", "subject-1");
        user.GrantFacilityCreation();
        return user;
    }

    private static UserId AddMemberViaInvitation(Facility facility, UserId adminId, FacilityRole role)
    {
        var (invitation, _) = facility.CreateInvitation(adminId, role, Now);
        var userId = UserId.New();
        invitation.Accept(facility, userId, Now.AddMinutes(1));
        return userId;
    }
}
