using FacilityManagementService.Domain.Entities;
using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Domain.Users;

/// <summary>
/// A registered person in the system, created when accepting an invitation (ADR-0015).
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    public const int MaxDisplayNameLength = 200;

    private readonly List<UserExternalIdentity> _externalIdentities = [];

    private User(UserId id)
        : base(id)
    {
    }

    public string DisplayName { get; private set; } = null!;

    /// <summary>
    /// Whether this user is allowed to create new facilities (ADR-0015). Granted and revoked
    /// only through operator commands, never through the API.
    /// </summary>
    public bool CanCreateFacilities { get; private set; }

    public IReadOnlyCollection<UserExternalIdentity> ExternalIdentities => _externalIdentities;

    /// <summary>
    /// Registers a new user and links their initial external identity.
    /// </summary>
    public static User Register(string displayName, string provider, string subject)
    {
        var user = new User(UserId.New())
        {
            DisplayName = NormalizeDisplayName(displayName),
            CanCreateFacilities = false,
        };

        user._externalIdentities.Add(new UserExternalIdentity(user.Id, provider, subject));

        return user;
    }

    /// <summary>
    /// Updates the user's display name.
    /// </summary>
    public void Rename(string displayName) =>
        DisplayName = NormalizeDisplayName(displayName);

    /// <summary>
    /// Grants the capability to create facilities (ADR-0015).
    /// </summary>
    public void GrantFacilityCreation() => CanCreateFacilities = true;

    /// <summary>
    /// Revokes the capability to create facilities (ADR-0015).
    /// </summary>
    public void RevokeFacilityCreation() => CanCreateFacilities = false;

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> if the user is not allowed to create facilities.
    /// </summary>
    public void EnsureCanCreateFacilities()
    {
        if (!CanCreateFacilities)
        {
            throw new ForbiddenException("The caller is not allowed to create facilities.");
        }
    }

    /// <summary>
    /// Returns the trimmed <paramref name="displayName"/> if it is non-empty, within
    /// <see cref="MaxDisplayNameLength"/> and not an email address; otherwise <see langword="null"/>.
    /// Used when falling back to an identity token's <c>name</c> claim (ADR-0015, #45).
    /// </summary>
    public static string? TryGetNonEmailDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var trimmed = displayName.Trim();

        if (trimmed.Length > MaxDisplayNameLength || trimmed.Contains('@'))
        {
            return null;
        }

        return trimmed;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var trimmed = displayName.Trim();

        if (trimmed.Length > MaxDisplayNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayName),
                $"A user's display name must be at most {MaxDisplayNameLength} characters.");
        }

        return trimmed;
    }
}
