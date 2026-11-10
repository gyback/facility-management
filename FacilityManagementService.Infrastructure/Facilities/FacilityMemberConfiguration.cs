using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;
using FacilityManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacilityManagementService.Infrastructure.Facilities;

internal sealed class FacilityMemberConfiguration : IEntityTypeConfiguration<FacilityMember>
{
    public void Configure(EntityTypeBuilder<FacilityMember> builder)
    {
        builder.ToTable("FacilityMembers");

        builder.HasKey(member => new { member.FacilityId, member.UserId })
            .HasName(UniqueIndexRules.FacilityMembersPrimaryKey);

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
