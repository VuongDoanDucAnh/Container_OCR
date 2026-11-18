using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Controllers;

public record GiaiTrinhCanhBaoDto(string? GhiChu);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CanhBaoController : ControllerBase
{
    private const int GhiChuToiDa = 500;

    private readonly AppDbContext _db;

    public CanhBaoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> DanhSach()
    {
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var ds = await _db.CanhBaoChuyens
            .AsNoTracking()
            .Where(c => c.DriverId == driverId)
            .OrderByDescending(c => c.ThoiGianTao)
            .Take(50)
            .Select(c => new
            {
                c.Id,
                c.PhanCongChuyenId,
                c.Loai,
                c.MoTa,
                c.TrangThai,
                c.GhiChuTaiXe,
                c.GhiChuQuanLy,
                c.ThoiGianTao,
                c.ThoiGianXuLy
            })
            .ToListAsync();

        return Ok(ds.Select(c => new
        {
            c.Id,
            c.PhanCongChuyenId,
            Loai = c.Loai.ToString(),
            c.MoTa,
            TrangThai = c.TrangThai.ToString(),
            c.GhiChuTaiXe,
            c.GhiChuQuanLy,
            ThoiGianTao = DateTime.SpecifyKind(c.ThoiGianTao, DateTimeKind.Utc),
            ThoiGianXuLy = c.ThoiGianXuLy.HasValue ? DateTime.SpecifyKind(c.ThoiGianXuLy.Value, DateTimeKind.Utc) : (DateTime?)null
        }));
    }

    [HttpPost("{id:int}/giai-trinh")]
    public async Task<IActionResult> GiaiTrinh(int id, [FromBody] GiaiTrinhCanhBaoDto? dto)
    {
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        string ghiChu = dto?.GhiChu?.Trim() ?? string.Empty;
        if (ghiChu.Length == 0 || ghiChu.Length > GhiChuToiDa)
        {
            return BadRequest(new
            {
                thanhCong = false,
                loi = "Nội dung giải trình không hợp lệ",
                huongGiaiQuyet = $"Nhập nội dung từ 1 đến {GhiChuToiDa} ký tự",
                maLoi = "GIAI_TRINH_KHONG_HOP_LE"
            });
        }

        var canhBao = await _db.CanhBaoChuyens.FirstOrDefaultAsync(c => c.Id == id && c.DriverId == driverId);
        if (canhBao is null)
        {
            return NotFound(new
            {
                thanhCong = false,
                loi = "Không tìm thấy cảnh báo này",
                huongGiaiQuyet = "Kéo để làm mới danh sách cảnh báo",
                maLoi = "KHONG_TIM_THAY_CANH_BAO"
            });
        }

        if (canhBao.TrangThai != TrangThaiCanhBao.Mo)
        {
            return Conflict(new
            {
                thanhCong = false,
                loi = "Cảnh báo này đã được quản lý xử lý",
                huongGiaiQuyet = "Kéo để làm mới danh sách cảnh báo",
                maLoi = "CANH_BAO_DA_XU_LY"
            });
        }

        canhBao.GhiChuTaiXe = ghiChu;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                thanhCong = false,
                loi = "Cảnh báo vừa được cập nhật bởi người khác",
                huongGiaiQuyet = "Kéo để làm mới rồi giải trình lại nếu cần",
                maLoi = "XUNG_DOT_DU_LIEU"
            });
        }

        return Ok(new { thanhCong = true });
    }
}