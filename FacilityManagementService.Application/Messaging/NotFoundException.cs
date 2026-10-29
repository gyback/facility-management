namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// Thrown when a target entity does not exist or belongs to a facility the caller is not a
/// member of, so a response never reveals that data outside the caller's scope exists
/// (ADR-0015). The API answers it with <c>404 not-found</c> (ADR-0017).
/// </summary>
/// <remarks>
/// The message is English for developers and must not quote user input.
/// </remarks>
public sealed class NotFoundException(string message = "The resource was not found.")
    : Exception(message);
