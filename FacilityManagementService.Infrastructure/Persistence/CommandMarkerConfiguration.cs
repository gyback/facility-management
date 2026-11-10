using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FacilityManagementService.Infrastructure.Persistence;

internal sealed class CommandMarkerConfiguration : IEntityTypeConfiguration<CommandMarker>
{
    public const string PrimaryKeyName = "PK_CommandMarkers";
    public const string ExpiresAtIndexName = "IX_CommandMarkers_ExpiresAt";

    public void Configure(EntityTypeBuilder<CommandMarker> builder)
    {
        builder.ToTable("CommandMarkers");

        builder.HasKey(marker => new { marker.Scope, marker.Key })
            .HasName(PrimaryKeyName);

        builder.Property(marker => marker.Scope)
            .HasMaxLength(CommandMarker.MaxScopeLength);

        builder.Property(marker => marker.Key)
            .HasMaxLength(CommandMarker.MaxKeyLength);

        builder.Property(marker => marker.Fingerprint)
            .HasMaxLength(CommandMarker.FingerprintLength);

        builder.HasIndex(marker => marker.ExpiresAt)
            .HasDatabaseName(ExpiresAtIndexName);
    }
}
