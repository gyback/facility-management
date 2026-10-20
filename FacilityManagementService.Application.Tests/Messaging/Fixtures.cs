using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Validation;

namespace FacilityManagementService.Application.Tests.Messaging;

/// <summary>
/// Records what ran, in order, so tests can assert how the decorators nest.
/// </summary>
internal sealed class Journal
{
    public List<string> Entries { get; } = [];
}

internal sealed record CreateThing(string Name) : ICommand<Guid>;

internal sealed class CreateThingHandler(Journal journal) : ICommandHandler<CreateThing, Guid>
{
    public Task<Guid> HandleAsync(CreateThing command, CancellationToken cancellationToken = default)
    {
        journal.Entries.Add("handle");
        cancellationToken.ThrowIfCancellationRequested();

        return command.Name == "explode"
            ? throw new InvalidOperationException("Boom.")
            : Task.FromResult(Guid.NewGuid());
    }
}

internal sealed class CreateThingValidator(Journal journal) : IValidator<CreateThing>
{
    public IEnumerable<ValidationError> Validate(CreateThing instance)
    {
        journal.Entries.Add("validate");

        if (string.IsNullOrWhiteSpace(instance.Name))
        {
            yield return ValidationError.Required(nameof(CreateThing.Name));
        }
    }
}

internal sealed record GetThing(string Name) : IQuery<string>;

internal sealed class GetThingHandler(Journal journal) : IQueryHandler<GetThing, string>
{
    public Task<string> HandleAsync(GetThing query, CancellationToken cancellationToken = default)
    {
        journal.Entries.Add("handle");

        return Task.FromResult(query.Name);
    }
}

internal sealed class GetThingValidator(Journal journal) : IValidator<GetThing>
{
    public IEnumerable<ValidationError> Validate(GetThing instance)
    {
        journal.Entries.Add("validate");

        if (string.IsNullOrWhiteSpace(instance.Name))
        {
            yield return ValidationError.Required(nameof(GetThing.Name));
        }
    }
}

internal sealed class RecordingUnitOfWork(Journal journal) : IUnitOfWork
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        journal.Entries.Add("begin");

        try
        {
            var result = await operation(cancellationToken);
            journal.Entries.Add("commit");
            return result;
        }
        catch
        {
            journal.Entries.Add("rollback");
            throw;
        }
    }
}

internal sealed record RenameThing(string Name) : ICommand<Unit>;

internal sealed record CountThings : IQuery<int>;

internal sealed class ThingsHandler : ICommandHandler<RenameThing, Unit>, IQueryHandler<CountThings, int>
{
    public Task<Unit> HandleAsync(RenameThing command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Unit.Value);

    public Task<int> HandleAsync(CountThings query, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

internal sealed class OtherRenameThingHandler : ICommandHandler<RenameThing, Unit>
{
    public Task<Unit> HandleAsync(RenameThing command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Unit.Value);
}

internal sealed record Unhandled : IQuery<int>;

internal sealed record GetGeneric<T> : IQuery<int>;

internal sealed class GenericHandler<T> : IQueryHandler<GetGeneric<T>, int>
{
    public Task<int> HandleAsync(GetGeneric<T> query, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

internal sealed class AnyMessageValidator<T> : IValidator<T>
{
    public IEnumerable<ValidationError> Validate(T instance) => [];
}

internal interface INamed
{
    string Name { get; }
}

internal sealed class NamedValidator : IValidator<INamed>
{
    public IEnumerable<ValidationError> Validate(INamed instance) => [];
}

internal readonly record struct StructRenameThingHandler : ICommandHandler<RenameThing, Unit>
{
    public Task<Unit> HandleAsync(RenameThing command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Unit.Value);
}
