namespace FacilityManagementService.Domain.Users;

/// <summary>
/// An external identity provider's subject linked to a <see cref="User"/> (ADR-0015).
/// Identified by the <c>(Provider, Subject)</c> pair rather than an id of its own (ADR-0018).
/// </summary>
public sealed class UserExternalIdentity
{
    public const int MaxProviderLength = 64;
    public const int MaxSubjectLength = 256;

    internal UserExternalIdentity(UserId userId, string provider, string subject)
    {
        UserId = userId;
        Provider = NormalizeRequired(provider, MaxProviderLength, nameof(provider));
        Subject = NormalizeRequired(subject, MaxSubjectLength, nameof(subject));
    }

    // For EF Core.
    private UserExternalIdentity()
    {
    }

    public UserId UserId { get; private init; }

    public string Provider { get; private init; } = null!;

    public string Subject { get; private init; } = null!;

    private static string NormalizeRequired(string value, int maxLength, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Must be at most {maxLength} characters.");
        }

        return trimmed;
    }
}
