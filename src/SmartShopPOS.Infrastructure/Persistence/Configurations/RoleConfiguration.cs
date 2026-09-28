using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);
        builder.HasAlternateKey(role => new { role.OrganizationId, role.Id })
            .HasName("ak_roles_organization_id_id");
        builder.Property(role => role.Name).HasMaxLength(100).IsRequired();
        builder.Property(role => role.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(500);
        builder.Property(role => role.IsActive).IsRequired();
        builder.Property(role => role.CreatedAt).IsRequired();
        builder.Property(role => role.UpdatedAt).IsRequired();
        builder.HasIndex(role => new { role.OrganizationId, role.NormalizedName })
            .IsUnique()
            .HasDatabaseName("ux_roles_organization_normalized_name");
        builder.HasOne(role => role.Organization)
            .WithMany()
            .HasForeignKey(role => role.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}