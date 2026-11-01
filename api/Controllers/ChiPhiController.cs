using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.DTOs;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/chiphi")]
[Authorize]
public class ChiPhiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IGeminiHoaDonClient _hoaDonClient;

    public ChiPhiController(AppDbContext db, IGeminiHoaDonClient hoaDonClient)
    {
        _db = db;
        _hoaDonClient = hoaDonClient;
    }

    [HttpGet]
    public async Task<IActionResult> DanhSach()
    {
        int driverId = LayDriverId();
        var ds = await _db.ChiPhiPhatSinhs
            .Where(c => c.DriverId == driverId)
            .OrderByDescending(c => c.ThoiGianGui).Take(50)
            .Select(c => new
            {
                id = c.Id,
                soTien = c.SoTien,
                giaDau = c.GiaDau,
                soLit = c.SoLit,
                ghiChu = c.GhiChu,
                thoiGianGui = DateTime.SpecifyKind(c.ThoiGianGui, DateTimeKind.Utc),
                daGiaiChi = c.TrangThai == TrangThaiChiPhi.DaGiaiChi
            }).ToListAsync();
        return Ok(ds);
    }

    [HttpPost]
    public async Task<IActionResult> Gui(GuiChiPhiDto dto)
    {
        int driverId = LayDriverId();
        if (!await _db.Drivers.AnyAsync(d => d.Id == driverId)) return Unauthorized();

        if (dto.SoTien <= 0 || dto.GiaDau <= 0 || dto.SoLit <= 0)
        {
            return BadRequest(new
            {
                thanhCong = false,
                loi = "Vui lòng nhập đầy đủ số tiền, giá dầu và số lít lớn hơn 0",
                huongGiaiQuyet = "Kiểm tra lại các ô nhập",
                maLoi = "DU_LIEU_KHONG_HOP_LE"
            });
        }

        var chiPhi = new ChiPhiPhatSinh
        {
            DriverId = driverId,
            SoTien = dto.SoTien,
            GiaDau = dto.GiaDau,
            SoLit = dto.SoLit,
            GhiChu = string.IsNullOrWhiteSpace(dto.GhiChu) ? null : dto.GhiChu.Trim()
        };

        _db.ChiPhiPhatSinhs.Add(chiPhi);
        await _db.SaveChangesAsync();

        return Ok(new { thanhCong = true });
    }

    [HttpPost("doc-hoa-don")]
    public async Task<IActionResult> DocHoaDon(IFormFile anh)
    {
        if (anh is null || anh.Length == 0)
        {
            return BadRequest(new DocHoaDonResponseDto
            {
                ThanhCong = false,
                Loi = "Chưa có ảnh hóa đơn được gửi lên",
                HuongGiaiQuyet = "Vui lòng chọn hoặc chụp ảnh hóa đơn nhiên liệu",
                MaLoi = "CHUA_CO_ANH"
            });
        }

        using var ms = new MemoryStream();
        await anh.CopyToAsync(ms);
        var anhBytes = ms.ToArray();

        DocHoaDonKetQuaAi ketQuaAi;
        try
        {
            ketQuaAi = await _hoaDonClient.DocHoaDonAsync(anhBytes, anh.ContentType);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new DocHoaDonResponseDto
            {
                ThanhCong = false,
                Loi = $"Chi tiết lỗi: {ex.Message}",
                HuongGiaiQuyet = "Kiểm tra API Key hoặc kết nối mạng",
                MaLoi = "LOI_DICH_VU_AI"
            });
        }

        if (!ketQuaAi.LaHoaDon)
        {
            return Ok(new DocHoaDonResponseDto
            {
                ThanhCong = false,
                LaHoaDon = false,
                Loi = "Ảnh gửi lên không phải là hóa đơn nhiên liệu",
                HuongGiaiQuyet = "Hãy chụp lại rõ nét hóa đơn cây xăng (xăng/dầu diesel)",
                MaLoi = "KHONG_PHAI_HOA_DON"
            });
        }

        bool khopSoLieu = true;
        string? canhBao = null;

        if (ketQuaAi.SoLit.HasValue && ketQuaAi.DonGia.HasValue && ketQuaAi.ThanhTien.HasValue)
        {
            var tich = ketQuaAi.SoLit.Value * ketQuaAi.DonGia.Value;
            var lech = Math.Abs(tich - ketQuaAi.ThanhTien.Value);

            if (lech > 1000m)
            {
                khopSoLieu = false;
                canhBao = $"Số tiền tính toán ({tich:N0} đ) lệch so với tổng tiền trên hóa đơn ({ketQuaAi.ThanhTien.Value:N0} đ). Vui lòng kiểm tra kỹ lại.";
            }
        }

        return Ok(new DocHoaDonResponseDto
        {
            ThanhCong = true,
            LaHoaDon = true,
            SoLit = ketQuaAi.SoLit,
            DonGia = ketQuaAi.DonGia,
            ThanhTien = ketQuaAi.ThanhTien,
            LoaiNhienLieu = ketQuaAi.LoaiNhienLieu,
            NgayGio = ketQuaAi.NgayGio,
            TenCuaHang = ketQuaAi.TenCuaHang,
            KhopSoLieu = khopSoLieu,
            CanhBao = canhBao,
            TruongNghiNgo = ketQuaAi.TruongNghiNgo
        });
    }

    private int LayDriverId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}