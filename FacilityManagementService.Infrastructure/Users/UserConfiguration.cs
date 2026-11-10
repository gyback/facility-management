using FacilityManagementService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacilityManagementService.Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.DisplayName)
            .HasMaxLength(User.MaxDisplayNameLength);

        builder.HasMany(user => user.ExternalIdentities)
            .WithOne()
            .HasForeignKey(identity => identity.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(user => user.ExternalIdentities)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
