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
[Route("api/trangchu")]
[Authorize]
public class TrangChuController : ControllerBase
{
    private const long GioiHanAnhByte = 10 * 1024 * 1024;
    private const int OdoToiDa = 9_999_999;
    private const string ThuMucAnhOdo = "odo";

    private readonly AppDbContext _db;
    private readonly IGeminiOdoClient _odoClient;
    private readonly IAnhLuuTruService _anhLuuTru;

    public TrangChuController(AppDbContext db, IGeminiOdoClient odoClient, IAnhLuuTruService anhLuuTru)
    {
        _db = db;
        _odoClient = odoClient;
        _anhLuuTru = anhLuuTru;
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

    [HttpPost("doc-odo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DocOdo(IFormFile anh)
    {
        if (anh is null || anh.Length == 0)
        {
            return BadRequest(new DocOdoResponseDto
            {
                ThanhCong = false,
                Loi = "Chưa có ảnh đồng hồ được gửi lên",
                HuongGiaiQuyet = "Vui lòng chụp ảnh đồng hồ công-tơ-mét",
                MaLoi = "CHUA_CO_ANH"
            });
        }

        if (anh.Length > GioiHanAnhByte)
        {
            return BadRequest(new DocOdoResponseDto
            {
                ThanhCong = false,
                Loi = "Ảnh quá lớn",
                HuongGiaiQuyet = "Vui lòng chụp lại với độ phân giải thấp hơn",
                MaLoi = "ANH_QUA_LON"
            });
        }

        using var ms = new MemoryStream();
        await anh.CopyToAsync(ms);
        var anhBytes = ms.ToArray();

        DocOdoKetQuaAi ketQuaAi;
        try
        {
            ketQuaAi = await _odoClient.DocOdoAsync(anhBytes, anh.ContentType);
        }
        catch (Exception)
        {
            var anhIdKhiLoi = await _anhLuuTru.TaiLenAsync(anhBytes, ThuMucAnhOdo);
            return StatusCode(503, new DocOdoResponseDto
            {
                ThanhCong = false,
                AnhPublicId = anhIdKhiLoi,
                Loi = "Hệ thống đọc đồng hồ đang tạm gián đoạn",
                HuongGiaiQuyet = "Bạn có thể nhập tay số ODO đang hiển thị trên đồng hồ",
                MaLoi = "LOI_DICH_VU_AI"
            });
        }

        if (!ketQuaAi.LaDongHoOdo)
        {
            return Ok(new DocOdoResponseDto
            {
                ThanhCong = false,
                LaDongHoOdo = false,
                Loi = "Ảnh gửi lên không phải đồng hồ công-tơ-mét",
                HuongGiaiQuyet = "Hãy chụp rõ mặt đồng hồ, thấy đủ dãy số ODO",
                MaLoi = "KHONG_PHAI_DONG_HO"
            });
        }

        var anhPublicId = await _anhLuuTru.TaiLenAsync(anhBytes, ThuMucAnhOdo);

        var canhBao = new List<string>();
        if (!ketQuaAi.Odo.HasValue)
        {
            canhBao.Add("Không đọc rõ số ODO, vui lòng nhập tay");
        }
        else if (ketQuaAi.TruongNghiNgo.Count > 0)
        {
            canhBao.Add("Số ODO có thể chưa chính xác, vui lòng kiểm tra lại");
        }

        if (string.Equals(ketQuaAi.DonVi, "mi", StringComparison.OrdinalIgnoreCase))
        {
            canhBao.Add("Đồng hồ đang hiển thị dặm, vui lòng đối chiếu lại đơn vị km");
        }

        return Ok(new DocOdoResponseDto
        {
            ThanhCong = true,
            LaDongHoOdo = true,
            Odo = ketQuaAi.Odo,
            DonVi = ketQuaAi.DonVi,
            AnhPublicId = anhPublicId,
            CanhBao = canhBao.Count == 0 ? null : string.Join(". ", canhBao),
            TruongNghiNgo = ketQuaAi.TruongNghiNgo
        });
    }

    [HttpPost("chuyen/{id:int}/bat-dau")]
    public async Task<IActionResult> BatDau(int id, [FromBody] BatDauChuyenDto? dto)
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

        if (dto?.OdoBatDau is not int odoBatDau || !OdoHopLe(odoBatDau))
        {
            return BadRequest(Loi(
                "Vui lòng chụp và xác nhận số ODO lúc bắt đầu chuyến",
                "Chụp ảnh đồng hồ công-tơ-mét rồi thử lại",
                "THIEU_ODO_BAT_DAU"));
        }

        if (!PublicIdOdoHopLe(dto.AnhOdoPublicId))
        {
            return BadRequest(Loi(
                "Ảnh ODO không hợp lệ",
                "Vui lòng chụp lại ảnh đồng hồ",
                "ANH_ODO_KHONG_HOP_LE"));
        }

        chuyen.TrangThai = TrangThaiChuyen.DangChay;
        chuyen.ThoiGianBatDau = DateTime.UtcNow;
        chuyen.OdoBatDau = odoBatDau;
        chuyen.AnhOdoBatDauPublicId = string.IsNullOrWhiteSpace(dto.AnhOdoPublicId)
            ? null
            : dto.AnhOdoPublicId.Trim();
        await _db.SaveChangesAsync();

        return Ok(new { thanhCong = true, trangThai = chuyen.TrangThai.ToString() });
    }

    [HttpPost("chuyen/{id:int}/hoan-thanh")]
    public async Task<IActionResult> HoanThanh(int id, [FromBody] HoanThanhChuyenDto? dto)
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

        if (dto?.OdoKetThuc is not int odoKetThuc || !OdoHopLe(odoKetThuc))
        {
            return BadRequest(Loi(
                "Vui lòng chụp và xác nhận số ODO lúc kết thúc chuyến",
                "Chụp ảnh đồng hồ công-tơ-mét rồi thử lại",
                "THIEU_ODO_KET_THUC"));
        }

        if (chuyen.OdoBatDau.HasValue && odoKetThuc < chuyen.OdoBatDau.Value)
        {
            return BadRequest(Loi(
                $"ODO kết thúc ({odoKetThuc}) nhỏ hơn ODO lúc bắt đầu ({chuyen.OdoBatDau.Value})",
                "Kiểm tra lại số ODO vừa nhập",
                "ODO_KET_THUC_NHO_HON_BAT_DAU"));
        }

        if (!PublicIdOdoHopLe(dto.AnhOdoPublicId))
        {
            return BadRequest(Loi(
                "Ảnh ODO không hợp lệ",
                "Vui lòng chụp lại ảnh đồng hồ",
                "ANH_ODO_KHONG_HOP_LE"));
        }

        byte[]? anhChuKy = null;
        byte[]? anhGiaoHang = null;

        if (!string.IsNullOrWhiteSpace(dto.AnhChuKyBase64)
            && !TryGiaiMaBase64(dto.AnhChuKyBase64, out anhChuKy))
        {
            return BadRequest(Loi(
                "Ảnh chữ ký gửi lên không hợp lệ",
                "Vui lòng ký lại và xác nhận lần nữa",
                "ANH_CHU_KY_KHONG_HOP_LE"));
        }

        if (!string.IsNullOrWhiteSpace(dto.AnhGiaoHangBase64)
            && !TryGiaiMaBase64(dto.AnhGiaoHangBase64, out anhGiaoHang))
        {
            return BadRequest(Loi(
                "Ảnh biên bản giao hàng gửi lên không hợp lệ",
                "Vui lòng chụp lại biên bản và xác nhận lần nữa",
                "ANH_GIAO_HANG_KHONG_HOP_LE"));
        }

        chuyen.TrangThai = TrangThaiChuyen.HoanThanh;
        chuyen.ThoiGianKetThuc = DateTime.UtcNow;
        chuyen.OdoKetThuc = odoKetThuc;
        chuyen.AnhOdoKetThucPublicId = string.IsNullOrWhiteSpace(dto.AnhOdoPublicId)
            ? null
            : dto.AnhOdoPublicId.Trim();

        if (!string.IsNullOrWhiteSpace(dto.NguoiKyTen))
            chuyen.NguoiKyTen = dto.NguoiKyTen.Trim();

        if (anhChuKy != null)
            chuyen.AnhChuKy = anhChuKy;

        if (anhGiaoHang != null)
        {
            chuyen.AnhGiaoHang = anhGiaoHang;
            chuyen.AnhGiaoHangLoaiNoiDung = string.IsNullOrWhiteSpace(dto.AnhGiaoHangLoaiNoiDung)
                ? "image/jpeg"
                : dto.AnhGiaoHangLoaiNoiDung.Trim();
        }

        await _db.SaveChangesAsync();

        return Ok(new { thanhCong = true, trangThai = chuyen.TrangThai.ToString() });
    }

    private static bool OdoHopLe(int odo) => odo is >= 0 and <= OdoToiDa;

    private static bool PublicIdOdoHopLe(string? publicId) =>
        string.IsNullOrWhiteSpace(publicId)
        || (publicId.Length <= 200 && publicId.StartsWith(ThuMucAnhOdo + "/", StringComparison.Ordinal));

    private static bool TryGiaiMaBase64(string chuoi, out byte[]? duLieu)
    {
        try
        {
            duLieu = Convert.FromBase64String(chuoi);
            return true;
        }
        catch (FormatException)
        {
            duLieu = null;
            return false;
        }
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