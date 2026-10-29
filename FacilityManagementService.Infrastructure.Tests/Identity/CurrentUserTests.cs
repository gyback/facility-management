using FacilityManagementService.Application.Users;
using FacilityManagementService.Domain.Users;
using FacilityManagementService.Infrastructure.Identity;

namespace FacilityManagementService.Infrastructure.Tests.Identity;

public sealed class CurrentUserTests
{
    private static readonly ExternalIdentity Identity = new("entra", "subject-1", "Alice");

    [Fact]
    public void Before_an_identity_is_set_reading_the_id_or_identity_fails()
    {
        var currentUser = new CurrentUser();

        Assert.False(currentUser.IsRegistered);
        Assert.Throws<InvalidOperationException>(() => _ = currentUser.Id);
        Assert.Throws<InvalidOperationException>(() => _ = currentUser.Identity);
    }

    [Fact]
    public void An_unlinked_identity_exposes_the_identity_and_throws_when_its_user_id_is_read()
    {
        var currentUser = new CurrentUser();
        currentUser.Set(Identity, userId: null);

        Assert.False(currentUser.IsRegistered);
        Assert.Equal(Identity, currentUser.Identity);
        Assert.Throws<UnregisteredUserException>(() => _ = currentUser.Id);
    }

    [Fact]
    public void A_linked_identity_exposes_both_the_identity_and_the_user_id()
    {
        var currentUser = new CurrentUser();
        var userId = UserId.New();
        currentUser.Set(Identity, userId);

        Assert.True(currentUser.IsRegistered);
        Assert.Equal(Identity, currentUser.Identity);
        Assert.Equal(userId, currentUser.Id);
    }
}

