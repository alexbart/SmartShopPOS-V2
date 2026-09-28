using Microsoft.EntityFrameworkCore;

namespace SmartShopPOS.Infrastructure.Persistence;

public class SmartShopPosDbContext : DbContext
{
    public SmartShopPosDbContext(DbContextOptions<SmartShopPosDbContext> options)
        : base(options)
    {
    }
}
