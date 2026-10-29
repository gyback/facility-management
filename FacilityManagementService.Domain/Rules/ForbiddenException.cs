namespace FacilityManagementService.Domain.Rules;

/// <summary>
/// Thrown when a domain authorisation check refuses the acting user, e.g. because their
/// facility role does not allow the action, they do not own the resource, or they lack the
/// capability to create facilities (ADR-0015). The API answers it with <c>403 forbidden</c>
/// (ADR-0017).
/// </summary>
/// <remarks>
/// Used only after checking that the caller is a member of the facility the target belongs
/// to; a non-member is answered with <c>404 not-found</c> so the response does not reveal that
/// the resource exists. The message is English for developers and must not quote user input.
/// </remarks>
public sealed class ForbiddenException(string message = "The caller is not allowed to do this.")
    : Exception(message);
