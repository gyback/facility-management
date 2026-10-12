namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// The result of a command that has no meaningful return value.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
