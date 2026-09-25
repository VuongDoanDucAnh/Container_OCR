using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.ViewModels;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly/ke-toan")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyKeToanController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyKeToanController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> KeToan(string? tuNgay, string? denNgay)
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

        var tuUtc = tu.ToUniversalTime();
        var denUtc = den.AddDays(1).ToUniversalTime();

        var dsLoaiHinh = await _db.LoaiHinhChuyens.AsNoTracking().OrderBy(l => l.Id).ToListAsync();

        var banGhi = await _db.ContainerRecords
            .AsNoTracking()
            .Where(c => c.ChecksumHopLe && c.ThoiGianQuet >= tuUtc && c.ThoiGianQuet < denUtc)
            .Select(c => new
            {
                c.DriverId,
                c.LoaiHinhChuyenId,
                DonGia = c.LoaiHinhChuyen != null ? c.LoaiHinhChuyen.DonGia : 0m
            })
            .ToListAsync();

        var taiXes = await _db.Drivers.AsNoTracking().OrderBy(d => d.MaTaiXe).ToListAsync();

        var bangLuong = taiXes
            .Select(tx =>
            {
                var cua = banGhi.Where(b => b.DriverId == tx.Id).ToList();
                return new LuongTaiXeItem
                {
                    MaTaiXe = tx.MaTaiXe,
                    HoTen = tx.HoTen,
                    SoContainer = cua.Count(b => b.LoaiHinhChuyenId != null),
                    SoChuaPhanLoai = cua.Count(b => b.LoaiHinhChuyenId == null),
                    TongTien = cua.Where(b => b.LoaiHinhChuyenId != null).Sum(b => b.DonGia)
                };
            })
            .Where(x => x.SoContainer > 0 || x.SoChuaPhanLoai > 0)
            .OrderByDescending(x => x.TongTien)
            .ToList();

        return View(new KeToanViewModel { TuNgay = tu, DenNgay = den, DsLoaiHinh = dsLoaiHinh, BangLuong = bangLuong });
    }

    [HttpPost("gia-cuoc/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatGiaCuoc(int id, decimal donGiaMoi)
    {
        if (donGiaMoi < 0) return RedirectToAction(nameof(KeToan));

        var lh = await _db.LoaiHinhChuyens.FirstOrDefaultAsync(l => l.Id == id);
        if (lh is null) return NotFound();

        lh.DonGia = donGiaMoi;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(KeToan));
    }
}