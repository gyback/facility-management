using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Http.Problems;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace FacilityManagementService.API.Http.Idempotency;

/// <summary>
/// Enforces the <c>Idempotency-Key</c> contract on every <c>POST</c> to an endpoint
/// (ADR-0017, ADR-0019): validates the header, rejects concurrent duplicates (<c>409
/// idempotency-key-in-use</c> with <c>Retry-After</c>) and reused keys with a different
/// request (<c>422 idempotency-key-reused</c>), and configures <see cref="IdempotencyContext"/>
/// so <see cref="IUnitOfWork"/> either replays the committed result or records a new marker in
/// the command's transaction.
/// </summary>
internal sealed partial class IdempotencyKeyMiddleware(
    RequestDelegate next,
    InFlightIdempotencyKeys inFlight)
{
    public const string HeaderName = "Idempotency-Key";

    public const int MaxLength = 64;

    /// <summary>1 to 64 visible ASCII characters; the SPA sends a UUID.</summary>
    public const string Pattern = @"^[\x21-\x7E]{1,64}$";

    public const string RetryAfterSeconds = "1";

    private const string MarkersPrimaryKeyName = "PK_CommandMarkers";

    public async Task InvokeAsync(
        HttpContext context,
        CurrentUser currentUser,
        IdempotencyContext idempotencyContext,
        IIdempotencyKeyLookup lookup)
    {
        var endpoint = context.GetEndpoint();

        if (!HttpMethods.IsPost(context.Request.Method) || !AcceptsPost(endpoint))
        {
            await next(context);
            return;
        }

        var values = context.Request.Headers[HeaderName];

        if (values.Count == 0)
        {
            await ProblemWriter.TryWriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                ProblemCodes.IdempotencyKeyMissing,
                $"Every POST request needs an {HeaderName} header.");
            return;
        }

        var key = values[0];

        if (values.Count > 1 || string.IsNullOrEmpty(key) || !KeyPattern().IsMatch(key))
        {
            await ProblemWriter.TryWriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                ProblemCodes.IdempotencyKeyInvalid,
                $"The {HeaderName} header must be a single value of 1 to {MaxLength} visible ASCII characters.");
            return;
        }

        var scope = GetScope(context, endpoint, currentUser);
        var fingerprint = await ComputeFingerprintAsync(context.Request, context.RequestAborted);

        if (!inFlight.TryAcquire(scope, key))
        {
            await WriteKeyInUseAsync(context);
            return;
        }

        try
        {
            var existing = await lookup.FindAsync(scope, key, context.RequestAborted);

            if (existing is not null)
            {
                if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    await WriteKeyReusedAsync(context);
                    return;
                }

                idempotencyContext.SetReplay(scope, key, fingerprint, existing.ResultJson);
            }
            else
            {
                idempotencyContext.SetRequest(scope, key, fingerprint);
            }

            try
            {
                await next(context);
            }
            catch (ConflictException conflict) when (conflict.IndexName == MarkersPrimaryKeyName)
            {
                var committed = await lookup.FindAsync(scope, key, context.RequestAborted);

                if (committed is not null &&
                    !string.Equals(committed.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    await WriteKeyReusedAsync(context);
                    return;
                }

                await WriteKeyInUseAsync(context);
            }
        }
        finally
        {
            inFlight.Release(scope, key);
        }
    }

    /// <summary>
    /// Whether routing matched an endpoint that handles <c>POST</c>. An unmatched route, or a
    /// method the endpoint does not allow, is answered by routing instead.
    /// </summary>
    private static bool AcceptsPost(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Post) == true;

    /// <summary>
    /// Computes the caller scope for the key (ADR-0017, ADR-0019). Endpoints that opt into
    /// unregistered identities (<c>AcceptInvitation</c>, ADR-0015) always scope by external
    /// identity so a retry after the identity is linked still matches the first call's scope.
    /// </summary>
    private static string GetScope(HttpContext context, Endpoint? endpoint, CurrentUser currentUser)
    {
        var allowsUnregistered = endpoint?.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(data => data.Policy == AuthorizationPolicies.AuthenticatedIdentity) == true;

        if (!allowsUnregistered && currentUser.IsRegistered)
        {
            return $"user:{currentUser.Id}";
        }

        if (context.User.Identity is { IsAuthenticated: true })
        {
            var identity = currentUser.Identity;
            return $"identity:{identity.Provider}:{identity.Subject}";
        }

        return "anonymous";
    }

    private static async Task<string> ComputeFingerprintAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendUtf8(sha256, request.Method);
        AppendUtf8(sha256, "\n");
        AppendUtf8(sha256, request.Path.Value ?? string.Empty);
        AppendUtf8(sha256, request.QueryString.Value ?? string.Empty);
        AppendUtf8(sha256, "\n");

        request.EnableBuffering();
        request.Body.Position = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            int bytesRead;
            while ((bytesRead = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                sha256.AppendData(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            request.Body.Position = 0;
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        sha256.GetHashAndReset(hash);
        return Convert.ToHexStringLower(hash);
    }

    private static void AppendUtf8(IncrementalHash sha256, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        sha256.AppendData(bytes);
    }

    private static ValueTask<bool> WriteKeyInUseAsync(HttpContext context)
    {
        context.Response.Headers.RetryAfter = RetryAfterSeconds;

        return ProblemWriter.TryWriteAsync(
            context,
            StatusCodes.Status409Conflict,
            ProblemCodes.IdempotencyKeyInUse,
            $"A request with this {HeaderName} is already in progress.");
    }

    private static ValueTask<bool> WriteKeyReusedAsync(HttpContext context) =>
        ProblemWriter.TryWriteAsync(
            context,
            StatusCodes.Status422UnprocessableEntity,
            ProblemCodes.IdempotencyKeyReused,
            $"The {HeaderName} was already used for a different request.");

    [GeneratedRegex(Pattern)]
    private static partial Regex KeyPattern();
}
