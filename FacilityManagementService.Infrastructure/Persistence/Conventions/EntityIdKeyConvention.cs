using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace FacilityManagementService.Infrastructure.Persistence.Conventions;

/// <summary>
/// Marks every primary-key property of a strongly typed id as never generated, since the domain
/// creates ids (ADR-0018).
/// </summary>
/// <remarks>
/// Required, not a detail: if EF Core believes a key is generated, a new entity whose key is
/// already set and that it discovers through a navigation (a new comment added to a tracked
/// thread) is treated as existing and updated instead of inserted.
/// </remarks>
internal sealed class EntityIdKeyConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var keyProperties = modelBuilder.Metadata.GetEntityTypes()
            .Select(entityType => entityType.FindPrimaryKey())
            .OfType<IConventionKey>()
            .SelectMany(key => key.Properties)
            .Where(property => EntityIdConventions.IsEntityId(property.ClrType));

        foreach (var property in keyProperties)
        {
            property.Builder.ValueGenerated(ValueGenerated.Never);
        }
    }
}
