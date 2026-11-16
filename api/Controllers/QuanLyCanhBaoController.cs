using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

public class CanhBaoItem
{
    public int Id { get; set; }
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string MaDon { get; set; } = string.Empty;
    public string TenTuyen { get; set; } = string.Empty;
    public LoaiCanhBao Loai { get; set; }
    public string MoTa { get; set; } = string.Empty;
    public DateTime ThoiGianTao { get; set; }
    public TrangThaiCanhBao TrangThai { get; set; }
    public string? GhiChuTaiXe { get; set; }
    public string? GhiChuQuanLy { get; set; }
    public DateTime? ThoiGianXuLy { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public class CanhBaoViewModel
{
    public int? DriverId { get; set; }
    public bool TatCa { get; set; }
    public List<CanhBaoItem> DanhSach { get; set; } = new();
}

[Route("quanly/canh-bao")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyCanhBaoController : Controller
{
    private const int GhiChuToiDa = 500;

    private readonly AppDbContext _db;

    public QuanLyCanhBaoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? driverId, bool tatCa = false)
    {
        var truyVan = _db.CanhBaoChuyens.AsNoTracking().AsQueryable();

        if (driverId.HasValue)
        {
            truyVan = truyVan.Where(c => c.DriverId == driverId.Value);
        }

        if (!tatCa)
        {
            truyVan = truyVan.Where(c => c.TrangThai == TrangThaiCanhBao.Mo);
        }

        var ds = await truyVan
            .OrderByDescending(c => c.ThoiGianTao)
            .Take(200)
            .Select(c => new
            {
                c.Id,
                MaTaiXe = c.Driver!.MaTaiXe,
                HoTen = c.Driver!.HoTen,
                MaDon = c.PhanCongChuyen!.DonHang!.MaDon,
                TenTuyen = c.PhanCongChuyen!.DonHang!.TuyenDuong!.TenTuyen,
                c.Loai,
                c.MoTa,
                c.ThoiGianTao,
                c.TrangThai,
                c.GhiChuTaiXe,
                c.GhiChuQuanLy,
                c.ThoiGianXuLy,
                c.RowVersion
            })
            .ToListAsync();

        var model = new CanhBaoViewModel
        {
            DriverId = driverId,
            TatCa = tatCa,
            DanhSach = ds.Select(c => new CanhBaoItem
            {
                Id = c.Id,
                MaTaiXe = c.MaTaiXe,
                HoTen = c.HoTen,
                MaDon = c.MaDon,
                TenTuyen = c.TenTuyen,
                Loai = c.Loai,
                MoTa = c.MoTa,
                ThoiGianTao = DateTime.SpecifyKind(c.ThoiGianTao, DateTimeKind.Utc),
                TrangThai = c.TrangThai,
                GhiChuTaiXe = c.GhiChuTaiXe,
                GhiChuQuanLy = c.GhiChuQuanLy,
                ThoiGianXuLy = c.ThoiGianXuLy.HasValue ? DateTime.SpecifyKind(c.ThoiGianXuLy.Value, DateTimeKind.Utc) : null,
                RowVersion = c.RowVersion is null ? string.Empty : Convert.ToBase64String(c.RowVersion)
            }).ToList()
        };

        return View(model);
    }

    [HttpPost("{id:int}/xu-ly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLy(int id, string? ghiChu, string? rowVersion, int? driverId, bool tatCa)
    {
        var canhBao = await _db.CanhBaoChuyens.FirstOrDefaultAsync(c => c.Id == id);
        if (canhBao is null)
        {
            TempData["Loi"] = "Không tìm thấy cảnh báo này";
            return RedirectToAction(nameof(Index), new { driverId, tatCa });
        }

        if (canhBao.TrangThai == TrangThaiCanhBao.DaXuLy)
        {
            TempData["Loi"] = "Cảnh báo này đã được xử lý trước đó";
            return RedirectToAction(nameof(Index), new { driverId, tatCa });
        }

        string ghiChuQuanLy = ghiChu?.Trim() ?? string.Empty;
        if (ghiChuQuanLy.Length == 0 || ghiChuQuanLy.Length > GhiChuToiDa)
        {
            TempData["Loi"] = $"Vui lòng nhập ghi chú xử lý (tối đa {GhiChuToiDa} ký tự)";
            return RedirectToAction(nameof(Index), new { driverId, tatCa });
        }

        if (!string.IsNullOrEmpty(rowVersion))
        {
            try
            {
                _db.Entry(canhBao).Property(c => c.RowVersion).OriginalValue = Convert.FromBase64String(rowVersion);
            }
            catch (FormatException)
            {
                TempData["Loi"] = "Dữ liệu trang đã cũ, vui lòng tải lại trang";
                return RedirectToAction(nameof(Index), new { driverId, tatCa });
            }
        }

        canhBao.TrangThai = TrangThaiCanhBao.DaXuLy;
        canhBao.GhiChuQuanLy = ghiChuQuanLy;
        canhBao.ThoiGianXuLy = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
            TempData["ThongBao"] = "Đã đánh dấu xử lý cảnh báo";
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Loi"] = "Cảnh báo vừa được cập nhật bởi người khác (ví dụ tài xế vừa giải trình). Vui lòng xem lại rồi xử lý lần nữa";
        }

        return RedirectToAction(nameof(Index), new { driverId, tatCa });
    }
}