using FacilityManagementService.Application.Messaging;

namespace FacilityManagementService.API.Http.Responses;

/// <summary>
/// The response of a command that creates something, answered with <c>201 Created</c> and a
/// <c>Location</c> header (ADR-0017).
/// </summary>
/// <param name="Id">The new resource's id; ids are strings on the wire, whatever their type inside.</param>
/// <param name="Warnings">Always present; empty when there is nothing to warn about.</param>
internal sealed record CreatedResponse(string Id, IReadOnlyList<WarningResponse> Warnings)
{
    /// <summary>The response of a command that can warn.</summary>
    public static CreatedResponse From<TId>(Outcome<TId> outcome) where TId : notnull =>
        new(outcome.Value.ToString()!, WarningResponse.From(outcome.Warnings));

    /// <summary>The response of a command that cannot warn.</summary>
    public static CreatedResponse From<TId>(TId id) where TId : notnull =>
        new(id.ToString()!, []);
}
