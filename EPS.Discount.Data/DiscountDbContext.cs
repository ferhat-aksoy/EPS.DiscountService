using Microsoft.EntityFrameworkCore;

namespace EPS.Discount.Data;

public class DiscountDbContext : DbContext
{
    public DiscountDbContext(DbContextOptions<DiscountDbContext> opts) : base(opts) { }

    public DbSet<DiscountCode> DiscountCodes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiscountCode>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Length).IsRequired();
        });
    }
}
