using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.DTOs;
using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/thongbao")]
[Authorize]
public class ThongBaoController : ControllerBase
{
    private static readonly HashSet<string> LoaiSuCoHopLe = new()
    {
        "XE_HONG",
        "KET_XE",
        "SAI_THUNG",
        "TAI_NAN",
        "KHAC"
    };

    private readonly AppDbContext _db;

    public ThongBaoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> LayTin(int sauId = 0)
    {
        int driverId = LayDriverId();

        var tins = await _db.TinNhans
            .Where(t => t.DriverId == driverId && t.Id > sauId)
            .OrderByDescending(t => t.Id)
            .Take(100)
            .ToListAsync();

        tins.Reverse();

        var canDanhDau = tins.Where(t => t.NguoiGui == NguoiGuiTin.QuanLy && !t.DaDoc).ToList();
        if (canDanhDau.Count > 0)
        {
            foreach (var tin in canDanhDau)
            {
                tin.DaDoc = true;
            }

            await _db.SaveChangesAsync();
        }

        return Ok(tins.Select(MapTin));
    }

    [HttpGet("chua-doc")]
    public async Task<IActionResult> DemChuaDoc()
    {
        int driverId = LayDriverId();

        int soTin = await _db.TinNhans.CountAsync(t =>
            t.DriverId == driverId && t.NguoiGui == NguoiGuiTin.QuanLy && !t.DaDoc);

        return Ok(new { soTin });
    }

    [HttpPost]
    public async Task<IActionResult> GuiTin(GuiTinDto dto)
    {
        int driverId = LayDriverId();

        if (!await _db.Drivers.AnyAsync(d => d.Id == driverId))
            return Unauthorized();

        string noiDung = (dto.NoiDung ?? string.Empty).Trim();
        if (noiDung.Length == 0)
        {
            return BadRequest(Loi(
                "Nội dung tin nhắn đang trống",
                "Vui lòng nhập nội dung rồi gửi lại",
                "NOI_DUNG_TRONG"));
        }

        if (noiDung.Length > 1000)
        {
            return BadRequest(Loi(
                "Tin nhắn quá dài",
                "Vui lòng rút gọn còn tối đa 1000 ký tự",
                "NOI_DUNG_QUA_DAI"));
        }

        string? loaiSuCo = string.IsNullOrWhiteSpace(dto.LoaiSuCo)
            ? null
            : dto.LoaiSuCo.Trim().ToUpperInvariant();

        if (loaiSuCo is not null && !LoaiSuCoHopLe.Contains(loaiSuCo))
        {
            return BadRequest(Loi(
                "Loại sự cố không hợp lệ",
                "Vui lòng chọn lại loại sự cố trong danh sách",
                "LOAI_SU_CO_SAI"));
        }

        var tin = new TinNhan
        {
            DriverId = driverId,
            NguoiGui = NguoiGuiTin.TaiXe,
            NoiDung = noiDung,
            LoaiSuCo = loaiSuCo
        };

        _db.TinNhans.Add(tin);
        await _db.SaveChangesAsync();

        return Ok(new { thanhCong = true, tin = MapTin(tin) });
    }

    private int LayDriverId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static object MapTin(TinNhan t) => new
    {
        id = t.Id,
        nguoiGui = t.NguoiGui.ToString(),
        noiDung = t.NoiDung,
        loaiSuCo = t.LoaiSuCo,
        thoiGian = DateTime.SpecifyKind(t.ThoiGian, DateTimeKind.Utc),
        daDoc = t.DaDoc
    };

    private static object Loi(string loi, string huongGiaiQuyet, string maLoi) => new
    {
        thanhCong = false,
        loi,
        huongGiaiQuyet,
        maLoi
    };
}