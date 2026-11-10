using System.Collections.Concurrent;

namespace FacilityManagementService.API.Http.Idempotency;

/// <summary>
/// Tracks <c>(Scope, Key)</c> pairs of <c>POST</c> requests that are currently executing in this
/// process, so a concurrent duplicate is answered immediately with
/// <c>409 idempotency-key-in-use</c> and <c>Retry-After</c> (ADR-0017, ADR-0019).
/// </summary>
internal sealed class InFlightIdempotencyKeys
{
    private readonly ConcurrentDictionary<(string Scope, string Key), byte> _active = new();

    public bool TryAcquire(string scope, string key) =>
        _active.TryAdd((scope, key), 0);

    public void Release(string scope, string key) =>
        _active.TryRemove((scope, key), out _);
}
