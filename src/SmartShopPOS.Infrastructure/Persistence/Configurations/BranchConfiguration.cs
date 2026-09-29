using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(branch => branch.Id);
        builder.HasAlternateKey(branch => new { branch.OrganizationId, branch.Id })
            .HasName("ak_branches_organization_id_id");
        builder.Property(branch => branch.Code).HasMaxLength(32).IsRequired();
        builder.Property(branch => branch.Name).HasMaxLength(160).IsRequired();
        builder.Property(branch => branch.IsActive).IsRequired();
        builder.Property(branch => branch.CreatedAt).IsRequired();
        builder.Property(branch => branch.UpdatedAt).IsRequired();
        builder.HasIndex(branch => new { branch.OrganizationId, branch.Code })
            .IsUnique()
            .HasDatabaseName("ux_branches_organization_id_code");
        builder.HasOne(branch => branch.Organization)
            .WithMany()
            .HasForeignKey(branch => branch.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}