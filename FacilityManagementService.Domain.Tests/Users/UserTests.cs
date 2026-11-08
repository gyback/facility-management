using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Domain.Tests.Users;

public sealed class UserTests
{
    [Fact]
    public void Register_creates_a_user_with_a_trimmed_display_name_and_linked_identity()
    {
        var user = User.Register("  Alice  ", "entra", "subject-1");

        Assert.NotEqual(Guid.Empty, user.Id.Value);
        Assert.Equal("Alice", user.DisplayName);
        Assert.False(user.CanCreateFacilities);
        var identity = Assert.Single(user.ExternalIdentities);
        Assert.Equal(user.Id, identity.UserId);
        Assert.Equal("entra", identity.Provider);
        Assert.Equal("subject-1", identity.Subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_a_missing_or_blank_display_name(string? displayName) =>
        Assert.ThrowsAny<ArgumentException>(() => User.Register(displayName!, "entra", "subject-1"));

    [Fact]
    public void Register_rejects_a_display_name_longer_than_the_limit() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            User.Register(new string('a', User.MaxDisplayNameLength + 1), "entra", "subject-1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_a_missing_or_blank_provider(string? provider) =>
        Assert.ThrowsAny<ArgumentException>(() => User.Register("Alice", provider!, "subject-1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_a_missing_or_blank_subject(string? subject) =>
        Assert.ThrowsAny<ArgumentException>(() => User.Register("Alice", "entra", subject!));

    [Fact]
    public void Rename_updates_and_trims_the_display_name()
    {
        var user = User.Register("Alice", "entra", "subject-1");

        user.Rename("  Alice Smith  ");

        Assert.Equal("Alice Smith", user.DisplayName);
    }

    [Fact]
    public void Facility_creation_capability_is_granted_revoked_and_enforced()
    {
        var user = User.Register("Alice", "entra", "subject-1");

        Assert.Throws<ForbiddenException>(user.EnsureCanCreateFacilities);

        user.GrantFacilityCreation();
        Assert.True(user.CanCreateFacilities);
        user.EnsureCanCreateFacilities();

        user.RevokeFacilityCreation();
        Assert.False(user.CanCreateFacilities);
        Assert.Throws<ForbiddenException>(user.EnsureCanCreateFacilities);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("alice@example.com", null)]
    [InlineData("  Alice  ", "Alice")]
    public void TryGetNonEmailDisplayName_accepts_only_non_email_names(string? input, string? expected) =>
        Assert.Equal(expected, User.TryGetNonEmailDisplayName(input));
}
