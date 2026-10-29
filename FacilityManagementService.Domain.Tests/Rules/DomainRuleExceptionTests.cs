using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Domain.Tests.Rules;

public sealed class DomainRuleExceptionTests
{
    [Fact]
    public void The_code_message_and_parameters_are_kept()
    {
        var exception = new TestRuleException(new Dictionary<string, object?> { ["capacity"] = 12 });

        Assert.Equal("test-rule", exception.Code);
        Assert.Equal("The test rule refused the change.", exception.Message);
        Assert.Equal(12, exception.Parameters["capacity"]);
    }

    [Fact]
    public void Parameters_are_empty_when_none_are_given()
    {
        Assert.Empty(new TestRuleException().Parameters);
    }

    private sealed class TestRuleException(IReadOnlyDictionary<string, object?>? parameters = null)
        : DomainRuleException("test-rule", "The test rule refused the change.", parameters);
}
