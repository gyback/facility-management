using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacilityManagementService.Infrastructure.Facilities;

internal sealed class FacilityInvitationConfiguration : IEntityTypeConfiguration<FacilityInvitation>
{
    public void Configure(EntityTypeBuilder<FacilityInvitation> builder)
    {
        builder.ToTable("FacilityInvitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(FacilityInvitation.TokenHashLength);

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        builder.Property(invitation => invitation.AcceptedAt)
            .IsConcurrencyToken();

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(invitation => invitation.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
