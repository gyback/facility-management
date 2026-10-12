using System.Reflection;
using System.Text.Json;

namespace FacilityManagementService.Domain.Tests;

public sealed class DomainDependencyTests
{
    private const string DomainAssembly = "FacilityManagementService.Domain";

    [Theory]
    [InlineData("FacilityManagementService.")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.Extensions.")]
    [InlineData("Microsoft.AspNetCore")]
    public void The_Domain_assembly_does_not_reference(string prefix)
    {
        var references = Assembly.Load(DomainAssembly)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.Empty(StartingWith(prefix, references));
    }

    // The compiler only emits references the IL uses, so the check above misses a
    // project or package reference added to Domain.csproj before anything uses it.
    // The deps.json lists what the project declares.
    [Theory]
    [InlineData("FacilityManagementService.")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.Extensions.")]
    [InlineData("Microsoft.AspNetCore")]
    public void The_Domain_project_does_not_depend_on(string prefix)
    {
        Assert.Empty(StartingWith(prefix, DeclaredDomainDependencies()));
    }

    private static IEnumerable<string?> StartingWith(string prefix, IEnumerable<string?> names) =>
        names.Where(name => name?.StartsWith(prefix, StringComparison.Ordinal) == true);

    private static IEnumerable<string> DeclaredDomainDependencies()
    {
        var depsFile = Path.Combine(
            AppContext.BaseDirectory,
            $"{Assembly.GetExecutingAssembly().GetName().Name}.deps.json");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));

        var domain = deps.RootElement.GetProperty("targets")
            .EnumerateObject()
            .SelectMany(target => target.Value.EnumerateObject())
            .Single(library => library.Name.StartsWith($"{DomainAssembly}/", StringComparison.Ordinal));

        return domain.Value.TryGetProperty("dependencies", out var dependencies)
            ? dependencies.EnumerateObject().Select(dependency => dependency.Name).ToList()
            : [];
    }
}
