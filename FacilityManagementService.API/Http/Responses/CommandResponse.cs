using FacilityManagementService.Application.Messaging;

namespace FacilityManagementService.API.Http.Responses;

/// <summary>
/// The response of a command that creates nothing and returns nothing else, answered with
/// <c>200 OK</c>; command endpoints never answer <c>204</c> (ADR-0017). A command with a
/// richer outcome defines its own response with a <c>Warnings</c> property.
/// </summary>
/// <param name="Warnings">Always present; empty when there is nothing to warn about.</param>
internal sealed record CommandResponse(IReadOnlyList<WarningResponse> Warnings)
{
    /// <summary>The response of a command that cannot warn.</summary>
    public static readonly CommandResponse NoWarnings = new([]);

    public static CommandResponse From(IReadOnlyList<Warning> warnings) => new(WarningResponse.From(warnings));
}
