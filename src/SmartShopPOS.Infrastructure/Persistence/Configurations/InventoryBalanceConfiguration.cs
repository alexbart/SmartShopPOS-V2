using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ToTable("inventory_balances", table =>
            table.HasCheckConstraint("ck_inventory_balances_quantity_non_negative", "\"QuantityOnHand\" >= 0"));
        builder.HasKey(balance => balance.Id);
        builder.HasAlternateKey(balance => new { balance.OrganizationId, balance.Id })
            .HasName("ak_inventory_balances_organization_id_id");

        builder.Property(balance => balance.QuantityOnHand).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(balance => balance.CreatedAt).IsRequired();
        builder.Property(balance => balance.UpdatedAt).IsRequired();

        builder.HasIndex(balance => new { balance.OrganizationId, balance.BranchId, balance.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_inventory_balances_organization_branch_product");

        builder.HasIndex(balance => new { balance.OrganizationId, balance.BranchId })
            .HasDatabaseName("ix_inventory_balances_organization_branch");

        builder.HasIndex(balance => new { balance.OrganizationId, balance.ProductId })
            .HasDatabaseName("ix_inventory_balances_organization_product");

        builder.HasOne(balance => balance.Organization)
            .WithMany()
            .HasForeignKey(balance => balance.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.Branch)
            .WithMany()
            .HasForeignKey(balance => new { balance.OrganizationId, balance.BranchId })
            .HasPrincipalKey(branch => new { branch.OrganizationId, branch.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(balance => balance.Product)
            .WithMany()
            .HasForeignKey(balance => new { balance.OrganizationId, balance.ProductId })
            .HasPrincipalKey(product => new { product.OrganizationId, product.Id })
            .OnDelete(DeleteBehavior.Restrict);

    }
}
