using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.ViewModels;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> TongQuan()
    {
        var homNay = DateTime.Today;
        var batDauUtc = homNay.ToUniversalTime();
        var ketThucUtc = homNay.AddDays(1).ToUniversalTime();

        var don = await LayDonTheoNgayAsync(homNay);
        var chuyens = don.SelectMany(d => d.Chuyens).ToList();

        var vm = new TongQuanViewModel
        {
            Ngay = homNay,
            TongDon = don.Count,
            DonChuaPhanCong = don.Count(d => d.ChuaPhanCong),
            ChuyenChuaNhan = chuyens.Count(c => c.TrangThai == TrangThaiChuyen.ChuaNhan),
            ChuyenDangChay = chuyens.Count(c => c.TrangThai == TrangThaiChuyen.DangChay),
            ChuyenHoanThanh = chuyens.Count(c => c.TrangThai == TrangThaiChuyen.HoanThanh),
            TaiXeDangChay = chuyens
                .Where(c => c.TrangThai == TrangThaiChuyen.DangChay)
                .Select(c => c.MaTaiXe)
                .Distinct()
                .Count(),
            TinChuaDoc = await _db.TinNhans.CountAsync(t => t.NguoiGui == NguoiGuiTin.TaiXe && !t.DaDoc),
            QuetHomNay = await _db.ContainerRecords.CountAsync(c => c.ThoiGianQuet >= batDauUtc && c.ThoiGianQuet < ketThucUtc),
            CanChuY = don.Where(d => d.CanChuY).ToList(),
            DangChay = don.Where(d => d.Chuyens.Any(c => c.TrangThai == TrangThaiChuyen.DangChay)).ToList()
        };

        return View(vm);
    }

    [HttpGet("don-hang")]
    public async Task<IActionResult> DonVaTuyen(string? ngay)
    {
        var ngayXem = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(ngay)
            && DateTime.TryParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayDaChon))
        {
            ngayXem = ngayDaChon.Date;
        }

        var don = await LayDonTheoNgayAsync(ngayXem);

        var theoTuyen = don
            .GroupBy(d => new { d.TenTuyen, d.DiemDi, d.DiemDen, d.KhoangCachKm })
            .Select(g => new TuyenTongHopItem
            {
                TenTuyen = g.Key.TenTuyen,
                DiemDi = g.Key.DiemDi,
                DiemDen = g.Key.DiemDen,
                KhoangCachKm = g.Key.KhoangCachKm,
                SoDon = g.Count(),
                SoDangChay = g.Count(d => d.Chuyens.Any(c => c.TrangThai == TrangThaiChuyen.DangChay)),
                SoHoanThanh = g.Count(d => d.Chuyens.Count > 0 && d.Chuyens.All(c => c.TrangThai == TrangThaiChuyen.HoanThanh))
            })
            .OrderByDescending(t => t.SoDon)
            .ToList();

        return View(new DonVaTuyenViewModel { Ngay = ngayXem, Don = don, TheoTuyen = theoTuyen });
    }

    [HttpGet("tai-xe")]
    public async Task<IActionResult> TaiXe()
    {
        var homNay = DateTime.Today;
        var batDauUtc = homNay.ToUniversalTime();
        var ketThucUtc = homNay.AddDays(1).ToUniversalTime();

        var taiXes = await _db.Drivers
            .AsNoTracking()
            .OrderBy(d => d.MaTaiXe)
            .Select(d => new TaiXeItem
            {
                Id = d.Id,
                MaTaiXe = d.MaTaiXe,
                HoTen = d.HoTen,
                NgayTao = d.NgayTao,
                TongContainerDaQuet = d.ContainerRecords.Count(),
                QuetHomNay = d.ContainerRecords.Count(c => c.ThoiGianQuet >= batDauUtc && c.ThoiGianQuet < ketThucUtc)
            })
            .ToListAsync();

        var chuyens = await _db.PhanCongChuyens
            .AsNoTracking()
            .Where(p => p.DonHang!.NgayChay == homNay)
            .Select(p => new ChuyenTaiXeItem
            {
                DriverId = p.DriverId,
                MaDon = p.DonHang!.MaDon,
                TenTuyen = p.DonHang!.TuyenDuong!.TenTuyen,
                TrangThai = p.TrangThai
            })
            .ToListAsync();

        foreach (var taiXe in taiXes)
        {
            taiXe.Chuyens = chuyens.Where(c => c.DriverId == taiXe.Id).ToList();
        }

        var vm = new TaiXeViewModel
        {
            Ngay = homNay,
            TaiXes = taiXes.OrderBy(t => t.ThuTuUuTien).ThenBy(t => t.MaTaiXe).ToList()
        };

        return View(vm);
    }

    private async Task<List<DonHangItem>> LayDonTheoNgayAsync(DateTime ngay)
    {
        return await _db.DonHangs
            .AsNoTracking()
            .Where(d => d.NgayChay == ngay)
            .OrderBy(d => d.MaDon)
            .Select(d => new DonHangItem
            {
                MaDon = d.MaDon,
                TenKhachHang = d.TenKhachHang,
                TenTuyen = d.TuyenDuong!.TenTuyen,
                DiemDi = d.TuyenDuong!.DiemDi,
                DiemDen = d.TuyenDuong!.DiemDen,
                KhoangCachKm = d.TuyenDuong!.KhoangCachKm,
                LoaiHang = d.LoaiHang,
                GhiChu = d.GhiChu,
                Chuyens = d.PhanCongChuyens
                    .Select(p => new ChuyenItem
                    {
                        MaTaiXe = p.Driver!.MaTaiXe,
                        HoTenTaiXe = p.Driver!.HoTen,
                        TrangThai = p.TrangThai,
                        ThoiGianBatDau = p.ThoiGianBatDau,
                        ThoiGianKetThuc = p.ThoiGianKetThuc
                    })
                    .ToList()
            })
            .ToListAsync();
    }
}