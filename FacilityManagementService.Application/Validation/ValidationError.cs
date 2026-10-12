namespace FacilityManagementService.Application.Validation;

/// <summary>
/// A single problem with a command or query.
/// </summary>
/// <param name="PropertyName">The invalid property, e.g. <c>nameof(CreateStay.To)</c>.</param>
/// <param name="Message">A human-readable description of the problem.</param>
public sealed record ValidationError(string PropertyName, string Message);
