using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/trangchu")]
[Authorize]
public class TrangChuController : ControllerBase
{
    private readonly AppDbContext _db;

    public TrangChuController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> LayTrangChu()
    {
        int driverId = LayDriverId();

        var driver = await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId);
        if (driver is null) return Unauthorized();

        var homNay = DateTime.Today;
        var batDauUtc = homNay.ToUniversalTime();
        var ketThucUtc = homNay.AddDays(1).ToUniversalTime();

        var chuyenTho = await _db.PhanCongChuyens
            .AsNoTracking()
            .Where(p => p.DriverId == driverId && p.DonHang!.NgayChay == homNay)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.TrangThai,
                p.ThoiGianBatDau,
                p.ThoiGianKetThuc,
                MaDon = p.DonHang!.MaDon,
                KhachHang = p.DonHang!.TenKhachHang,
                LoaiHang = p.DonHang!.LoaiHang,
                GhiChu = p.DonHang!.GhiChu,
                TenTuyen = p.DonHang!.TuyenDuong!.TenTuyen,
                DiemDi = p.DonHang!.TuyenDuong!.DiemDi,
                DiemDen = p.DonHang!.TuyenDuong!.DiemDen,
                KhoangCachKm = p.DonHang!.TuyenDuong!.KhoangCachKm
            })
            .ToListAsync();

        var chuyenHomNay = chuyenTho
            .Select(c => new
            {
                phanCongId = c.Id,
                maDon = c.MaDon,
                khachHang = c.KhachHang,
                loaiHang = c.LoaiHang,
                ghiChu = c.GhiChu,
                tenTuyen = c.TenTuyen,
                diemDi = c.DiemDi,
                diemDen = c.DiemDen,
                khoangCachKm = c.KhoangCachKm,
                trangThai = c.TrangThai.ToString(),
                thoiGianBatDau = DaDatUtc(c.ThoiGianBatDau),
                thoiGianKetThuc = DaDatUtc(c.ThoiGianKetThuc)
            })
            .ToList();

        int soQuetHomNay = await _db.ContainerRecords.CountAsync(c =>
            c.DriverId == driverId && c.ThoiGianQuet >= batDauUtc && c.ThoiGianQuet < ketThucUtc);

        int soTinChuaDoc = await _db.TinNhans.CountAsync(t =>
            t.DriverId == driverId && t.NguoiGui == NguoiGuiTin.QuanLy && !t.DaDoc);

        return Ok(new
        {
            maTaiXe = driver.MaTaiXe,
            hoTen = driver.HoTen,
            ngay = homNay.ToString("yyyy-MM-dd"),
            soQuetHomNay,
            soTinChuaDoc,
            chuyenHomNay
        });
    }

    [HttpPost("chuyen/{id:int}/bat-dau")]
    public async Task<IActionResult> BatDau(int id)
    {
        int driverId = LayDriverId();

        var chuyen = await _db.PhanCongChuyens.FirstOrDefaultAsync(p => p.Id == id && p.DriverId == driverId);
        if (chuyen is null)
        {
            return NotFound(Loi(
                "Không tìm thấy chuyến này",
                "Kéo để làm mới danh sách chuyến rồi thử lại",
                "KHONG_TIM_THAY_CHUYEN"));
        }

        if (chuyen.TrangThai != TrangThaiChuyen.ChuaNhan)
        {
            return Conflict(Loi(
                "Chuyến này đã được nhận trước đó",
                "Kéo để làm mới danh sách chuyến",
                "SAI_TRANG_THAI"));
        }

        chuyen.TrangThai = TrangThaiChuyen.DangChay;
        chuyen.ThoiGianBatDau = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { thanhCong = true, trangThai = chuyen.TrangThai.ToString() });
    }

    [HttpPost("chuyen/{id:int}/hoan-thanh")]
    public async Task<IActionResult> HoanThanh(int id)
    {
        int driverId = LayDriverId();

        var chuyen = await _db.PhanCongChuyens.FirstOrDefaultAsync(p => p.Id == id && p.DriverId == driverId);
        if (chuyen is null)
        {
            return NotFound(Loi(
                "Không tìm thấy chuyến này",
                "Kéo để làm mới danh sách chuyến rồi thử lại",
                "KHONG_TIM_THAY_CHUYEN"));
        }

        if (chuyen.TrangThai != TrangThaiChuyen.DangChay)
        {
            return Conflict(Loi(
                "Chuyến này chưa bắt đầu hoặc đã hoàn thành",
                "Kéo để làm mới danh sách chuyến",
                "SAI_TRANG_THAI"));
        }

        chuyen.TrangThai = TrangThaiChuyen.HoanThanh;
        chuyen.ThoiGianKetThuc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { thanhCong = true, trangThai = chuyen.TrangThai.ToString() });
    }

    private int LayDriverId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static DateTime? DaDatUtc(DateTime? giaTri) =>
        giaTri.HasValue ? DateTime.SpecifyKind(giaTri.Value, DateTimeKind.Utc) : null;

    private static object Loi(string loi, string huongGiaiQuyet, string maLoi) => new
    {
        thanhCong = false,
        loi,
        huongGiaiQuyet,
        maLoi
    };
}