using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Messaging.Decorators;
using FacilityManagementService.Application.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace FacilityManagementService.Application.Tests.Messaging;

public sealed class HandlerDecoratorTests : IDisposable
{
    private readonly ServiceProvider _provider = HandlerRegistrationTests.BuildProvider(
        typeof(CreateThingHandler), typeof(CreateThingValidator),
        typeof(GetThingHandler), typeof(GetThingValidator));

    private readonly IServiceScope _scope;

    public HandlerDecoratorTests() => _scope = _provider.CreateScope();

    private List<string> Journal => _provider.GetRequiredService<Journal>().Entries;

    private IReadOnlyList<FakeLogRecord> Logs => _provider.GetFakeLogCollector().GetSnapshot();

    private T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    [Fact]
    public void Command_handlers_are_wrapped_with_logging_outermost() =>
        Assert.IsType<LoggingCommandHandlerDecorator<CreateThing, Guid>>(Resolve<ICommandHandler<CreateThing, Guid>>());

    [Fact]
    public void Query_handlers_are_wrapped_with_logging_outermost() =>
        Assert.IsType<LoggingQueryHandlerDecorator<GetThing, string>>(Resolve<IQueryHandler<GetThing, string>>());

    [Fact]
    public async Task A_valid_command_is_validated_then_handled_inside_a_transaction()
    {
        await Resolve<ICommandHandler<CreateThing, Guid>>().HandleAsync(new CreateThing("Sauna"), TestContext.Current.CancellationToken);

        Assert.Equal(["validate", "begin", "handle", "commit"], Journal);
        var log = Assert.Single(Logs);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.StartsWith($"Handled {nameof(CreateThing)} in ", log.Message);
    }

    [Fact]
    public async Task An_invalid_command_is_rejected_and_logged_before_a_transaction_is_opened()
    {
        var handler = Resolve<ICommandHandler<CreateThing, Guid>>();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new CreateThing(""), TestContext.Current.CancellationToken));

        Assert.Equal([ValidationError.Required(nameof(CreateThing.Name))], exception.Errors);
        Assert.Equal(["validate"], Journal);
        var log = Assert.Single(Logs);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.StartsWith($"Rejected {nameof(CreateThing)} after ", log.Message);
    }

    [Fact]
    public async Task A_failing_command_is_rolled_back_and_logged_as_an_error()
    {
        var handler = Resolve<ICommandHandler<CreateThing, Guid>>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CreateThing("explode"), TestContext.Current.CancellationToken));

        Assert.Equal(["validate", "begin", "handle", "rollback"], Journal);
        var log = Assert.Single(Logs);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Same(exception, log.Exception);
    }

    [Fact]
    public async Task A_cancelled_command_is_rolled_back_and_logged_as_information()
    {
        var handler = Resolve<ICommandHandler<CreateThing, Guid>>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new CreateThing("Sauna"), new CancellationToken(canceled: true)));

        Assert.Equal(["validate", "begin", "handle", "rollback"], Journal);
        var log = Assert.Single(Logs);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Null(log.Exception);
        Assert.StartsWith($"Cancelled {nameof(CreateThing)} after ", log.Message);
    }

    [Fact]
    public async Task A_valid_query_is_validated_and_handled_without_a_transaction()
    {
        var result = await Resolve<IQueryHandler<GetThing, string>>().HandleAsync(new GetThing("Sauna"), TestContext.Current.CancellationToken);

        Assert.Equal("Sauna", result);
        Assert.Equal(["validate", "handle"], Journal);
        var log = Assert.Single(Logs);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.StartsWith($"Handled {nameof(GetThing)} in ", log.Message);
    }

    [Fact]
    public async Task An_invalid_query_is_rejected_and_logged()
    {
        var handler = Resolve<IQueryHandler<GetThing, string>>();

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new GetThing(" "), TestContext.Current.CancellationToken));

        Assert.Equal(["validate"], Journal);
        Assert.Equal(LogLevel.Warning, Assert.Single(Logs).Level);
    }

    [Fact]
    public async Task Log_messages_do_not_contain_the_message_contents()
    {
        await Resolve<ICommandHandler<CreateThing, Guid>>().HandleAsync(new CreateThing("Secret sauna"), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(Logs, log => log.Message.Contains("Secret"));
    }

    [Fact]
    public void The_validation_exception_message_does_not_contain_the_error_messages()
    {
        var exception = new ValidationException([new ValidationError("Email", "invalid", "'secret@example.com' is invalid.")]);

        Assert.Equal("Validation failed with 1 errors.", exception.Message);
    }
}
