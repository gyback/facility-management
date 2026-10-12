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
    /// Registers each concrete class in <paramref name="types"/> that implements
    /// <see cref="ICommandHandler{TCommand,TResult}"/> or <see cref="IQueryHandler{TQuery,TResult}"/>
    /// as a scoped service for every handler interface it implements, wrapped in the decorators.
    /// The class itself is also registered, so the container can validate its dependencies.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Two classes handle the same command or query.
    /// </exception>
    internal static IServiceCollection AddHandlers(this IServiceCollection services, IEnumerable<Type> types)
    {
        var registered = new Dictionary<Type, Type>();

        foreach (var (service, implementation) in FindImplementations(types, IsHandlerInterface))
        {
            if (!registered.TryAdd(service, implementation))
            {
                throw new InvalidOperationException(
                    $"{service} is implemented by both {registered[service]} and {implementation}; " +
                    "each command and query must have exactly one handler.");
            }

            var decorators = service.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                ? CommandDecorators
                : QueryDecorators;

            // Built once here rather than per resolution; innermost decorator first.
            var factories = Enumerable.Reverse(decorators)
                .Select(decorator => ActivatorUtilities.CreateFactory(
                    decorator.MakeGenericType(service.GetGenericArguments()), [service]))
                .ToArray();

            services.TryAddScoped(implementation);
            services.AddScoped(service, provider =>
            {
                var handler = provider.GetRequiredService(implementation);

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
    /// </summary>
    internal static IServiceCollection AddValidators(this IServiceCollection services, IEnumerable<Type> types)
    {
        foreach (var (service, implementation) in FindImplementations(types, IsValidatorInterface))
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }

    private static IEnumerable<(Type Service, Type Implementation)> FindImplementations(
        IEnumerable<Type> types,
        Func<Type, bool> isService) =>
        types
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(isService)
                .Select(service => (Service: service, Implementation: type)));

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>));

    private static bool IsValidatorInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IValidator<>);
}
