namespace FacilityManagementService.Application.Messaging;

/// <summary>
/// A use case that changes state. Returns at most an identifier or an outcome,
/// never a read model; use <see cref="Unit"/> when there is nothing to return.
/// </summary>
/// <typeparam name="TResult">The result of handling the command.</typeparam>
public interface ICommand<out TResult>;
