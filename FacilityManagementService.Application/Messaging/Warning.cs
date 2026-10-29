namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// Something the user should know about a command that succeeded and was committed, e.g. a
/// stay that pushes nights over the facility's capacity (ADR-0005, ADR-0017). Returned in an
/// <see cref="Outcome{T}"/>.
/// </summary>
/// <remarks>
/// Each kind of warning is a subtype carrying its own typed data, e.g.
/// <c>CapacityExceededWarning(IReadOnlyList&lt;DateOnly&gt; Nights, int Capacity)</c>. Adding one
/// also needs a <see cref="WarningCodes"/> constant, a response contract in the API and an
/// entry in the API README's catalogue.
/// </remarks>
public abstract record Warning
{
    /// <summary>The kind of warning, a <see cref="WarningCodes"/> constant. The frontend translates it.</summary>
    public abstract string Code { get; }

    /// <summary>An English description for developers and logs; never shown to users.</summary>
    public abstract string Message { get; }
}
