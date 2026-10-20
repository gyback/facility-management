using System.Text.RegularExpressions;
using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.Domain.Tests.Rules;

public sealed partial class RuleCodesTests
{
    [Fact]
    public void Every_code_is_kebab_case()
    {
        Assert.All(RuleCodes.All, code => Assert.Matches(KebabCase(), code));
    }

    [Fact]
    public void Every_code_is_unique()
    {
        Assert.Equal(RuleCodes.All.Count, RuleCodes.All.Distinct().Count());
    }

    [GeneratedRegex("^[a-z]+(-[a-z]+)*$")]
    private static partial Regex KebabCase();
}
