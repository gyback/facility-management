using FacilityManagementService.Domain.Rules;
using FacilityManagementService.Infrastructure.Persistence;

namespace FacilityManagementService.Infrastructure.Tests.Persistence;

public sealed class UniqueIndexRulesTests
{
    [Fact]
    public void Every_mapped_index_has_a_known_rule_code() =>
        Assert.All(UniqueIndexRules.RuleCodeByIndexName.Values, code => Assert.Contains(code, RuleCodes.All));

    [Fact]
    public void An_unknown_index_has_no_rule_code() =>
        Assert.Null(UniqueIndexRules.FindRuleCode("IX_Unknown"));
}
