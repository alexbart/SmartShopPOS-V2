using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.HasAlternateKey(user => new { user.OrganizationId, user.Id })
            .HasName("ak_users_organization_id_id");
        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Property(user => user.UpdatedAt).IsRequired();
        builder.HasIndex(user => new { user.OrganizationId, user.NormalizedEmail })
            .IsUnique()
            .HasDatabaseName("ux_users_organization_normalized_email");
        builder.HasOne(user => user.Organization)
            .WithMany()
            .HasForeignKey(user => user.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}