using System.Text.Json;

namespace FacilityManagementService.Application.Validation;

/// <summary>
/// A single problem with a command or query. Create one with the factory for its code, e.g.
/// <see cref="TooLong"/>, so codes, messages and parameters stay consistent across validators.
/// </summary>
/// <param name="PropertyName">The invalid property, e.g. <c>nameof(CreateStay.To)</c>.</param>
/// <param name="Code">
/// The kind of problem, a <see cref="ValidationCodes"/> constant. The frontend translates it.
/// </param>
/// <param name="Message">
/// An English description for developers and logs. It is never shown to users and must not
/// quote user input.
/// </param>
/// <param name="Parameters">
/// The values the frontend needs to describe the problem, keyed by camelCase name, e.g.
/// <c>maxLength</c>; <see langword="null"/> when there are none.
/// </param>
public sealed record ValidationError(
    string PropertyName,
    string Code,
    string Message,
    IReadOnlyDictionary<string, object>? Parameters = null)
{
    /// <summary>The property has no value.</summary>
    public static ValidationError Required(string propertyName) =>
        new(propertyName, ValidationCodes.Required, "Is required.");

    /// <summary>The property's text is longer than <paramref name="maxLength"/> characters.</summary>
    public static ValidationError TooLong(string propertyName, int maxLength) =>
        new(
            propertyName,
            ValidationCodes.TooLong,
            $"Must be at most {maxLength} characters.",
            new Dictionary<string, object> { ["maxLength"] = maxLength });

    /// <summary>The property's value is outside <paramref name="minimum"/> to <paramref name="maximum"/>.</summary>
    public static ValidationError OutOfRange(string propertyName, object minimum, object maximum) =>
        new(
            propertyName,
            ValidationCodes.OutOfRange,
            $"Must be between {minimum} and {maximum}.",
            new Dictionary<string, object> { ["minimum"] = minimum, ["maximum"] = maximum });

    /// <summary>
    /// The property's value is not after the value of <paramref name="otherPropertyName"/>,
    /// e.g. a stay that ends before it starts.
    /// </summary>
    /// <remarks>
    /// The <c>otherProperty</c> parameter is the other property's name on the wire (camelCase),
    /// so the frontend can find that field's label.
    /// </remarks>
    public static ValidationError MustBeAfter(string propertyName, string otherPropertyName)
    {
        var otherProperty = JsonNamingPolicy.CamelCase.ConvertName(otherPropertyName);

        return new(
            propertyName,
            ValidationCodes.MustBeAfter,
            $"Must be after {otherProperty}.",
            new Dictionary<string, object> { ["otherProperty"] = otherProperty });
    }
}
