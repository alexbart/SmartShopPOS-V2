using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class UserBranchConfiguration : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> builder)
    {
        builder.ToTable("user_branches");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.OrganizationId).IsRequired();
        builder.Property(assignment => assignment.UserId).IsRequired();
        builder.Property(assignment => assignment.BranchId).IsRequired();
        builder.Property(assignment => assignment.CreatedAt).IsRequired();
        builder.Property(assignment => assignment.DeactivatedAt);
        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.UserId, assignment.BranchId })
            .IsUnique()
            .HasFilter("\"DeactivatedAt\" IS NULL")
            .HasDatabaseName("ux_user_branches_active_assignment");
        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.UserId })
            .HasDatabaseName("ix_user_branches_organization_user");
        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.BranchId })
            .HasDatabaseName("ix_user_branches_organization_branch");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.OrganizationId, assignment.UserId })
            .HasPrincipalKey(user => new { user.OrganizationId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.OrganizationId, assignment.BranchId })
            .HasPrincipalKey(branch => new { branch.OrganizationId, branch.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}