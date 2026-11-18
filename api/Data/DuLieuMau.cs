using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Data;

public static class DuLieuMau
{
    private static readonly (string Ma, string HoTen)[] TaiXeMau =
    {
        ("MAU01", "Lê Minh Khang"),
        ("MAU02", "Phạm Quốc Bảo"),
        ("MAU03", "Võ Thanh Tùng"),
        ("MAU04", "Đặng Hoàng Nam")
    };

    private static readonly (string Khach, string LoaiHang, string? GhiChu, int Tuyen, TrangThaiChuyen? TrangThai)[] DonMau =
    {
        ("Công ty TNHH Mẫu An Phát", "Container 40 feet - hàng dệt may", null, 0, TrangThaiChuyen.HoanThanh),
        ("Công ty CP Mẫu Bình Minh", "Container 20 feet - linh kiện điện tử", "Hàng dễ vỡ, giao trước 11h", 1, TrangThaiChuyen.HoanThanh),
        ("Công ty TNHH Mẫu Hoàng Gia", "Container 40 feet - gỗ nội thất", null, 2, TrangThaiChuyen.DangChay),
        ("Công ty CP Mẫu Thái Bình", "Container 20 feet - nguyên liệu nhựa", null, 0, TrangThaiChuyen.DangChay),
        ("Công ty TNHH Mẫu Phương Nam", "Container 40 feet lạnh - thủy sản", "Giữ lạnh, ưu tiên giao sớm", 3, TrangThaiChuyen.DangChay),
        ("Công ty TNHH Mẫu Đại Lợi", "Container 20 feet - hàng gia dụng", null, 4, TrangThaiChuyen.ChuaNhan),
        ("Công ty CP Mẫu Kim Long", "Container 40 feet - máy móc", "Cần xe đầu kéo khung cao", 1, null),
        ("Công ty TNHH Mẫu Sao Việt", "Container 20 feet - thực phẩm khô", null, 2, null)
    };

    public static async Task KhoiTaoAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        await TaoLoaiHinhChuyenAsync(db);
        await TaoQuanLyAsync(db, config, logger);
        await TaoTaiXeMauAsync(db);
        await TaoTuyenDuongAsync(db);
        await TaoDonHangHomNayAsync(db);
    }

    private static async Task TaoQuanLyAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (await db.QuanLys.AnyAsync())
        {
            return;
        }

        var matKhau = config["QuanLy:MatKhauMau"];
        if (string.IsNullOrWhiteSpace(matKhau))
        {
            logger.LogWarning("Chưa cấu hình QuanLy:MatKhauMau trong user-secrets, bỏ qua bước tạo tài khoản quản lý.");
            return;
        }

        db.QuanLys.Add(new QuanLy
        {
            TenDangNhap = (config["QuanLy:TenDangNhap"] ?? "quanly").Trim(),
            MatKhauHash = BCrypt.Net.BCrypt.HashPassword(matKhau),
            HoTen = "Quản lý điều phối"
        });
        await db.SaveChangesAsync();
    }

    private static async Task TaoTaiXeMauAsync(AppDbContext db)
    {
        foreach (var (ma, hoTen) in TaiXeMau)
        {
            if (await db.Drivers.AnyAsync(d => d.MaTaiXe == ma))
            {
                continue;
            }

            db.Drivers.Add(new Driver
            {
                MaTaiXe = ma,
                HoTen = hoTen,
                Username = ma.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"))
            });
        }

        await db.SaveChangesAsync();
    }
    private static async Task TaoLoaiHinhChuyenAsync(AppDbContext db)
    {
        if (await db.LoaiHinhChuyens.AnyAsync())
        {
            return;
        }

        db.LoaiHinhChuyens.AddRange(
            new LoaiHinhChuyen { Ten = "Một chiều", DonGia = 4_000_000m },
            new LoaiHinhChuyen { Ten = "Thả rỗng", DonGia = 4_000_000m },
            new LoaiHinhChuyen { Ten = "Tái xuất", DonGia = 7_200_000m });

        await db.SaveChangesAsync();
    }

    private static async Task TaoTuyenDuongAsync(AppDbContext db)
    {
        if (await db.TuyenDuongs.AnyAsync())
        {
            return;
        }

        db.TuyenDuongs.AddRange(
            new TuyenDuong { TenTuyen = "Cát Lái - Sóng Thần", DiemDi = "Cảng Cát Lái, TP. Thủ Đức", DiemDen = "KCN Sóng Thần, Bình Dương", KhoangCachKm = 30 },
            new TuyenDuong { TenTuyen = "Cát Lái - Long Hậu", DiemDi = "Cảng Cát Lái, TP. Thủ Đức", DiemDen = "KCN Long Hậu, Long An", KhoangCachKm = 34 },
            new TuyenDuong { TenTuyen = "Cát Lái - Biên Hòa", DiemDi = "Cảng Cát Lái, TP. Thủ Đức", DiemDen = "KCN Biên Hòa 2, Đồng Nai", KhoangCachKm = 28 },
            new TuyenDuong { TenTuyen = "Cái Mép - Phú Mỹ", DiemDi = "Cảng Cái Mép, Bà Rịa - Vũng Tàu", DiemDen = "KCN Phú Mỹ, Bà Rịa - Vũng Tàu", KhoangCachKm = 15 },
            new TuyenDuong { TenTuyen = "Hiệp Phước - Tân Tạo", DiemDi = "Cảng Hiệp Phước, Nhà Bè", DiemDen = "KCN Tân Tạo, Bình Tân", KhoangCachKm = 22 });

        await db.SaveChangesAsync();
    }

    private static async Task TaoDonHangHomNayAsync(AppDbContext db)
    {
        var homNay = DateTime.Today;
        if (await db.DonHangs.AnyAsync(d => d.NgayChay == homNay))
        {
            return;
        }

        var tuyens = await db.TuyenDuongs.OrderBy(t => t.Id).ToListAsync();
        var taiXes = await db.Drivers.OrderBy(d => d.Id).ToListAsync();
        if (tuyens.Count == 0 || taiXes.Count == 0)
        {
            return;
        }

        var thuTu = 0;
        foreach (var mau in DonMau)
        {
            thuTu++;

            var don = new DonHang
            {
                MaDon = $"DH{homNay:yyyyMMdd}-{thuTu:00}",
                TenKhachHang = mau.Khach,
                TuyenDuongId = tuyens[mau.Tuyen % tuyens.Count].Id,
                NgayChay = homNay,
                LoaiHang = mau.LoaiHang,
                GhiChu = mau.GhiChu
            };

            if (mau.TrangThai is { } trangThai)
            {
                var phanCong = new PhanCongChuyen
                {
                    DriverId = taiXes[(thuTu - 1) % taiXes.Count].Id,
                    TrangThai = trangThai
                };

                if (trangThai != TrangThaiChuyen.ChuaNhan)
                {
                    phanCong.ThoiGianBatDau = DateTime.UtcNow.AddHours(-3);
                }

                if (trangThai == TrangThaiChuyen.HoanThanh)
                {
                    phanCong.ThoiGianKetThuc = DateTime.UtcNow.AddHours(-1);
                }

                don.PhanCongChuyens.Add(phanCong);
            }

            db.DonHangs.Add(don);
        }

        await db.SaveChangesAsync();
    }
}