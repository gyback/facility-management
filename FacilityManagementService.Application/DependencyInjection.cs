using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Messaging.Decorators;
using FacilityManagementService.Application.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FacilityManagementService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// The decorators wrapped around every command handler, outermost first:
    /// logging sees every outcome, including validation failures, and validation
    /// runs before a transaction is opened.
    /// </summary>
    internal static readonly Type[] CommandDecorators =
    [
        typeof(LoggingCommandHandlerDecorator<,>),
        typeof(ValidationCommandHandlerDecorator<,>),
        typeof(TransactionCommandHandlerDecorator<,>),
    ];

    /// <summary>
    /// The decorators wrapped around every query handler, outermost first.
    /// Queries never write, so there is no transaction.
    /// </summary>
    internal static readonly Type[] QueryDecorators =
    [
        typeof(LoggingQueryHandlerDecorator<,>),
        typeof(ValidationQueryHandlerDecorator<,>),
    ];

    /// <summary>
    /// Registers every command and query handler and every validator in the Application
    /// assembly, with the handlers wrapped in <see cref="CommandDecorators"/> or
    /// <see cref="QueryDecorators"/>.
    /// </summary>
    /// <remarks>
    /// Resolving a command handler requires an <see cref="Persistence.IUnitOfWork"/>,
    /// which Infrastructure provides.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var types = typeof(DependencyInjection).Assembly.GetTypes();

        services.AddValidators(types);
        services.AddHandlers(types);

        return services;
    }

    /// <summary>
    /// The key for the registrations only <see cref="AddHandlers"/> resolves. It is private,
    /// so nothing else can inject a handler class and bypass its decorators.
    /// </summary>
    private static readonly object HandlerKey = new();

    /// <summary>
    /// Registers each concrete class in <paramref name="types"/> that implements
    /// <see cref="ICommandHandler{TCommand,TResult}"/> or <see cref="IQueryHandler{TQuery,TResult}"/>
    /// as a scoped service for every handler interface it implements, wrapped in the decorators.
    /// Registering the same classes again does nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Two classes handle the same command or query, or a handler is an open generic class.
    /// </exception>
    internal static IServiceCollection AddHandlers(this IServiceCollection services, IEnumerable<Type> types)
    {
        var registered = new Dictionary<Type, Type>();
        var handlers = FindImplementations(types, IsHandlerInterface)
            .Where(handler => !CommandDecorators.Contains(handler.Implementation) &&
                              !QueryDecorators.Contains(handler.Implementation));

        foreach (var (service, implementation) in handlers)
        {
            if (implementation.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException(
                    $"{implementation} is an open generic handler, which cannot be registered; " +
                    "write one handler per command or query.");
            }

            if (!registered.TryAdd(service, implementation))
            {
                throw new InvalidOperationException(
                    $"{service} is implemented by both {registered[service]} and {implementation}; " +
                    "each command and query must have exactly one handler.");
            }

            if (services.Any(descriptor => descriptor.ServiceType == service))
            {
                if (IsRegisteredHandler(services, implementation))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"{service} is already registered, so {implementation} cannot handle it; " +
                    "each command and query must have exactly one handler.");
            }

            var decorators = (service.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                    ? CommandDecorators
                    : QueryDecorators)
                .Select(decorator => decorator.MakeGenericType(service.GetGenericArguments()))
                .ToArray();

            // Built once here rather than per resolution; innermost decorator first.
            var factories = Enumerable.Reverse(decorators)
                .Select(decorator => ActivatorUtilities.CreateFactory(decorator, [service]))
                .ToArray();

            services.TryAddKeyedScoped(implementation, HandlerKey);

            // Never resolved. The factory below hides the decorators' dependencies (e.g.
            // IUnitOfWork) from ValidateOnBuild; registering the decorators lets it check them.
            foreach (var decorator in decorators)
            {
                services.TryAddKeyedScoped(decorator, HandlerKey);
            }

            services.AddScoped(service, provider =>
            {
                var handler = provider.GetRequiredKeyedService(implementation, HandlerKey);

                foreach (var factory in factories)
                {
                    handler = factory(provider, [handler]);
                }

                return handler;
            });
        }

        return services;
    }

    /// <summary>
    /// Registers each concrete class in <paramref name="types"/> that implements
    /// <see cref="IValidator{T}"/> as a scoped service for every validator interface it implements.
    /// Registering the same classes again does nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A validator is an open generic class or validates an interface or abstract class.
    /// The validation decorator only runs the validators for the exact message type, so
    /// such a validator would never run.
    /// </exception>
    internal static IServiceCollection AddValidators(this IServiceCollection services, IEnumerable<Type> types)
    {
        foreach (var (service, implementation) in FindImplementations(types, IsValidatorInterface))
        {
            if (implementation.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException(
                    $"{implementation} is an open generic validator, which would never run; " +
                    "write one validator per command or query.");
            }

            var validated = service.GetGenericArguments()[0];

            if (validated.IsInterface || validated.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"{implementation} validates {validated}, an interface or abstract class, so it would " +
                    "never run; validators run only for the exact command or query type.");
            }

            services.TryAddEnumerable(ServiceDescriptor.Scoped(service, implementation));
        }

        return services;
    }

    /// <summary>
    /// Finds every non-abstract class in <paramref name="types"/>, including open generic
    /// ones, and each interface it implements that matches <paramref name="isService"/>.
    /// </summary>
    internal static IEnumerable<(Type Service, Type Implementation)> FindImplementations(
        IEnumerable<Type> types,
        Func<Type, bool> isService) =>
        types
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(isService)
                .Select(service => (Service: service, Implementation: type)));

    internal static bool IsHandlerInterface(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>));

    private static bool IsValidatorInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IValidator<>);

    private static bool IsRegisteredHandler(IServiceCollection services, Type implementation) =>
        services.Any(descriptor =>
            descriptor.IsKeyedService &&
            descriptor.ServiceKey == HandlerKey &&
            descriptor.ServiceType == implementation);
}
