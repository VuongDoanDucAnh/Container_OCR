using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<ContainerRecord> ContainerRecords => Set<ContainerRecord>();
    public DbSet<QuanLy> QuanLys => Set<QuanLy>();
    public DbSet<TuyenDuong> TuyenDuongs => Set<TuyenDuong>();
    public DbSet<DonHang> DonHangs => Set<DonHang>();
    public DbSet<PhanCongChuyen> PhanCongChuyens => Set<PhanCongChuyen>();
    public DbSet<TinNhan> TinNhans => Set<TinNhan>();
    public DbSet<LoaiHinhChuyen> LoaiHinhChuyens => Set<LoaiHinhChuyen>();

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

        modelBuilder.Entity<QuanLy>()
            .Property(q => q.TenDangNhap)
            .HasMaxLength(50);

        modelBuilder.Entity<QuanLy>()
            .HasIndex(q => q.TenDangNhap)
            .IsUnique();

        modelBuilder.Entity<DonHang>()
            .Property(d => d.MaDon)
            .HasMaxLength(30);

        modelBuilder.Entity<DonHang>()
            .HasIndex(d => d.MaDon)
            .IsUnique();

        modelBuilder.Entity<DonHang>()
            .HasIndex(d => d.NgayChay);

        modelBuilder.Entity<DonHang>()
            .HasOne(d => d.TuyenDuong)
            .WithMany(t => t.DonHangs)
            .HasForeignKey(d => d.TuyenDuongId);

        modelBuilder.Entity<PhanCongChuyen>()
            .HasOne(p => p.DonHang)
            .WithMany(d => d.PhanCongChuyens)
            .HasForeignKey(p => p.DonHangId);

        modelBuilder.Entity<PhanCongChuyen>()
            .HasOne(p => p.Driver)
            .WithMany()
            .HasForeignKey(p => p.DriverId);

        modelBuilder.Entity<PhanCongChuyen>()
            .Property(p => p.TrangThai)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<TinNhan>()
            .HasOne(t => t.Driver)
            .WithMany()
            .HasForeignKey(t => t.DriverId);

        modelBuilder.Entity<TinNhan>()
            .Property(t => t.NguoiGui)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<TinNhan>()
            .HasIndex(t => new { t.DriverId, t.ThoiGian });

        modelBuilder.Entity<ContainerRecord>()
            .HasOne(c => c.PhanCongChuyen)
            .WithMany(p => p.ContainerRecords)
            .HasForeignKey(c => c.PhanCongChuyenId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ContainerRecord>()
            .HasOne(c => c.LoaiHinhChuyen)
            .WithMany()
            .HasForeignKey(c => c.LoaiHinhChuyenId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LoaiHinhChuyen>()
            .Property(l => l.DonGia)
            .HasColumnType("decimal(18,0)");
    }
}