namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// A use case that reads state. Has no side effects and never calls <c>SaveChanges</c>.
/// </summary>
/// <typeparam name="TResult">The read model returned by the query.</typeparam>
public interface IQuery<out TResult>;
