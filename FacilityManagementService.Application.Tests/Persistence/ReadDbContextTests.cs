using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.Application.Tests.Persistence;

public sealed class ReadDbContextTests
{
    // A [Fact] rather than a [Theory] per property: xUnit v3 fails a theory with no data, and
    // IReadDbContext has no queryables until the first feature adds one.
    [Fact]
    public void IReadDbContext_exposes_only_aggregate_roots()
    {
        Assert.Empty(QueryablesOfNonAggregateRoots(typeof(IReadDbContext)));
    }

    [Fact]
    public void The_check_reports_a_queryable_of_something_that_is_not_an_aggregate_root()
    {
        Assert.Equal(
            [$"{nameof(IMixedReadContext.Parts)}: {nameof(Part)}", $"{nameof(IMixedReadContext.Names)}: String"],
            QueryablesOfNonAggregateRoots(typeof(IMixedReadContext)));
    }

    private static List<string> QueryablesOfNonAggregateRoots(Type readContext) =>
        readContext.GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            .Select(property => (property.Name, Element: property.PropertyType.GenericTypeArguments[0]))
            .Where(queryable => !IsAggregateRoot(queryable.Element))
            .Select(queryable => $"{queryable.Name}: {queryable.Element.Name}")
            .ToList();

    private static bool IsAggregateRoot(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }
        }

        return false;
    }

    private interface IMixedReadContext
    {
        IQueryable<Machine> Machines { get; }

        IQueryable<Part> Parts { get; }

        IQueryable<string> Names { get; }
    }

    private readonly record struct MachineId(Guid Value) : IEntityId<MachineId>
    {
        public static MachineId From(Guid value) => new(value);
    }

    private readonly record struct PartId(Guid Value) : IEntityId<PartId>
    {
        public static PartId From(Guid value) => new(value);
    }

    private sealed class Machine : AggregateRoot<MachineId>;

    private sealed class Part : Entity<PartId>;
}
