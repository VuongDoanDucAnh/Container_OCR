using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

    public ContainerController(
        IGeminiOcrClient geminiClient,
        IGoogleSheetsService sheetsService,
        IContainerRecordRepository recordRepo,
        IDriverRepository driverRepo)
    {
        _geminiClient = geminiClient;
        _sheetsService = sheetsService;
        _recordRepo = recordRepo;
        _driverRepo = driverRepo;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(IFormFile anh)
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

        var record = new ContainerRecord
        {
            DriverId = driverId,
            MaContainer = ketQuaOcr.MaContainer,
            ChecksumHopLe = hopLe
        };
        await _recordRepo.ThemMoiAsync(record);

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

        return Ok(new
        {
            thanhCong = true,
            maContainer = ketQuaOcr.MaContainer,
            checksumHopLe = hopLe
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