using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.API.Http.Responses;

/// <summary>
/// The response of a command that creates something, answered with <c>201 Created</c> and a
/// <c>Location</c> header (ADR-0017).
/// </summary>
/// <param name="Id">
/// The new resource's id, unwrapped from its strongly typed id (ADR-0018); a string with format
/// <c>uuid</c> on the wire.
/// </param>
/// <param name="Warnings">Always present; empty when there is nothing to warn about.</param>
internal sealed record CreatedResponse(Guid Id, IReadOnlyList<WarningResponse> Warnings)
{
    /// <summary>The response of a command that can warn.</summary>
    public static CreatedResponse From<TId>(Outcome<TId> outcome) where TId : struct, IEntityId<TId> =>
        new(outcome.Value.Value, WarningResponse.From(outcome.Warnings));

    /// <summary>The response of a command that cannot warn.</summary>
    public static CreatedResponse From<TId>(TId id) where TId : struct, IEntityId<TId> =>
        new(id.Value, []);
}
