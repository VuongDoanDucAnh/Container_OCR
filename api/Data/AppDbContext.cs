using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<ContainerRecord> ContainerRecords => Set<ContainerRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Driver>()
            .HasIndex(d => d.MaTaiXe)
            .IsUnique();

        modelBuilder.Entity<Driver>()
            .HasIndex(d => d.Username)
            .IsUnique();

        modelBuilder.Entity<ContainerRecord>()
            .HasOne(c => c.Driver)
            .WithMany(d => d.ContainerRecords)
            .HasForeignKey(c => c.DriverId);
    }
}
