using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.Domain.Tests.Entities;

/// <summary>
/// Checks that every strongly typed id is written as ADR-0018 and the Domain README describe.
/// </summary>
public sealed class EntityIdConformanceTests
{
    // A [Fact] over all ids rather than a [Theory] per id: xUnit v3 fails a theory with no data,
    // and Domain has no ids until the first feature adds one.
    [Fact]
    public void Every_id_in_the_Domain_assembly_is_written_by_the_convention()
    {
        var problems = typeof(IEntityId<>).Assembly.GetTypes()
            .Where(IsEntityId)
            .SelectMany(Problems);

        Assert.Empty(problems);
    }

    [Fact]
    public void An_id_written_by_the_convention_has_no_problems()
    {
        Assert.Empty(Problems(typeof(ThingId)));
    }

    [Fact]
    public void The_check_reports_each_way_an_id_can_differ_from_the_convention()
    {
        Assert.Equal(
            [$"{nameof(PlainStructId)} is not a record struct", $"{nameof(PlainStructId)}.New() does not return a version 7 id"],
            Problems(typeof(PlainStructId)));
        Assert.Equal([$"{nameof(MutableId)} is not readonly"], Problems(typeof(MutableId)));
        Assert.Equal([$"{nameof(RecordToStringId)}.ToString() is not the bare GUID"], Problems(typeof(RecordToStringId)));
        Assert.Equal([$"{nameof(NoNewId)} has no public static New()"], Problems(typeof(NoNewId)));
    }

    private static bool IsEntityId(Type type) =>
        type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntityId<>));

    private static IEnumerable<string> Problems(Type id)
    {
        if (!id.IsDefined(typeof(IsReadOnlyAttribute)))
        {
            yield return $"{id.Name} is not readonly";
        }

        // The compiler generates PrintMembers for every record struct and for nothing else.
        var printMembers = id.GetMethod(
            "PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(StringBuilder)]);
        if (printMembers?.IsDefined(typeof(CompilerGeneratedAttribute)) != true)
        {
            yield return $"{id.Name} is not a record struct";
        }

        var newId = id.GetMethod("New", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        if (newId?.ReturnType != id)
        {
            yield return $"{id.Name} has no public static New()";
        }
        else if (ValueOf(newId.Invoke(null, null)!).Version != 7)
        {
            yield return $"{id.Name}.New() does not return a version 7 id";
        }

        var value = Guid.NewGuid();
        if (From(id, value).ToString() != value.ToString())
        {
            yield return $"{id.Name}.ToString() is not the bare GUID";
        }
    }

    private static Guid ValueOf(object id) => (Guid)id.GetType().GetProperty("Value")!.GetValue(id)!;

    private static object From(Type id, Guid value) =>
        id.GetMethod("From", BindingFlags.Public | BindingFlags.Static, [typeof(Guid)])!.Invoke(null, [value])!;

    private readonly struct PlainStructId(Guid value) : IEntityId<PlainStructId>
    {
        public Guid Value { get; } = value;
        public static PlainStructId New() => new(Guid.NewGuid());
        public static PlainStructId From(Guid value) => new(value);
        public override string ToString() => Value.ToString();
    }

    private record struct MutableId(Guid Value) : IEntityId<MutableId>
    {
        public static MutableId New() => new(Guid.CreateVersion7());
        public static MutableId From(Guid value) => new(value);
        public override readonly string ToString() => Value.ToString();
    }

    private readonly record struct RecordToStringId(Guid Value) : IEntityId<RecordToStringId>
    {
        public static RecordToStringId New() => new(Guid.CreateVersion7());
        public static RecordToStringId From(Guid value) => new(value);
    }

    private readonly record struct NoNewId(Guid Value) : IEntityId<NoNewId>
    {
        public static NoNewId From(Guid value) => new(value);
        public override string ToString() => Value.ToString();
    }
}
