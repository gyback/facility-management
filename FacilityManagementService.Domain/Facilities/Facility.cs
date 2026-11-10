using FacilityManagementService.Domain.Entities;
using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// A facility (such as a cabin or house) with a sleeping capacity and a set of members holding
/// per-facility roles (ADR-0004, ADR-0005, ADR-0015).
/// </summary>
public sealed class Facility : AggregateRoot<FacilityId>
{
    public const int MaxNameLength = 200;
    public const int MinCapacity = 1;
    public const int MaxCapacity = 1000;

    private readonly List<FacilityMember> _members = [];

    private Facility(FacilityId id)
        : base(id)
    {
    }

    public string Name { get; private set; } = null!;

    public int Capacity { get; private set; }

    public IReadOnlyCollection<FacilityMember> Members => _members;

    /// <summary>
    /// Creates a new facility and makes <paramref name="creator"/> its initial
    /// <see cref="FacilityRole.Administrator"/> (ADR-0015).
    /// </summary>
    /// <exception cref="ForbiddenException">
    /// <paramref name="creator"/> does not have <see cref="User.CanCreateFacilities"/>.
    /// </exception>
    public static Facility Create(User creator, string name, int capacity)
    {
        ArgumentNullException.ThrowIfNull(creator);
        creator.EnsureCanCreateFacilities();

        var facility = new Facility(FacilityId.New())
        {
            Name = NormalizeName(name),
            Capacity = ValidateCapacity(capacity),
        };

        facility._members.Add(new FacilityMember(facility.Id, creator.Id, FacilityRole.Administrator));

        return facility;
    }

    /// <summary>
    /// Whether <paramref name="userId"/> is a member of this facility.
    /// </summary>
    public bool IsMember(UserId userId) =>
        _members.Any(member => member.UserId == userId);

    /// <summary>
    /// The role of <paramref name="userId"/> in this facility, or <see langword="null"/> if
    /// they are not a member.
    /// </summary>
    public FacilityRole? FindRole(UserId userId) =>
        _members.FirstOrDefault(member => member.UserId == userId)?.Role;

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> if <paramref name="userId"/> is not an
    /// <see cref="FacilityRole.Administrator"/> of this facility.
    /// </summary>
    public void EnsureAdministrator(UserId userId)
    {
        if (FindRole(userId) != FacilityRole.Administrator)
        {
            throw new ForbiddenException("Only a facility administrator is allowed to do this.");
        }
    }

    /// <summary>
    /// Renames the facility. Only an administrator may do so.
    /// </summary>
    public void Rename(UserId by, string name)
    {
        EnsureAdministrator(by);
        Name = NormalizeName(name);
    }

    /// <summary>
    /// Sets the facility's sleeping capacity in people (ADR-0005). Only an administrator may do so.
    /// </summary>
    public void SetCapacity(UserId by, int capacity)
    {
        EnsureAdministrator(by);
        Capacity = ValidateCapacity(capacity);
    }

    /// <summary>
    /// Issues a new single-use invitation to join this facility with <paramref name="role"/>
    /// (ADR-0015). Only an administrator may do so.
    /// </summary>
    public (FacilityInvitation Invitation, string Token) CreateInvitation(
        UserId by,
        FacilityRole role,
        DateTimeOffset now)
    {
        EnsureAdministrator(by);
        return FacilityInvitation.Issue(Id, role, by, now);
    }

    /// <summary>
    /// Sets the role of an existing member. Only an administrator may do so, and at least one
    /// administrator must remain.
    /// </summary>
    public void SetMemberRole(UserId by, UserId memberUserId, FacilityRole role)
    {
        EnsureAdministrator(by);

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "The facility role is not defined.");
        }

        var member = _members.FirstOrDefault(existing => existing.UserId == memberUserId)
            ?? throw new InvalidOperationException("The user is not a member of this facility.");

        if (member.Role == FacilityRole.Administrator &&
            role != FacilityRole.Administrator &&
            CountAdministrators() == 1)
        {
            throw new LastFacilityAdministratorException();
        }

        member.Role = role;
    }

    /// <summary>
    /// Removes a member from the facility. Only an administrator may do so, and at least one
    /// administrator must remain.
    /// </summary>
    public void RemoveMember(UserId by, UserId memberUserId)
    {
        EnsureAdministrator(by);

        var member = _members.FirstOrDefault(existing => existing.UserId == memberUserId)
            ?? throw new InvalidOperationException("The user is not a member of this facility.");

        if (member.Role == FacilityRole.Administrator && CountAdministrators() == 1)
        {
            throw new LastFacilityAdministratorException();
        }

        _members.Remove(member);
    }

    internal void AddInvitedMember(UserId userId, FacilityRole role)
    {
        if (IsMember(userId))
        {
            throw new AlreadyFacilityMemberException();
        }

        _members.Add(new FacilityMember(Id, userId, role));
    }

    private int CountAdministrators() =>
        _members.Count(member => member.Role == FacilityRole.Administrator);

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                $"A facility's name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }

    private static int ValidateCapacity(int capacity)
    {
        if (capacity is < MinCapacity or > MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                $"A facility's capacity must be between {MinCapacity} and {MaxCapacity}.");
        }

        return capacity;
    }
}
