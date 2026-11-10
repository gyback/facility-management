namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// A marker row written in the same transaction as a command's changes (ADR-0019).
/// Serves both EF Core commit verification (<c>verifySucceeded</c>) and HTTP <c>POST</c>
/// <c>Idempotency-Key</c> replay (ADR-0017).
/// </summary>
internal sealed class CommandMarker
{
    public const string CommitScope = "commit";
    public const int MaxScopeLength = 360;
    public const int MaxKeyLength = 64;
    public const int FingerprintLength = 64;

    public static readonly TimeSpan IdempotencyKeyLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan CommitMarkerLifetime = TimeSpan.FromMinutes(5);

    private CommandMarker(
        string scope,
        string key,
        string? fingerprint,
        string? resultJson,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Scope = scope;
        Key = key;
        Fingerprint = fingerprint;
        ResultJson = resultJson;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    // For EF Core.
    private CommandMarker()
    {
        Scope = null!;
        Key = null!;
    }

    public string Scope { get; private set; }

    public string Key { get; private set; }

    public string? Fingerprint { get; private set; }

    public string? ResultJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static CommandMarker ForIdempotencyKey(
        string scope,
        string key,
        string fingerprint,
        string resultJson,
        DateTimeOffset now) =>
        new(
            scope,
            key,
            fingerprint,
            resultJson,
            now,
            now.Add(IdempotencyKeyLifetime));

    public static CommandMarker ForCommitVerification(
        string key,
        DateTimeOffset now) =>
        new(
            CommitScope,
            key,
            fingerprint: null,
            resultJson: null,
            now,
            now.Add(CommitMarkerLifetime));
}
