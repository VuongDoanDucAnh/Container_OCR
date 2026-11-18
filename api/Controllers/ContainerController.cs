using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
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
    private readonly ILogger<ContainerController> _logger;

    private const int DungSaiPhut = 5;
    private const long GioiHanAnhByte = 10 * 1024 * 1024;

    public ContainerController(
        IGeminiOcrClient geminiClient,
        IGoogleSheetsService sheetsService,
        IContainerRecordRepository recordRepo,
        IDriverRepository driverRepo,
        IAnhLuuTruService anhLuuTru,
        AppDbContext db,
        ILogger<ContainerController> logger)
    {
        _geminiClient = geminiClient;
        _sheetsService = sheetsService;
        _recordRepo = recordRepo;
        _driverRepo = driverRepo;
        _anhLuuTru = anhLuuTru;
        _db = db;
        _logger = logger;
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
        var dongHo = System.Diagnostics.Stopwatch.StartNew();
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

        int kichThuocAnh = (int)Math.Min(anh.Length, int.MaxValue);

        if (anh.Length > GioiHanAnhByte)
        {
            return await KetThucQuet(
                BadRequest(new
                {
                    thanhCong = false,
                    loi = "Ảnh quá lớn",
                    huongGiaiQuyet = "Vui lòng chụp lại với độ phân giải thấp hơn",
                    maLoi = "ANH_QUA_LON"
                }),
                driverId, maYeuCau, kichThuocAnh, dongHo, "ANH_QUA_LON");
        }

        var driver = await _driverRepo.TimTheoIdAsync(driverId);
        if (driver is null) return Unauthorized();

        if (maYeuCau.HasValue)
        {
            var daLuuTruoc = await _db.ContainerRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.MaYeuCau == maYeuCau.Value && c.DriverId == driverId);

            if (daLuuTruoc is not null)
            {
                return await KetThucQuet(KetQuaDaLuu(daLuuTruoc), driverId, maYeuCau, kichThuocAnh, dongHo, "TRUNG_YEU_CAU", daLuuTruoc.MaContainer);
            }
        }

        using var ms = new MemoryStream();
        await anh.CopyToAsync(ms);
        byte[] anhBytes = ms.ToArray();

        GeminiOcrResult ketQuaOcr;
        try
        {
            ketQuaOcr = await _geminiClient.DocMaContainerAsync(anhBytes, anh.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gọi Gemini OCR thất bại. DriverId={DriverId} MaYeuCau={MaYeuCau} KichThuocAnh={KichThuocAnh}", driverId, maYeuCau, anhBytes.Length);
            var loaiLoi = PhanLoaiNgoaiLe(ex);
            return await KetThucQuet(PhanHoiLoiAi(loaiLoi), driverId, maYeuCau, kichThuocAnh, dongHo, MaLoiAi(loaiLoi));
        }

        if (!string.IsNullOrWhiteSpace(ketQuaOcr.GhiChu))
        {
            _logger.LogWarning("Gemini OCR báo lỗi. Loai={Loai} DriverId={DriverId} MaYeuCau={MaYeuCau} ChiTiet={ChiTiet}", ketQuaOcr.Loai, driverId, maYeuCau, CatNgan(ketQuaOcr.GhiChu));
            return await KetThucQuet(PhanHoiLoiAi(ketQuaOcr.Loai), driverId, maYeuCau, kichThuocAnh, dongHo, MaLoiAi(ketQuaOcr.Loai));
        }

        if (!ketQuaOcr.DocDuoc || string.IsNullOrWhiteSpace(ketQuaOcr.MaContainer))
        {
            return await KetThucQuet(
                Ok(new
                {
                    thanhCong = false,
                    loi = "Không đọc được mã container trong ảnh",
                    huongGiaiQuyet = "Vui lòng chụp lại, đảm bảo mã container rõ nét và đủ ánh sáng",
                    maLoi = "KHONG_DOC_DUOC"
                }),
                driverId, maYeuCau, kichThuocAnh, dongHo, "KHONG_DOC_DUOC");
        }

        bool hopLe = Iso6346Validator.IsValid(ketQuaOcr.MaContainer, out string loiChecksum);

        if (!hopLe)
        {
            return await KetThucQuet(
                Ok(new
                {
                    thanhCong = false,
                    loi = $"Mã container đọc được ({ketQuaOcr.MaContainer}) không đúng định dạng",
                    huongGiaiQuyet = "Vui lòng chụp lại ảnh rõ hơn, đảm bảo thấy đủ toàn bộ mã container",
                    maLoi = "CHECKSUM_SAI",
                    maContainer = ketQuaOcr.MaContainer
                }),
                driverId, maYeuCau, kichThuocAnh, dongHo, "CHECKSUM_SAI", ketQuaOcr.MaContainer);
        }

        var bayGio = DateTime.UtcNow;
        var thoiGianQuet = bayGio;
        if (thoiGianChup.HasValue)
        {
            var gioChup = thoiGianChup.Value.UtcDateTime;
            thoiGianQuet = gioChup > bayGio ? bayGio : gioChup;
        }

        var (tuNgayUtc, denNgayUtc) = KhoangNgayVn(thoiGianQuet);
        var banGhiTrung = await _recordRepo.TimBanGhiTrungAsync(ketQuaOcr.MaContainer, tuNgayUtc, denNgayUtc);
        if (banGhiTrung is not null)
        {
            bool cungTaiXe = banGhiTrung.DriverId == driverId;

            if (cungTaiXe)
            {
                return await KetThucQuet(
                    Conflict(new
                    {
                        thanhCong = false,
                        loi = "Bạn đã quét container này trong cùng ngày rồi",
                        huongGiaiQuyet = "Không cần quét lại. Nếu đây thực sự là 2 chuyến hàng khác nhau, báo cho quản lý kiểm tra",
                        maLoi = "LAP_DON",
                        maContainer = ketQuaOcr.MaContainer,
                        thoiGianDaQuetTruoc = banGhiTrung.ThoiGianQuet
                    }),
                    driverId, maYeuCau, kichThuocAnh, dongHo, "LAP_DON", ketQuaOcr.MaContainer);
            }

            return await KetThucQuet(
                Conflict(new
                {
                    thanhCong = false,
                    loi = "Container này đã được tài xế khác ghi nhận trong cùng ngày",
                    huongGiaiQuyet = "Kiểm tra lại đúng thùng container. Nếu chắc chắn đúng thùng này, báo cho quản lý kiểm tra",
                    maLoi = "TRUNG_KHAC_TAI_XE",
                    maContainer = ketQuaOcr.MaContainer,
                    thoiGianDaQuetTruoc = banGhiTrung.ThoiGianQuet
                }),
                driverId, maYeuCau, kichThuocAnh, dongHo, "TRUNG_KHAC_TAI_XE", ketQuaOcr.MaContainer);
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
            return await KetThucQuet(KetQuaDaLuu(daLuu), driverId, maYeuCau, kichThuocAnh, dongHo, "TRUNG_YEU_CAU", daLuu.MaContainer);
        }

        try
        {
            await _sheetsService.GhiDongLuongAsync(driver.MaTaiXe, driver.HoTen, ketQuaOcr.MaContainer, record.ThoiGianQuet);
            record.DaGhiSheet = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ghi bảng lương Google Sheets thất bại. DriverId={DriverId} MaContainer={MaContainer}", driverId, ketQuaOcr.MaContainer);
            return await KetThucQuet(
                Ok(new
                {
                    thanhCong = true,
                    maContainer = ketQuaOcr.MaContainer,
                    checksumHopLe = hopLe,
                    canhBao = "Đã ghi nhận thành công, nhưng chưa đồng bộ vào bảng lương",
                    huongGiaiQuyet = "Không cần chụp lại. Báo cho quản lý để kiểm tra lại bảng lương cuối ngày",
                    maLoi = "LOI_GHI_SHEET"
                }),
                driverId, maYeuCau, kichThuocAnh, dongHo, "THANH_CONG_LOI_SHEET", ketQuaOcr.MaContainer);
        }

        await _recordRepo.CapNhatAsync(record);

        return await KetThucQuet(
            Ok(new
            {
                thanhCong = true,
                maContainer = ketQuaOcr.MaContainer,
                checksumHopLe = hopLe
            }),
            driverId, maYeuCau, kichThuocAnh, dongHo, "THANH_CONG", ketQuaOcr.MaContainer);
    }

    private static GeminiLoiLoai PhanLoaiNgoaiLe(Exception ex) => ex switch
    {
        InvalidOperationException or KeyNotFoundException or JsonException => GeminiLoiLoai.KhongCoNoiDung,
        _ => GeminiLoiLoai.TamThoi
    };

    private IActionResult PhanHoiLoiAi(GeminiLoiLoai loai)
    {
        switch (loai)
        {
            case GeminiLoiLoai.CauHinh:
                return StatusCode(500, new
                {
                    thanhCong = false,
                    loi = "Hệ thống nhận diện ảnh đang gặp sự cố cấu hình",
                    huongGiaiQuyet = "Vui lòng báo cho quản lý để kiểm tra",
                    maLoi = "LOI_CAU_HINH_AI"
                });
            case GeminiLoiLoai.KhongCoNoiDung:
                return Ok(new
                {
                    thanhCong = false,
                    loi = "Không đọc được mã container trong ảnh",
                    huongGiaiQuyet = "Vui lòng chụp lại, đảm bảo mã container rõ nét và đủ ánh sáng",
                    maLoi = "KHONG_DOC_DUOC"
                });
            default:
                return StatusCode(503, new
                {
                    thanhCong = false,
                    loi = "Hệ thống nhận diện ảnh đang tạm ngừng hoạt động",
                    huongGiaiQuyet = "Vui lòng thử lại sau ít phút. Nếu vẫn không được, báo cho quản lý",
                    maLoi = "HET_LUOT"
                });
        }
    }

    private static string CatNgan(string chuoi) => chuoi.Length <= 500 ? chuoi : chuoi[..500] + "...";

    private static string MaLoiAi(GeminiLoiLoai loai) => loai switch
    {
        GeminiLoiLoai.CauHinh => "LOI_CAU_HINH_AI",
        GeminiLoiLoai.KhongCoNoiDung => "KHONG_DOC_DUOC",
        _ => "HET_LUOT"
    };

    private async Task<IActionResult> KetThucQuet(
        IActionResult ketQua,
        int driverId,
        Guid? maYeuCau,
        int kichThuocAnh,
        System.Diagnostics.Stopwatch dongHo,
        string maKetQua,
        string? maContainerDoc = null)
    {
        try
        {
            _db.ChangeTracker.Clear();
            _db.NhatKyQuets.Add(new NhatKyQuet
            {
                DriverId = driverId,
                MaYeuCau = maYeuCau,
                KetQua = maKetQua,
                MaContainerDoc = maContainerDoc is { Length: > 20 } ? maContainerDoc[..20] : maContainerDoc,
                DoTreMs = (int)dongHo.ElapsedMilliseconds,
                KichThuocAnhByte = kichThuocAnh
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được nhật ký quét. DriverId={DriverId} KetQua={KetQua}", driverId, maKetQua);
        }

        return ketQua;
    }

    private static (DateTime TuUtc, DateTime DenUtc) KhoangNgayVn(DateTime thoiGianUtc)
    {
        var muiGio = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var gioVn = TimeZoneInfo.ConvertTimeFromUtc(thoiGianUtc, muiGio);
        var dauNgayVn = gioVn.Date;
        return (
            TimeZoneInfo.ConvertTimeToUtc(dauNgayVn, muiGio),
            TimeZoneInfo.ConvertTimeToUtc(dauNgayVn.AddDays(1), muiGio));
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