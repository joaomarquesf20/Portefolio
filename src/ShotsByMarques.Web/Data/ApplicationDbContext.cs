using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<SessionType> SessionTypes => Set<SessionType>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Gallery> Galleries => Set<Gallery>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PhotoPack> PhotoPacks => Set<PhotoPack>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Booking>()
            .HasOne(b => b.ClientUser)
            .WithMany()
            .HasForeignKey(b => b.ClientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Booking>()
            .HasOne(b => b.SessionType)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.SessionTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Photo>()
            .HasOne(p => p.Gallery)
            .WithMany(g => g.Photos)
            .HasForeignKey(p => p.GalleryId)
            .OnDelete(DeleteBehavior.Cascade);

        // One private gallery per booking (photo delivery to the right client)
        builder.Entity<Gallery>()
            .HasOne(g => g.Booking)
            .WithOne(b => b.Gallery)
            .HasForeignKey<Gallery>(g => g.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Order>()
            .HasOne(o => o.ClientUser)
            .WithMany()
            .HasForeignKey(o => o.ClientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Order>().HasIndex(o => o.StripeSessionId);
        builder.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();

        // Decimal precision for money values
        builder.Entity<SessionType>().Property(s => s.Price).HasPrecision(18, 2);
        builder.Entity<Booking>().Property(b => b.Price).HasPrecision(18, 2);
        builder.Entity<Photo>().Property(p => p.Price).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(18, 2);
        builder.Entity<Order>().Property(o => o.DiscountAmount).HasPrecision(18, 2);
        builder.Entity<OrderItem>().Property(i => i.Price).HasPrecision(18, 2);
        builder.Entity<PhotoPack>().Property(p => p.DiscountPercent).HasPrecision(5, 2);
    }
}
