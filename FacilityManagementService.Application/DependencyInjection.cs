using System.Reflection;
using FacilityManagementService.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityManagementService.Application;

public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    /// <summary>
    /// Registers every command and query handler in the Application assembly.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    /// <summary>
    /// Registers each concrete class in <paramref name="assembly"/> that implements
    /// <see cref="ICommandHandler{TCommand,TResult}"/> or <see cref="IQueryHandler{TQuery,TResult}"/>
    /// as a scoped service for every handler interface it implements.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Two classes handle the same command or query.
    /// </exception>
    internal static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var registrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(IsHandlerInterface)
                .Select(service => (Service: service, Implementation: type)));

        var registered = new Dictionary<Type, Type>();

        foreach (var (service, implementation) in registrations)
        {
            if (!registered.TryAdd(service, implementation))
            {
                throw new InvalidOperationException(
                    $"{service} is implemented by both {registered[service]} and {implementation}; " +
                    "each command and query must have exactly one handler.");
            }

            services.AddScoped(service, implementation);
        }

        return services;
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());
}
