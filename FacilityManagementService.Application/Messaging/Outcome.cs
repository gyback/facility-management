using System.Text.Json.Serialization;

namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// The result of a command that can warn: its value plus the warnings about what it did,
/// e.g. <c>ICommand&lt;Outcome&lt;StayId&gt;&gt;</c>. Commands that cannot warn return their result
/// as is.
/// </summary>
public sealed record Outcome<T>
{
    /// <summary>Creates an outcome with warnings.</summary>
    /// <param name="Value">What the command returns, e.g. the id of what it created.</param>
    /// <param name="Warnings">The warnings; empty when there is nothing to warn about.</param>
    [JsonConstructor]
    public Outcome(T Value, IReadOnlyList<Warning> Warnings)
    {
        this.Value = Value;
        this.Warnings = Warnings;
    }

    /// <summary>An outcome without warnings.</summary>
    public Outcome(T value)
        : this(value, [])
    {
    }

    /// <summary>What the command returns, e.g. the id of what it created.</summary>
    public T Value { get; init; }

    /// <summary>The warnings; empty when there is nothing to warn about.</summary>
    public IReadOnlyList<Warning> Warnings { get; init; }

    public void Deconstruct(out T value, out IReadOnlyList<Warning> warnings)
    {
        value = Value;
        warnings = Warnings;
    }
}
