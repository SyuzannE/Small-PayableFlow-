using Microsoft.EntityFrameworkCore;
using PayableFlow.Core;

namespace PayableFlow.Api;

public sealed class PayableDbContext(DbContextOptions<PayableDbContext> options) : DbContext(options)
{
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vendor>(e =>
        {
            e.Property(v => v.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(v => v.Name).IsUnique();
        });

        modelBuilder.Entity<Invoice>(e =>
        {
            e.Property(i => i.InvoiceNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(i => new { i.VendorId, i.InvoiceNumber }).IsUnique();
            e.HasOne(i => i.Vendor).WithMany().HasForeignKey(i => i.VendorId);
        });
    }
}
