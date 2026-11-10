using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Tests.Persistence;

public sealed class CommandResultSerializerTests
{
    [Fact]
    public void Strongly_typed_id_round_trips()
    {
        var id = FacilityId.New();

        var json = CommandResultSerializer.Serialize(id);
        var restored = CommandResultSerializer.Deserialize<FacilityId>(json);

        Assert.Equal(id, restored);
    }

    [Fact]
    public void Unit_round_trips()
    {
        var json = CommandResultSerializer.Serialize(Unit.Value);
        var restored = CommandResultSerializer.Deserialize<Unit>(json);

        Assert.Equal(Unit.Value, restored);
    }

    [Fact]
    public void Command_result_records_round_trip()
    {
        var created = new CreatedFacilityInvitation(
            FacilityInvitationId.New(),
            "raw-token",
            new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

        var json = CommandResultSerializer.Serialize(created);
        var restored = CommandResultSerializer.Deserialize<CreatedFacilityInvitation>(json);

        Assert.Equal(created, restored);
    }

    [Fact]
    public void Outcome_with_polymorphic_warnings_round_trips()
    {
        var warning = new SampleCapacityWarning(
            [new DateOnly(2027, 7, 10), new DateOnly(2027, 7, 11)],
            12);
        var outcome = new Outcome<FacilityId>(FacilityId.New(), [warning]);

        var json = CommandResultSerializer.Serialize(outcome);
        var restored = CommandResultSerializer.Deserialize<Outcome<FacilityId>>(json);

        Assert.Equal(outcome.Value, restored.Value);
        var restoredWarning = Assert.IsType<SampleCapacityWarning>(Assert.Single(restored.Warnings));
        Assert.Equal(warning.Code, restoredWarning.Code);
        Assert.Equal(warning.Message, restoredWarning.Message);
        Assert.Equal(warning.Nights, restoredWarning.Nights);
        Assert.Equal(warning.Capacity, restoredWarning.Capacity);
    }

    public sealed record SampleCapacityWarning(IReadOnlyList<DateOnly> Nights, int Capacity) : Warning
    {
        public override string Code => "capacity-exceeded";

        public override string Message => $"{Nights.Count} nights exceed the facility's capacity of {Capacity}.";
    }
}
