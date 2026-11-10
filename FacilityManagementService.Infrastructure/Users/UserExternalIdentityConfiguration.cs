using FacilityManagementService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacilityManagementService.Infrastructure.Users;

internal sealed class UserExternalIdentityConfiguration : IEntityTypeConfiguration<UserExternalIdentity>
{
    public void Configure(EntityTypeBuilder<UserExternalIdentity> builder)
    {
        builder.ToTable("UserExternalIdentities");

        builder.HasKey(identity => new { identity.Provider, identity.Subject });

        builder.Property(identity => identity.Provider)
            .HasMaxLength(UserExternalIdentity.MaxProviderLength);

        builder.Property(identity => identity.Subject)
            .HasMaxLength(UserExternalIdentity.MaxSubjectLength);
    }
}
