using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Data;

public class Dsw2025TpiContext : DbContext
{

    public Dsw2025TpiContext(DbContextOptions<Dsw2025TpiContext> options) : base(options)
    {

    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Customer> Customers { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>(eb =>
        {
            eb.ToTable("Customers");
            eb.Property(c => c.Id)
            .ValueGeneratedNever();
            eb.Property(c => c.Email)
            .HasMaxLength(320);
            eb.Property(c => c.Name)
            .HasMaxLength(60)
            .IsRequired();
            eb.Property(c => c.PhoneNumber);
        });
        modelBuilder.Entity<Order>(eb =>
        {
            eb.ToTable("Orders");
            eb.Property(o => o.Id)
            .ValueGeneratedNever();
            eb.Property(o => o.Date)
            .HasMaxLength(10)
            .IsRequired();
            eb.Property(o => o.ShippingAddress)
            .HasMaxLength(60);
            eb.Property(o => o.BillingAddress)
            .HasPrecision(15, 2);
            eb.Property(o => o.Notes)
            .HasMaxLength(60);
        });
        modelBuilder.Entity<OrderItem>(eb =>
        {
            eb.ToTable("OrderItems");
            eb.Property(oi => oi.Id)
            .ValueGeneratedNever();
            eb.Property(oi => oi.Quantity)
            .IsRequired();
            eb.Property(oi => oi.UnitPrice)
            .HasPrecision(15, 2)
            .IsRequired();
        });
        modelBuilder.Entity<Product>(eb =>
        {
            eb.ToTable("Products");
            eb.Property(p => p.Id)
            .ValueGeneratedNever();
            eb.Property(p => p.Sku)
            .HasMaxLength(20)
            .IsRequired();
            eb.Property(p => p.Name)
            .HasMaxLength(60)
            .IsRequired();
            eb.Property(p => p.CurrentUnitPrice)
            .HasPrecision(15, 2);
            eb.Property(p => p.InternalCode)
            .HasMaxLength(60);
            eb.Property(p => p.Description)
            .HasMaxLength(200);
            eb.Property(p => p.StockQuantity)
            .HasDefaultValue(0);
        });
    }
}
