using System.Reflection;

namespace FacilityManagementService.Domain.Tests;

public sealed class DomainDependencyTests
{
    [Theory]
    [InlineData("FacilityManagementService.")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.Extensions.")]
    [InlineData("Microsoft.AspNetCore")]
    public void The_Domain_assembly_does_not_reference(string prefix)
    {
        var references = Assembly.Load("FacilityManagementService.Domain")
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith(prefix, StringComparison.Ordinal) == true);

        Assert.Empty(references);
    }
}
