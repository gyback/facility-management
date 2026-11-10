using System.Diagnostics.CodeAnalysis;

namespace FacilityManagementService.Application.Persistence;

/// <summary>
/// Scoped per-request state connecting the API's <c>Idempotency-Key</c> middleware with
/// <see cref="IUnitOfWork"/> (ADR-0017, ADR-0019).
/// </summary>
public sealed class IdempotencyContext
{
    private IdempotencyRequest? _request;
    private string? _replayedResultJson;

    /// <summary>
    /// The active <c>Idempotency-Key</c> request metadata when the current command should record
    /// an idempotency marker in its transaction; <see langword="null"/> when no key applies
    /// (<c>PUT</c>, <c>DELETE</c>, or non-HTTP callers).
    /// </summary>
    public IdempotencyRequest? Request => _request;

    /// <summary>
    /// Configures the current scope to record an <c>Idempotency-Key</c> marker when
    /// <see cref="IUnitOfWork.ExecuteAsync{TResult}"/> commits.
    /// </summary>
    public void SetRequest(string scope, string key, string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        _request = new IdempotencyRequest(scope, key, fingerprint);
        _replayedResultJson = null;
    }

    /// <summary>
    /// Configures the current scope to replay a previously committed command result from
    /// <paramref name="resultJson"/> without running the handler or opening a transaction.
    /// </summary>
    public void SetReplay(string scope, string key, string fingerprint, string resultJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);

        _request = new IdempotencyRequest(scope, key, fingerprint);
        _replayedResultJson = resultJson;
    }

    /// <summary>
    /// Deserialises and returns the replayed command result if this scope is replaying a
    /// previously committed <c>Idempotency-Key</c>.
    /// </summary>
    public bool TryGetReplayedResult<TResult>([MaybeNullWhen(false)] out TResult result)
    {
        if (_replayedResultJson is null)
        {
            result = default;
            return false;
        }

        result = CommandResultSerializer.Deserialize<TResult>(_replayedResultJson);
        return true;
    }
}

/// <summary>
/// The caller scope, <c>Idempotency-Key</c> value and request fingerprint for an HTTP
/// <c>POST</c> command (ADR-0017, ADR-0019).
/// </summary>
public sealed record IdempotencyRequest(string Scope, string Key, string Fingerprint);
