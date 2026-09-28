using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(userRole => new { userRole.UserId, userRole.RoleId })
            .HasName("pk_user_roles");
        builder.HasIndex(userRole => userRole.OrganizationId).HasDatabaseName("ix_user_roles_organization_id");
        builder.HasOne(userRole => userRole.User)
            .WithMany()
            .HasForeignKey(userRole => new { userRole.OrganizationId, userRole.UserId })
            .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(userRole => userRole.Role)
            .WithMany()
            .HasForeignKey(userRole => new { userRole.OrganizationId, userRole.RoleId })
            .HasPrincipalKey(role => new { role.OrganizationId, role.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}