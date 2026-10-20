using System.Linq.Expressions;
using FacilityManagementService.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FacilityManagementService.Infrastructure.Persistence.Conventions;

/// <summary>
/// Stores a strongly typed id as its <see cref="Guid"/> (ADR-0018). Applied to every id by
/// <see cref="EntityIdConventions.AddEntityIdConventions"/>, never by hand.
/// </summary>
internal sealed class EntityIdConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, FromGuid())
    where TId : struct, IEntityId<TId>
{
    // An expression tree cannot call a static abstract member (CS8927), so `value => TId.From(value)`
    // does not compile; the call to TId's implementation of From is built by hand instead.
    private static Expression<Func<Guid, TId>> FromGuid()
    {
        var from = typeof(IEntityId<TId>).GetMethod(nameof(IEntityId<TId>.From))!;
        var map = typeof(TId).GetInterfaceMap(typeof(IEntityId<TId>));
        var implementation = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, from)];

        var value = Expression.Parameter(typeof(Guid), "value");
        return Expression.Lambda<Func<Guid, TId>>(Expression.Call(implementation, value), value);
    }
}
