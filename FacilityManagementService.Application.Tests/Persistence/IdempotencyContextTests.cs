using FacilityManagementService.Application.Persistence;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Tests.Persistence;

public sealed class IdempotencyContextTests
{
    [Fact]
    public void Default_context_has_no_request_and_no_replay()
    {
        var context = new IdempotencyContext();

        Assert.Null(context.Request);
        Assert.False(context.TryGetReplayedResult<FacilityId>(out _));
    }

    [Fact]
    public void SetRequest_stores_request_without_replay()
    {
        var context = new IdempotencyContext();

        context.SetRequest("user:123", "key-1", "fingerprint-1");

        Assert.Equal(new IdempotencyRequest("user:123", "key-1", "fingerprint-1"), context.Request);
        Assert.False(context.TryGetReplayedResult<FacilityId>(out _));
    }

    [Fact]
    public void SetReplay_returns_deserialized_result()
    {
        var context = new IdempotencyContext();
        var expected = FacilityId.New();
        var json = CommandResultSerializer.Serialize(expected);

        context.SetReplay("user:123", "key-1", "fingerprint-1", json);

        Assert.Equal(new IdempotencyRequest("user:123", "key-1", "fingerprint-1"), context.Request);
        Assert.True(context.TryGetReplayedResult<FacilityId>(out var actual));
        Assert.Equal(expected, actual);
    }
}
