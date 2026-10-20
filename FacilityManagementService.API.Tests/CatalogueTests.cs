using FacilityManagementService.API.Http.Problems;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Rules;

namespace FacilityManagementService.API.Tests;

/// <summary>
/// The catalogue in the API README lists exactly the codes the API can send, so a problem's
/// <c>type</c> link always leads to an entry and the frontend knows every code (ADR-0017).
/// </summary>
public sealed class CatalogueTests
{
    private static readonly string[] ReadmeLines =
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "README.md"));

    [Fact]
    public void Every_problem_code_is_in_the_catalogue() =>
        Assert.Equal(
            ProblemCodes.All.Concat(RuleCodes.All).Order(StringComparer.Ordinal),
            EntriesUnder("### Problems"));

    [Fact]
    public void Every_warning_code_is_in_the_catalogue() =>
        Assert.Equal(WarningCodes.All.Order(StringComparer.Ordinal), EntriesUnder("### Warnings"));

    [Fact]
    public void Every_validation_code_is_in_the_catalogue() =>
        Assert.Equal(ValidationCodes.All.Order(StringComparer.Ordinal), EntriesUnder("### Validation codes"));

    [Fact]
    public void Every_code_given_to_a_bare_status_is_a_problem_code()
    {
        var codes = Enumerable.Range(400, 200).Select(ProblemCatalogue.CodeForStatus).Distinct();

        Assert.All(codes, code => Assert.Contains(code, ProblemCodes.All));
    }

    [Fact]
    public void Every_problem_code_has_its_own_title() =>
        Assert.Equal(
            ProblemCodes.All.Count,
            ProblemCodes.All.Select(ProblemCatalogue.TitleFor).Distinct().Count());

    /// <summary>The <c>####</c> headings in the section that starts with <paramref name="heading"/>.</summary>
    private static IEnumerable<string> EntriesUnder(string heading) =>
        ReadmeLines
            .SkipWhile(line => line != heading)
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal) &&
                               !line.StartsWith("### ", StringComparison.Ordinal))
            .Where(line => line.StartsWith("#### ", StringComparison.Ordinal))
            .Select(line => line["#### ".Length..].Trim())
            .Order(StringComparer.Ordinal);
}
