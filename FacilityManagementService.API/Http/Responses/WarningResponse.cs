using FacilityManagementService.Application.Messaging;

namespace FacilityManagementService.API.Http.Responses;

/// <summary>
/// A warning in a command response (ADR-0017): a <c>code</c>, an English <c>message</c> for
/// developers, and the fields of its kind. Polymorphic on <c>code</c>, so OpenAPI and the
/// generated client see a union of the known warnings.
/// </summary>
/// <remarks>
/// To add a warning: a <see cref="Warning"/> subtype in Application with a
/// <see cref="WarningCodes"/> constant, a subtype of this record registered with
/// <c>[JsonDerivedType(typeof(XWarningResponse), WarningCodes.X)]</c>, an arm in
/// <see cref="From(Warning)"/>, and an entry in the README's catalogue. There is no
/// <c>Code</c> property: the discriminator writes <c>code</c>.
/// <para>
/// System.Text.Json refuses a polymorphic type without derived types, so the first warning
/// (#8) also adds <c>[JsonPolymorphic(TypeDiscriminatorPropertyName = "code")]</c> here; a test
/// fails until it does.
/// </para>
/// </remarks>
internal abstract record WarningResponse(string Message)
{
    public static WarningResponse From(Warning warning) => warning switch
    {
        _ => throw new InvalidOperationException(
            $"There is no response contract for the warning {warning.GetType().Name}."),
    };

    public static IReadOnlyList<WarningResponse> From(IReadOnlyList<Warning> warnings) =>
        warnings.Select(From).ToArray();
}
