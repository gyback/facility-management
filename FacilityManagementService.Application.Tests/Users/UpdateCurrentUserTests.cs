using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Tests.Users;

public sealed class UpdateCurrentUserTests
{
    private readonly FakeUserRepository _users = new();

    [Fact]
    public async Task Updates_the_current_users_display_name()
    {
        var user = User.Register("Alice", "entra", "subject-1");
        _users.Add(user);
        var currentUser = new FakeCurrentUser(user.Id);
        var handler = new UpdateCurrentUserHandler(currentUser, _users);

        await handler.HandleAsync(new UpdateCurrentUser("  Alice Smith  "), TestContext.Current.CancellationToken);

        Assert.Equal("Alice Smith", user.DisplayName);
    }

    [Fact]
    public async Task Throws_not_found_when_the_current_user_does_not_exist()
    {
        var currentUser = new FakeCurrentUser(UserId.New());
        var handler = new UpdateCurrentUserHandler(currentUser, _users);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new UpdateCurrentUser("Alice"), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_rejects_a_missing_or_blank_display_name(string? displayName)
    {
        var validator = new UpdateCurrentUserValidator();

        var errors = validator.Validate(new UpdateCurrentUser(displayName!));

        Assert.Equal([ValidationError.Required(nameof(UpdateCurrentUser.DisplayName))], errors);
    }

    [Fact]
    public void Validator_rejects_a_display_name_longer_than_the_limit()
    {
        var validator = new UpdateCurrentUserValidator();
        var longName = new string('a', User.MaxDisplayNameLength + 1);

        var errors = validator.Validate(new UpdateCurrentUser(longName));

        ValidationAssertions.Equal(
            [ValidationError.TooLong(nameof(UpdateCurrentUser.DisplayName), User.MaxDisplayNameLength)],
            errors);
    }

    [Fact]
    public void Validator_accepts_a_valid_display_name()
    {
        var validator = new UpdateCurrentUserValidator();

        Assert.Empty(validator.Validate(new UpdateCurrentUser("Alice")));
    }
}
