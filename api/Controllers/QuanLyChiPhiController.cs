using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.ViewModels;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly/ke-toan/chi-phi")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyChiPhiController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyChiPhiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> ChiPhi(string? tuNgay, string? denNgay)
    {
        var (tu, den) = XacDinhKhoangNgay(tuNgay, denNgay);
        var tuUtc = tu.ToUniversalTime();
        var denUtc = den.AddDays(1).ToUniversalTime();

        var ds = await _db.ChiPhiPhatSinhs
            .AsNoTracking()
            .Where(c => c.ThoiGianGui >= tuUtc && c.ThoiGianGui < denUtc)
            .OrderByDescending(c => c.ThoiGianGui)
            .Select(c => new
            {
                c.Id,
                c.DriverId,
                MaTaiXe = c.Driver!.MaTaiXe,
                HoTen = c.Driver!.HoTen,
                c.SoTien,
                c.GiaDau,
                c.SoLit,
                c.GhiChu,
                c.ThoiGianGui,
                c.TrangThai
            })
            .ToListAsync();

        var nhom = ds
            .GroupBy(c => new { c.DriverId, c.MaTaiXe, c.HoTen })
            .Select(g => new ChiPhiTaiXeNhom
            {
                DriverId = g.Key.DriverId,
                MaTaiXe = g.Key.MaTaiXe,
                HoTen = g.Key.HoTen,
                DanhSach = g.Select(c => new ChiPhiItem
                {
                    Id = c.Id,
                    SoTien = c.SoTien,
                    GiaDau = c.GiaDau,
                    SoLit = c.SoLit,
                    GhiChu = c.GhiChu,
                    ThoiGianGui = DateTime.SpecifyKind(c.ThoiGianGui, DateTimeKind.Utc),
                    DaGiaiChi = c.TrangThai == TrangThaiChiPhi.DaGiaiChi
                }).ToList()
            })
            .OrderByDescending(n => n.CoChoGiaiChi)
            .ThenBy(n => n.MaTaiXe)
            .ToList();

        return View(new ChiPhiViewModel { TuNgay = tu, DenNgay = den, TheoTaiXe = nhom });
    }

    [HttpPost("giai-chi/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GiaiChi(int id, string? tuNgay, string? denNgay)
    {
        var cp = await _db.ChiPhiPhatSinhs.FirstOrDefaultAsync(c => c.Id == id);
        if (cp is not null && cp.TrangThai == TrangThaiChiPhi.ChoGiaiChi)
        {
            cp.TrangThai = TrangThaiChiPhi.DaGiaiChi;
            cp.ThoiGianGiaiChi = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ChiPhi), new { tuNgay, denNgay });
    }

    [HttpPost("giai-chi-tuan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GiaiChiTuan(int driverId, string? tuNgay, string? denNgay)
    {
        var (tu, den) = XacDinhKhoangNgay(tuNgay, denNgay);
        var tuUtc = tu.ToUniversalTime();
        var denUtc = den.AddDays(1).ToUniversalTime();

        var dsCanGiai = await _db.ChiPhiPhatSinhs
            .Where(c => c.DriverId == driverId
                && c.TrangThai == TrangThaiChiPhi.ChoGiaiChi
                && c.ThoiGianGui >= tuUtc && c.ThoiGianGui < denUtc)
            .ToListAsync();

        foreach (var cp in dsCanGiai)
        {
            cp.TrangThai = TrangThaiChiPhi.DaGiaiChi;
            cp.ThoiGianGiaiChi = DateTime.UtcNow;
        }

        if (dsCanGiai.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ChiPhi), new { tuNgay, denNgay });
    }

    private static (DateTime Tu, DateTime Den) XacDinhKhoangNgay(string? tuNgay, string? denNgay)
    {
        var homNay = DateTime.Today;
        int lechThu = homNay.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)homNay.DayOfWeek - 1;
        var tu = homNay.AddDays(-lechThu);
        var den = homNay;

        if (DateTime.TryParseExact(tuNgay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tParse))
        {
            tu = tParse.Date;
        }
        if (DateTime.TryParseExact(denNgay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dParse))
        {
            den = dParse.Date;
        }

        return (tu, den);
    }
}