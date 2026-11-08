using FacilityManagementService.Domain.Facilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacilityManagementService.Infrastructure.Facilities;

internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities");

        builder.HasKey(facility => facility.Id);

        builder.Property(facility => facility.Name)
            .HasMaxLength(Facility.MaxNameLength);

        builder.HasMany(facility => facility.Members)
            .WithOne()
            .HasForeignKey(member => member.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(facility => facility.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
