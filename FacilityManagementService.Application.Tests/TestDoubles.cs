using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Users;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Tests;

internal sealed class FakeCurrentUser : ICurrentUser
{
    private UserId? _userId;

    public FakeCurrentUser(
        UserId? userId = null,
        string provider = "entra",
        string subject = "subject-1",
        string? displayName = "Alice")
    {
        _userId = userId;
        Identity = new ExternalIdentity(provider, subject, displayName);
    }

    public UserId Id => _userId ?? throw new UnregisteredUserException();

    public ExternalIdentity Identity { get; set; }

    public void SetRegistered(UserId userId) => _userId = userId;
}

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<UserId, User> _users = [];

    public IReadOnlyCollection<User> All => _users.Values;

    public Task<User?> GetAsync(UserId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<User?> FindByExternalIdentityAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Values.FirstOrDefault(user =>
            user.ExternalIdentities.Any(identity =>
                identity.Provider == provider && identity.Subject == subject)));

    public void Add(User user) => _users[user.Id] = user;
}

internal sealed class FakeFacilityRepository : IFacilityRepository
{
    private readonly Dictionary<FacilityId, Facility> _facilities = [];

    public IReadOnlyCollection<Facility> All => _facilities.Values;

    public Task<Facility?> GetAsync(FacilityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_facilities.GetValueOrDefault(id));

    public void Add(Facility facility) => _facilities[facility.Id] = facility;
}

internal sealed class FakeFacilityInvitationRepository : IFacilityInvitationRepository
{
    private readonly Dictionary<FacilityInvitationId, FacilityInvitation> _invitations = [];

    public IReadOnlyCollection<FacilityInvitation> All => _invitations.Values;

    public Task<FacilityInvitation?> GetAsync(
        FacilityInvitationId id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_invitations.GetValueOrDefault(id));

    public Task<FacilityInvitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_invitations.Values.FirstOrDefault(invitation =>
            invitation.TokenHash == tokenHash));

    public void Add(FacilityInvitation invitation) => _invitations[invitation.Id] = invitation;

    public void Remove(FacilityInvitation invitation) => _invitations.Remove(invitation.Id);
}

internal sealed class EmptyReadDbContext : IReadDbContext
{
    public IQueryable<Facility> Facilities => Enumerable.Empty<Facility>().AsQueryable();

    public IQueryable<FacilityInvitation> FacilityInvitations =>
        Enumerable.Empty<FacilityInvitation>().AsQueryable();

    public IQueryable<User> Users => Enumerable.Empty<User>().AsQueryable();
}

internal static class ValidationAssertions
{
    public static void Equal(
        IReadOnlyList<ValidationError> expected,
        IEnumerable<ValidationError> actual)
    {
        var actualList = actual.ToList();
        Assert.Equal(expected.Count, actualList.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].PropertyName, actualList[index].PropertyName);
            Assert.Equal(expected[index].Code, actualList[index].Code);
            Assert.Equal(expected[index].Message, actualList[index].Message);
            Assert.Equal(expected[index].Parameters, actualList[index].Parameters);
        }
    }
}
