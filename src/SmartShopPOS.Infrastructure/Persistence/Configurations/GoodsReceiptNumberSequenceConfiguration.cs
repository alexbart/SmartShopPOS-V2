using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class GoodsReceiptNumberSequenceConfiguration : IEntityTypeConfiguration<GoodsReceiptNumberSequence>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptNumberSequence> builder)
    {
        builder.ToTable("goods_receipt_number_sequences", table =>
            table.HasCheckConstraint("ck_goods_receipt_number_sequences_last_number_positive", "\"LastNumber\" > 0"));
        builder.HasKey(sequence => sequence.OrganizationId);
        builder.Property(sequence => sequence.LastNumber).IsRequired();
        builder.HasOne(sequence => sequence.Organization).WithMany()
            .HasForeignKey(sequence => sequence.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
