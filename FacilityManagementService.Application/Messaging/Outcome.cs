namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// The result of a command that can warn: its value plus the warnings about what it did,
/// e.g. <c>ICommand&lt;Outcome&lt;StayId&gt;&gt;</c>. Commands that cannot warn return their result
/// as is.
/// </summary>
/// <param name="Value">What the command returns, e.g. the id of what it created.</param>
/// <param name="Warnings">The warnings; empty when there is nothing to warn about.</param>
public sealed record Outcome<T>(T Value, IReadOnlyList<Warning> Warnings)
{
    /// <summary>An outcome without warnings.</summary>
    public Outcome(T value)
        : this(value, [])
    {
    }
}
