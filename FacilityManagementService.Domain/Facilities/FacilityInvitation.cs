using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FacilityManagementService.Domain.Entities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Domain.Facilities;

/// <summary>
/// A single-use invitation to join a <see cref="Facility"/> with a given
/// <see cref="FacilityRole"/>, valid for 14 days (ADR-0015). Only the SHA-256 hash of the
/// secret token is stored.
/// </summary>
public sealed class FacilityInvitation : AggregateRoot<FacilityInvitationId>
{
    /// <summary>How long a newly issued invitation stays valid (ADR-0015).</summary>
    public static readonly TimeSpan Validity = TimeSpan.FromDays(14);

    /// <summary>32 bytes (256 bits) of cryptographic randomness for the bearer token (#45).</summary>
    public const int TokenByteLength = 32;

    /// <summary>Hex-encoded SHA-256 digest length.</summary>
    public const int TokenHashLength = 64;

    private FacilityInvitation(FacilityInvitationId id)
        : base(id)
    {
    }

    public FacilityId FacilityId { get; private init; }

    public FacilityRole Role { get; private init; }

    public string TokenHash { get; private init; } = null!;

    public UserId CreatedByUserId { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public UserId? AcceptedByUserId { get; private set; }

    internal static (FacilityInvitation Invitation, string Token) Issue(
        FacilityId facilityId,
        FacilityRole role,
        UserId createdByUserId,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "The facility role is not defined.");
        }

        Span<byte> randomBytes = stackalloc byte[TokenByteLength];
        RandomNumberGenerator.Fill(randomBytes);
        var token = Base64Url.EncodeToString(randomBytes);

        var invitation = new FacilityInvitation(FacilityInvitationId.New())
        {
            FacilityId = facilityId,
            Role = role,
            TokenHash = HashToken(token),
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            ExpiresAt = now.Add(Validity),
        };

        return (invitation, token);
    }

    /// <summary>
    /// Computes the lowercase hex-encoded SHA-256 hash of a raw invitation <paramref name="token"/>.
    /// </summary>
    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var bytes = Encoding.UTF8.GetBytes(token.Trim());
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>
    /// Whether this invitation has not yet been accepted and has not expired as of <paramref name="now"/>.
    /// </summary>
    public bool IsPending(DateTimeOffset now) =>
        AcceptedAt is null && now < ExpiresAt;

    /// <summary>
    /// Accepts the invitation for <paramref name="userId"/>, adding them to
    /// <paramref name="facility"/> with <see cref="Role"/> and marking the invitation spent.
    /// </summary>
    /// <exception cref="InvitationExpiredException">
    /// The invitation has already been accepted or has expired as of <paramref name="now"/>.
    /// </exception>
    /// <exception cref="AlreadyFacilityMemberException">
    /// <paramref name="userId"/> is already a member of <paramref name="facility"/>.
    /// </exception>
    public void Accept(Facility facility, UserId userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facility);

        if (facility.Id != FacilityId)
        {
            throw new ArgumentException("The facility does not match the invitation.", nameof(facility));
        }

        if (!IsPending(now))
        {
            throw new InvitationExpiredException();
        }

        facility.AddInvitedMember(userId, Role);

        AcceptedAt = now;
        AcceptedByUserId = userId;
    }
}
