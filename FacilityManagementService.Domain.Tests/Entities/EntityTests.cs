using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.Domain.Tests.Entities;

public sealed class EntityTests
{
    [Fact]
    public void An_entity_keeps_the_id_it_was_created_with()
    {
        var id = ThingId.New();

        Assert.Equal(id, new Thing(id).Id);
    }

    [Fact]
    public void An_entity_rejects_a_default_id()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Thing(default));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void An_entity_rejects_an_empty_id()
    {
        Assert.Throws<ArgumentException>(() => new Thing(ThingId.From(Guid.Empty)));
    }

    [Fact]
    public void New_returns_distinct_version_7_ids()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => ThingId.New()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.Equal(7, id.Value.Version));
    }

    [Fact]
    public void From_wraps_the_value_without_checking_it()
    {
        var value = Guid.NewGuid();

        Assert.Equal(value, ThingId.From(value).Value);
        Assert.Equal(Guid.Empty, ThingId.From(Guid.Empty).Value);
    }

    [Fact]
    public void An_id_converts_to_the_bare_guid()
    {
        var value = Guid.NewGuid();

        Assert.Equal(value.ToString(), ThingId.From(value).ToString());
    }

    [Fact]
    public void Ids_with_the_same_value_are_equal()
    {
        var value = Guid.NewGuid();

        Assert.Equal(ThingId.From(value), ThingId.From(value));
        Assert.True(ThingId.From(value) == ThingId.From(value));
    }

    private sealed class Thing(ThingId id) : AggregateRoot<ThingId>(id);
}

public readonly record struct ThingId(Guid Value) : IEntityId<ThingId>
{
    public static ThingId New() => new(Guid.CreateVersion7());
    public static ThingId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
