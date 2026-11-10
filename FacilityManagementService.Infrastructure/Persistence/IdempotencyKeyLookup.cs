using FacilityManagementService.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityManagementService.Infrastructure.Persistence;

/// <summary>
/// Queries <see cref="CommandMarker"/> rows untracked to check whether an
/// <c>Idempotency-Key</c> has already been committed and has not expired (ADR-0017, ADR-0019).
/// </summary>
internal sealed class IdempotencyKeyLookup(AppDbContext context, TimeProvider timeProvider) : IIdempotencyKeyLookup
{
    public async Task<StoredIdempotencyKey?> FindAsync(
        string scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        return await context.Set<CommandMarker>()
            .AsNoTracking()
            .Where(marker =>
                marker.Scope == scope &&
                marker.Key == key &&
                marker.ExpiresAt > now &&
                marker.Fingerprint != null &&
                marker.ResultJson != null)
            .Select(marker => new StoredIdempotencyKey(marker.Fingerprint!, marker.ResultJson!))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
