using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Repositories;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.Validators;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContainerController : ControllerBase
{
    private readonly IGeminiOcrClient _geminiClient;
    private readonly IGoogleSheetsService _sheetsService;
    private readonly IContainerRecordRepository _recordRepo;
    private readonly IDriverRepository _driverRepo;
    private readonly IAnhLuuTruService _anhLuuTru;
    private readonly AppDbContext _db;

    private const int DungSaiPhut = 5;

    public ContainerController(
        IGeminiOcrClient geminiClient,
        IGoogleSheetsService sheetsService,
        IContainerRecordRepository recordRepo,
        IDriverRepository driverRepo,
        IAnhLuuTruService anhLuuTru,
        AppDbContext db)
    {
        _geminiClient = geminiClient;
        _sheetsService = sheetsService;
        _recordRepo = recordRepo;
        _driverRepo = driverRepo;
        _anhLuuTru = anhLuuTru;
        _db = db;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(
        IFormFile anh,
        [FromForm] int? loaiHinhChuyenId,
        [FromForm] string? diaDiem,
        [FromForm] Guid? maYeuCau,
        [FromForm] DateTimeOffset? thoiGianChup,
        [FromForm] double? viDo,
        [FromForm] double? kinhDo)
    {
        if (anh is null || anh.Length == 0)
        {
            return BadRequest(new
            {
                thanhCong = false,
                loi = "Chưa có ảnh nào được gửi lên",
                huongGiaiQuyet = "Vui lòng chụp ảnh container rồi thử lại",
                maLoi = "CHUA_CO_ANH"
            });
        }

        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await _driverRepo.TimTheoIdAsync(driverId);
        if (driver is null) return Unauthorized();

        if (maYeuCau.HasValue)
        {
            var daLuuTruoc = await _db.ContainerRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MaYeuCau == maYeuCau.Value && c.DriverId == driverId);

            if (daLuuTruoc is not null) return KetQuaDaLuu(daLuuTruoc);
        }

        using var ms = new MemoryStream();
        await anh.CopyToAsync(ms);
        byte[] anhBytes = ms.ToArray();

        GeminiOcrResult ketQuaOcr;
        try
        {
            ketQuaOcr = await _geminiClient.DocMaContainerAsync(anhBytes, anh.ContentType);
        }
        catch (Exception)
        {
            return StatusCode(503, new
            {
                thanhCong = false,
                loi = "Hệ thống nhận diện ảnh đang tạm ngừng hoạt động",
                huongGiaiQuyet = "Vui lòng thử lại sau ít phút. Nếu vẫn không được, báo cho quản lý",
                maLoi = "HET_LUOT"
            });
        }

        if (!string.IsNullOrWhiteSpace(ketQuaOcr.GhiChu))
        {
            return StatusCode(503, new
            {
                thanhCong = false,
                loi = "Hệ thống nhận diện ảnh đang tạm ngừng hoạt động",
                huongGiaiQuyet = "Vui lòng thử lại sau ít phút. Nếu vẫn không được, báo cho quản lý",
                maLoi = "HET_LUOT"
            });
        }

        if (!ketQuaOcr.DocDuoc || string.IsNullOrWhiteSpace(ketQuaOcr.MaContainer))
        {
            return Ok(new
            {
                thanhCong = false,
                loi = "Không đọc được mã container trong ảnh",
                huongGiaiQuyet = "Vui lòng chụp lại, đảm bảo mã container rõ nét và đủ ánh sáng",
                maLoi = "KHONG_DOC_DUOC"
            });
        }

        bool hopLe = Iso6346Validator.IsValid(ketQuaOcr.MaContainer, out string loiChecksum);

        if (!hopLe)
        {
            return Ok(new
            {
                thanhCong = false,
                loi = $"Mã container đọc được ({ketQuaOcr.MaContainer}) không đúng định dạng",
                huongGiaiQuyet = "Vui lòng chụp lại ảnh rõ hơn, đảm bảo thấy đủ toàn bộ mã container",
                maLoi = "CHECKSUM_SAI",
                maContainer = ketQuaOcr.MaContainer
            });
        }

        var banGhiTrung = await _recordRepo.TimBanGhiTrungAsync(ketQuaOcr.MaContainer);
        if (banGhiTrung is not null)
        {
            bool cungTaiXe = banGhiTrung.DriverId == driverId;

            if (cungTaiXe)
            {
                return Conflict(new
                {
                    thanhCong = false,
                    loi = "Bạn đã quét container này trước đó rồi",
                    huongGiaiQuyet = "Không cần quét lại. Nếu đây thực sự là 2 chuyến hàng khác nhau, báo cho quản lý kiểm tra",
                    maLoi = "LAP_DON",
                    maContainer = ketQuaOcr.MaContainer,
                    thoiGianDaQuetTruoc = banGhiTrung.ThoiGianQuet
                });
            }

            return Conflict(new
            {
                thanhCong = false,
                loi = "Container này đã được tài xế khác ghi nhận trước",
                huongGiaiQuyet = "Kiểm tra lại đúng thùng container. Nếu chắc chắn đúng thùng này, báo cho quản lý kiểm tra",
                maLoi = "TRUNG_KHAC_TAI_XE",
                maContainer = ketQuaOcr.MaContainer,
                thoiGianDaQuetTruoc = banGhiTrung.ThoiGianQuet
            });
        }

        var bayGio = DateTime.UtcNow;
        var thoiGianQuet = bayGio;
        if (thoiGianChup.HasValue)
        {
            var gioChup = thoiGianChup.Value.UtcDateTime;
            thoiGianQuet = gioChup > bayGio ? bayGio : gioChup;
        }

        var ngayChup = thoiGianQuet.ToLocalTime().Date;
        var moBatDauMuon = thoiGianQuet.AddMinutes(DungSaiPhut);
        var chuyenKhop = await _db.PhanCongChuyens
            .Where(p => p.DriverId == driverId
                && p.DonHang!.NgayChay == ngayChup
                && p.ThoiGianBatDau != null
                && p.ThoiGianBatDau <= moBatDauMuon
                && (p.ThoiGianKetThuc == null || p.ThoiGianKetThuc >= thoiGianQuet))
            .OrderBy(p => p.ThoiGianBatDau)
            .FirstOrDefaultAsync();

        int? loaiHinhHopLe = null;
        if (loaiHinhChuyenId.HasValue
            && await _db.LoaiHinhChuyens.AnyAsync(l => l.Id == loaiHinhChuyenId.Value))
        {
            loaiHinhHopLe = loaiHinhChuyenId.Value;
        }

        double? viDoHopLe = null;
        double? kinhDoHopLe = null;
        if (viDo is >= -90 and <= 90 && kinhDo is >= -180 and <= 180)
        {
            viDoHopLe = viDo;
            kinhDoHopLe = kinhDo;
        }

        var anhPublicId = await _anhLuuTru.TaiLenAsync(anhBytes, "container");

        var record = new ContainerRecord
        {
            DriverId = driverId,
            MaContainer = ketQuaOcr.MaContainer,
            ChecksumHopLe = hopLe,
            ThoiGianQuet = thoiGianQuet,
            PhanCongChuyenId = chuyenKhop?.Id,
            LoaiHinhChuyenId = loaiHinhHopLe,
            DiaDiem = string.IsNullOrWhiteSpace(diaDiem) ? null : diaDiem.Trim(),
            MaYeuCau = maYeuCau,
            ViDo = viDoHopLe,
            KinhDo = kinhDoHopLe,
            AnhPublicId = anhPublicId,
            ThoiGianNhanServer = bayGio
        };

        try
        {
            await _recordRepo.ThemMoiAsync(record);
        }
        catch (DbUpdateException) when (maYeuCau.HasValue)
        {
            var daLuu = await _db.ContainerRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MaYeuCau == maYeuCau.Value && c.DriverId == driverId);

            if (daLuu is null) throw;
            return KetQuaDaLuu(daLuu);
        }

        try
        {
            await _sheetsService.GhiDongLuongAsync(driver.MaTaiXe, driver.HoTen, ketQuaOcr.MaContainer, record.ThoiGianQuet);
            record.DaGhiSheet = true;
        }
        catch (Exception)
        {
            return Ok(new
            {
                thanhCong = true,
                maContainer = ketQuaOcr.MaContainer,
                checksumHopLe = hopLe,
                canhBao = "Đã ghi nhận thành công, nhưng chưa đồng bộ vào bảng lương",
                huongGiaiQuyet = "Không cần chụp lại. Báo cho quản lý để kiểm tra lại bảng lương cuối ngày",
                maLoi = "LOI_GHI_SHEET"
            });
        }

        await _recordRepo.CapNhatAsync(record);

        return Ok(new
        {
            thanhCong = true,
            maContainer = ketQuaOcr.MaContainer,
            checksumHopLe = hopLe
        });
    }

    private IActionResult KetQuaDaLuu(ContainerRecord banGhi)
    {
        if (!banGhi.DaGhiSheet)
        {
            return Ok(new
            {
                thanhCong = true,
                maContainer = banGhi.MaContainer,
                checksumHopLe = banGhi.ChecksumHopLe,
                canhBao = "Đã ghi nhận thành công, nhưng chưa đồng bộ vào bảng lương",
                huongGiaiQuyet = "Không cần chụp lại. Báo cho quản lý để kiểm tra lại bảng lương cuối ngày",
                maLoi = "LOI_GHI_SHEET"
            });
        }

        return Ok(new
        {
            thanhCong = true,
            maContainer = banGhi.MaContainer,
            checksumHopLe = banGhi.ChecksumHopLe
        });
    }

    [HttpGet("lich-su")]
    public async Task<IActionResult> LichSu()
    {
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var danhSach = await _recordRepo.LayTheoTaiXeAsync(driverId);
        return Ok(danhSach);
    }
}