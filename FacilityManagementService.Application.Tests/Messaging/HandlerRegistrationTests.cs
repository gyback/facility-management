using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Messaging.Decorators;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityManagementService.Application.Tests.Messaging;

public sealed class HandlerRegistrationTests
{
    [Fact]
    public void Every_command_and_query_in_the_Application_assembly_has_exactly_one_handler()
    {
        var problems = FindMessagesWithoutExactlyOneHandler(typeof(DependencyInjection).Assembly.GetTypes());

        Assert.Empty(problems);
    }

    [Fact]
    public void The_handler_check_reports_missing_and_duplicate_handlers()
    {
        Type[] types =
        [
            typeof(RenameThing), typeof(CountThings), typeof(Unhandled),
            typeof(ThingsHandler), typeof(OtherRenameThingHandler), typeof(StructRenameThingHandler),
        ];

        var problems = FindMessagesWithoutExactlyOneHandler(types);

        Assert.Equal(
            [
                $"{nameof(RenameThing)} has 2 handlers: {nameof(ThingsHandler)}, {nameof(OtherRenameThingHandler)}",
                $"{nameof(Unhandled)} has 0 handlers: ",
            ],
            problems);
    }

    [Fact]
    public void AddApplication_builds_a_valid_container()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<IUnitOfWork, RecordingUnitOfWork>()
            .AddSingleton<Journal>()
            .AddApplication();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void Two_handlers_for_the_same_message_are_rejected()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddHandlers([typeof(ThingsHandler), typeof(OtherRenameThingHandler)]));

        Assert.Contains(nameof(ThingsHandler), exception.Message);
        Assert.Contains(nameof(OtherRenameThingHandler), exception.Message);
    }

    [Fact]
    public void A_class_handling_several_messages_is_registered_for_each()
    {
        using var provider = BuildProvider(typeof(ThingsHandler));
        using var scope = provider.CreateScope();

        Assert.IsType<LoggingCommandHandlerDecorator<RenameThing, Unit>>(
            scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameThing, Unit>>());
        Assert.IsType<LoggingQueryHandlerDecorator<CountThings, int>>(
            scope.ServiceProvider.GetRequiredService<IQueryHandler<CountThings, int>>());
    }

    [Fact]
    public void Handlers_and_validators_are_scoped()
    {
        using var provider = BuildProvider(typeof(CreateThingHandler), typeof(CreateThingValidator));
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.Same(
            first.ServiceProvider.GetRequiredService<ICommandHandler<CreateThing, Guid>>(),
            first.ServiceProvider.GetRequiredService<ICommandHandler<CreateThing, Guid>>());
        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<ICommandHandler<CreateThing, Guid>>(),
            second.ServiceProvider.GetRequiredService<ICommandHandler<CreateThing, Guid>>());
    }

    [Fact]
    public void A_missing_unit_of_work_fails_when_the_container_is_built()
    {
        var services = new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton<Journal>()
            .AddHandlers([typeof(CreateThingHandler)]);

        var exception = Assert.Throws<AggregateException>(() =>
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));

        Assert.Contains(nameof(IUnitOfWork), exception.Message);
    }

    [Fact]
    public void The_handler_class_cannot_be_resolved_without_its_decorators()
    {
        using var provider = BuildProvider(typeof(CreateThingHandler));
        using var scope = provider.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<CreateThingHandler>());
    }

    [Fact]
    public async Task Registering_the_same_handlers_and_validators_twice_registers_them_once()
    {
        Type[] types = [typeof(ThingsHandler), typeof(CreateThingHandler), typeof(CreateThingValidator)];
        using var provider = new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton<Journal>()
            .AddScoped<IUnitOfWork, RecordingUnitOfWork>()
            .AddValidators(types).AddHandlers(types)
            .AddValidators(types).AddHandlers(types)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.Single(scope.ServiceProvider.GetServices<ICommandHandler<RenameThing, Unit>>());
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateThing, Guid>>()
                .HandleAsync(new CreateThing(""), TestContext.Current.CancellationToken));
        Assert.Single(exception.Errors);
        Assert.Equal(["validate"], provider.GetRequiredService<Journal>().Entries);
    }

    [Fact]
    public void A_second_handler_for_an_already_registered_message_is_rejected()
    {
        var services = new ServiceCollection().AddHandlers([typeof(ThingsHandler)]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddHandlers([typeof(OtherRenameThingHandler)]));

        Assert.Contains(nameof(OtherRenameThingHandler), exception.Message);
    }

    [Theory]
    [InlineData(typeof(AnyMessageValidator<>))]
    [InlineData(typeof(NamedValidator))]
    public void Validators_that_would_never_run_are_rejected(Type validator)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddValidators([validator]));

        Assert.Contains(validator.Name, exception.Message);
    }

    [Fact]
    public void Open_generic_handlers_are_rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddHandlers([typeof(GenericHandler<>)]));

        Assert.Contains(typeof(GenericHandler<>).Name, exception.Message);
    }

    internal static ServiceProvider BuildProvider(params Type[] types) =>
        new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton<Journal>()
            .AddScoped<IUnitOfWork, RecordingUnitOfWork>()
            .AddValidators(types)
            .AddHandlers(types)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    /// <summary>
    /// For every concrete command and query in <paramref name="types"/>, checks that exactly
    /// one handler in <paramref name="types"/>, as found by <see cref="DependencyInjection.AddHandlers"/>,
    /// handles it.
    /// </summary>
    private static List<string> FindMessagesWithoutExactlyOneHandler(IReadOnlyCollection<Type> types)
    {
        var handlers = DependencyInjection
            .FindImplementations(types, DependencyInjection.IsHandlerInterface)
            .ToList();

        return types
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(message => message.GetInterfaces()
                .Where(type => type.IsGenericType)
                .Select(type => type.GetGenericTypeDefinition() switch
                {
                    var definition when definition == typeof(ICommand<>) =>
                        typeof(ICommandHandler<,>).MakeGenericType(message, type.GetGenericArguments()[0]),
                    var definition when definition == typeof(IQuery<>) =>
                        typeof(IQueryHandler<,>).MakeGenericType(message, type.GetGenericArguments()[0]),
                    _ => null,
                })
                .OfType<Type>()
                .Select(handlerInterface => (Message: message, Handlers: handlers
                    .Where(handler => handler.Service == handlerInterface)
                    .Select(handler => handler.Implementation.Name)
                    .ToList())))
            .Where(entry => entry.Handlers.Count != 1)
            .Select(entry =>
                $"{entry.Message.Name} has {entry.Handlers.Count} handlers: {string.Join(", ", entry.Handlers)}")
            .ToList();
    }
}
