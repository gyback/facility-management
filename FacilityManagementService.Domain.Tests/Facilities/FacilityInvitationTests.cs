using System.Buffers.Text;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Domain.Tests.Facilities;

public sealed class FacilityInvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creating_an_invitation_produces_a_256_bit_token_and_stores_only_its_sha256_hash()
    {
        var (creator, facility) = CreateFacility();

        var (invitation, token) = facility.CreateInvitation(creator.Id, FacilityRole.Owner, Now);

        Assert.Equal(FacilityInvitation.TokenByteLength, Base64Url.DecodeFromChars(token).Length);
        Assert.NotEqual(token, invitation.TokenHash);
        Assert.Equal(FacilityInvitation.TokenHashLength, invitation.TokenHash.Length);
        Assert.Equal(FacilityInvitation.HashToken(token), invitation.TokenHash);
        Assert.Equal(facility.Id, invitation.FacilityId);
        Assert.Equal(FacilityRole.Owner, invitation.Role);
        Assert.Equal(creator.Id, invitation.CreatedByUserId);
        Assert.Equal(Now, invitation.CreatedAt);
        Assert.Equal(Now.AddDays(14), invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedAt);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.True(invitation.IsPending(Now));
    }

    [Fact]
    public void Accepting_a_pending_invitation_adds_the_member_and_spends_the_invitation()
    {
        var (creator, facility) = CreateFacility();
        var (invitation, _) = facility.CreateInvitation(creator.Id, FacilityRole.Owner, Now);
        var inviteeId = UserId.New();
        var acceptedAt = Now.AddDays(1);

        invitation.Accept(facility, inviteeId, acceptedAt);

        Assert.True(facility.IsMember(inviteeId));
        Assert.Equal(FacilityRole.Owner, facility.FindRole(inviteeId));
        Assert.Equal(acceptedAt, invitation.AcceptedAt);
        Assert.Equal(inviteeId, invitation.AcceptedByUserId);
        Assert.False(invitation.IsPending(acceptedAt));
    }

    [Fact]
    public void An_expired_invitation_cannot_be_accepted()
    {
        var (creator, facility) = CreateFacility();
        var (invitation, _) = facility.CreateInvitation(creator.Id, FacilityRole.Member, Now);
        var inviteeId = UserId.New();

        var exception = Assert.Throws<InvitationExpiredException>(() =>
            invitation.Accept(facility, inviteeId, invitation.ExpiresAt));

        Assert.Equal(RuleCodes.InvitationExpired, exception.Code);
        Assert.False(facility.IsMember(inviteeId));
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void An_already_accepted_invitation_cannot_be_accepted_again()
    {
        var (creator, facility) = CreateFacility();
        var (invitation, _) = facility.CreateInvitation(creator.Id, FacilityRole.Member, Now);
        invitation.Accept(facility, UserId.New(), Now.AddMinutes(5));

        var secondInviteeId = UserId.New();
        var exception = Assert.Throws<InvitationExpiredException>(() =>
            invitation.Accept(facility, secondInviteeId, Now.AddMinutes(10)));

        Assert.Equal(RuleCodes.InvitationExpired, exception.Code);
        Assert.False(facility.IsMember(secondInviteeId));
    }

    [Fact]
    public void Accepting_an_invitation_for_an_existing_member_fails_without_spending_the_invitation_or_changing_their_role()
    {
        var (creator, facility) = CreateFacility();
        var (invitation, _) = facility.CreateInvitation(creator.Id, FacilityRole.Member, Now);

        var exception = Assert.Throws<AlreadyFacilityMemberException>(() =>
            invitation.Accept(facility, creator.Id, Now.AddMinutes(5)));

        Assert.Equal(RuleCodes.AlreadyFacilityMember, exception.Code);
        Assert.Equal(FacilityRole.Administrator, facility.FindRole(creator.Id));
        Assert.Null(invitation.AcceptedAt);
        Assert.True(invitation.IsPending(Now.AddMinutes(5)));
    }

    private static (User Creator, Facility Facility) CreateFacility()
    {
        var creator = User.Register("Alice", "entra", "subject-1");
        creator.GrantFacilityCreation();
        var facility = Facility.Create(creator, "Lake Cabin", 12);
        return (creator, facility);
    }
}
