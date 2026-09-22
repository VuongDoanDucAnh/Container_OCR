using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly/thong-bao")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyThongBaoController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyThongBaoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public IActionResult Index(int? taiXeId)
    {
        ViewData["TaiXeBanDau"] = taiXeId;
        return View();
    }

    [HttpGet("chua-doc")]
    public async Task<IActionResult> ChuaDoc()
    {
        int soTin = await _db.TinNhans.CountAsync(t => t.NguoiGui == NguoiGuiTin.TaiXe && !t.DaDoc);
        return Json(new { soTin });
    }

    [HttpGet("danh-sach")]
    public async Task<IActionResult> DanhSach()
    {
        var taiXes = await _db.Drivers
            .AsNoTracking()
            .OrderBy(d => d.MaTaiXe)
            .Select(d => new { d.Id, d.MaTaiXe, d.HoTen })
            .ToListAsync();

        var tomTat = await _db.TinNhans
            .AsNoTracking()
            .GroupBy(t => t.DriverId)
            .Select(g => new
            {
                DriverId = g.Key,
                IdCuoi = g.Max(t => t.Id),
                ChuaDoc = g.Count(t => t.NguoiGui == NguoiGuiTin.TaiXe && !t.DaDoc),
                SuCoChuaDoc = g.Count(t => t.NguoiGui == NguoiGuiTin.TaiXe && !t.DaDoc && t.LoaiSuCo != null)
            })
            .ToListAsync();

        var idCuois = tomTat.Select(x => x.IdCuoi).ToList();
        var tinCuois = await _db.TinNhans
            .AsNoTracking()
            .Where(t => idCuois.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        var ketQua = taiXes
            .Select(d =>
            {
                var ts = tomTat.FirstOrDefault(x => x.DriverId == d.Id);

                TinNhan? tinCuoi = null;
                if (ts is not null)
                {
                    tinCuois.TryGetValue(ts.IdCuoi, out tinCuoi);
                }

                return new
                {
                    driverId = d.Id,
                    maTaiXe = d.MaTaiXe,
                    hoTen = d.HoTen,
                    chuaDoc = ts?.ChuaDoc ?? 0,
                    coSuCo = (ts?.SuCoChuaDoc ?? 0) > 0,
                    tinCuoi = tinCuoi?.NoiDung,
                    nguoiGuiCuoi = tinCuoi?.NguoiGui.ToString(),
                    thoiGianCuoi = tinCuoi is not null
                        ? DateTime.SpecifyKind(tinCuoi.ThoiGian, DateTimeKind.Utc)
                        : (DateTime?)null
                };
            })
            .OrderByDescending(x => x.chuaDoc > 0)
            .ThenByDescending(x => x.thoiGianCuoi)
            .ThenBy(x => x.maTaiXe)
            .ToList();

        return Json(ketQua);
    }

    [HttpGet("{driverId:int}/tin")]
    public async Task<IActionResult> Tin(int driverId, int sauId = 0)
    {
        if (!await _db.Drivers.AnyAsync(d => d.Id == driverId))
            return NotFound();

        var tins = await _db.TinNhans
            .Where(t => t.DriverId == driverId && t.Id > sauId)
            .OrderByDescending(t => t.Id)
            .Take(200)
            .ToListAsync();

        tins.Reverse();

        var canDanhDau = tins.Where(t => t.NguoiGui == NguoiGuiTin.TaiXe && !t.DaDoc).ToList();
        if (canDanhDau.Count > 0)
        {
            foreach (var tin in canDanhDau)
            {
                tin.DaDoc = true;
            }

            await _db.SaveChangesAsync();
        }

        return Json(tins.Select(t => new
        {
            id = t.Id,
            nguoiGui = t.NguoiGui.ToString(),
            noiDung = t.NoiDung,
            loaiSuCo = t.LoaiSuCo,
            thoiGian = DateTime.SpecifyKind(t.ThoiGian, DateTimeKind.Utc)
        }));
    }

    [HttpPost("{driverId:int}/gui")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Gui(int driverId, [FromForm] string? noiDung)
    {
        string noi = (noiDung ?? string.Empty).Trim();
        if (noi.Length == 0 || noi.Length > 1000)
            return BadRequest(new { thanhCong = false, loi = "Nội dung phải từ 1 đến 1000 ký tự." });

        if (!await _db.Drivers.AnyAsync(d => d.Id == driverId))
            return NotFound(new { thanhCong = false, loi = "Không tìm thấy tài xế." });

        _db.TinNhans.Add(new TinNhan
        {
            DriverId = driverId,
            NguoiGui = NguoiGuiTin.QuanLy,
            NoiDung = noi
        });
        await _db.SaveChangesAsync();

        return Json(new { thanhCong = true });
    }
}