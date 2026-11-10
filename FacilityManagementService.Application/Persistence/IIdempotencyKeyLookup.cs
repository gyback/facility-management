namespace FacilityManagementService.Application.Persistence;

/// <summary>
/// Looks up a previously committed <c>Idempotency-Key</c> marker (ADR-0017, ADR-0019).
/// Called by the API's idempotency-key middleware on <c>POST</c> requests.
/// </summary>
public interface IIdempotencyKeyLookup
{
    /// <summary>
    /// Returns the stored fingerprint and serialised command result for an unexpired
    /// <c>Idempotency-Key</c> in <paramref name="scope"/>, or <see langword="null"/> if no
    /// unexpired marker exists.
    /// </summary>
    Task<StoredIdempotencyKey?> FindAsync(
        string scope,
        string key,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A committed <c>Idempotency-Key</c> marker retrieved by <see cref="IIdempotencyKeyLookup"/>.
/// </summary>
/// <param name="Fingerprint">The lowercase hex SHA-256 fingerprint of the original request.</param>
/// <param name="ResultJson">The JSON-serialised command result <c>TResult</c>.</param>
public sealed record StoredIdempotencyKey(string Fingerprint, string ResultJson);
