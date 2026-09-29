using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", table =>
        {
            table.HasCheckConstraint("ck_stock_movements_quantity_positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("ck_stock_movements_type_valid", "\"MovementType\" BETWEEN 1 AND 6");
        });
        builder.HasKey(movement => movement.Id);
        builder.HasAlternateKey(movement => new { movement.OrganizationId, movement.Id })
            .HasName("ak_stock_movements_organization_id_id");

        builder.Property(movement => movement.MovementType).HasConversion<int>().IsRequired();
        builder.Property(movement => movement.Quantity).HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(movement => movement.OccurredAt).IsRequired();
        builder.Property(movement => movement.ReferenceType).HasMaxLength(100);
        builder.Property(movement => movement.Reason).HasMaxLength(500);
        builder.Property(movement => movement.CreatedAt).IsRequired();
        builder.Property(movement => movement.UpdatedAt).IsRequired();

        builder.HasIndex(movement => new { movement.OrganizationId, movement.BranchId, movement.ProductId, movement.OccurredAt })
            .HasDatabaseName("ix_stock_movements_organization_branch_product_occurred");

        builder.HasIndex(movement => new { movement.OrganizationId, movement.BranchId, movement.OccurredAt })
            .HasDatabaseName("ix_stock_movements_organization_branch_occurred");

        builder.HasIndex(movement => new { movement.OrganizationId, movement.ProductId, movement.OccurredAt })
            .HasDatabaseName("ix_stock_movements_organization_product_occurred");

        builder.HasOne(movement => movement.Organization)
            .WithMany()
            .HasForeignKey(movement => movement.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Branch)
            .WithMany()
            .HasForeignKey(movement => new { movement.OrganizationId, movement.BranchId })
            .HasPrincipalKey(branch => new { branch.OrganizationId, branch.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Product)
            .WithMany()
            .HasForeignKey(movement => new { movement.OrganizationId, movement.ProductId })
            .HasPrincipalKey(product => new { product.OrganizationId, product.Id })
            .OnDelete(DeleteBehavior.Restrict);

    }
}
