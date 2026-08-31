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
            return BadRequest(new { loi = "Chưa gửi ảnh" });

        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await _driverRepo.TimTheoIdAsync(driverId);
        if (driver is null) return Unauthorized();

        using var ms = new MemoryStream();
        await anh.CopyToAsync(ms);
        byte[] anhBytes = ms.ToArray();

        var ketQuaOcr = await _geminiClient.DocMaContainerAsync(anhBytes, anh.ContentType);

        if (!ketQuaOcr.DocDuoc || string.IsNullOrWhiteSpace(ketQuaOcr.MaContainer))
        {
            return Ok(new
            {
                thanhCong = false,
                loi = ketQuaOcr.GhiChu ?? "Không đọc được mã container, vui lòng chụp lại rõ hơn"
            });
        }

        bool hopLe = Iso6346Validator.IsValid(ketQuaOcr.MaContainer, out string loiChecksum);

        var record = new ContainerRecord
        {
            DriverId = driverId,
            MaContainer = ketQuaOcr.MaContainer,
            ChecksumHopLe = hopLe
        };
        await _recordRepo.ThemMoiAsync(record);

        if (hopLe)
        {
            try
            {
                await _sheetsService.GhiDongLuongAsync(driver.MaTaiXe, driver.HoTen, ketQuaOcr.MaContainer, record.ThoiGianQuet);
                record.DaGhiSheet = true;
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    thanhCong = true,
                    maContainer = ketQuaOcr.MaContainer,
                    checksumHopLe = hopLe,
                    canhBao = $"Đã lưu vào DB nhưng ghi Google Sheets thất bại: {ex.Message}"
                });
            }
        }

        return Ok(new
        {
            thanhCong = true,
            maContainer = ketQuaOcr.MaContainer,
            checksumHopLe = hopLe,
            loiChecksum = hopLe ? null : loiChecksum
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
