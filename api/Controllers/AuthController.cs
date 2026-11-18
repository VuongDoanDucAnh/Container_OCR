using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.DTOs;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Repositories;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string SchemeGoogle = "Google";
    private const string SchemeGoogleNgoai = "GoogleNgoai";
    private const string DiaChiQuayVeApp = "appocr://dang-nhap";
    private static readonly TimeSpan HanMaDangNhap = TimeSpan.FromSeconds(60);

    private readonly IDriverRepository _driverRepo;
    private readonly IJwtService _jwtService;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    public AuthController(
        IDriverRepository driverRepo,
        IJwtService jwtService,
        AppDbContext db,
        IMemoryCache cache,
        IAuthenticationSchemeProvider schemeProvider)
    {
        _driverRepo = driverRepo;
        _jwtService = jwtService;
        _db = db;
        _cache = cache;
        _schemeProvider = schemeProvider;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        string maTaiXe = (dto.MaTaiXe ?? string.Empty).Trim();
        string hoTen = (dto.HoTen ?? string.Empty).Trim();
        string username = (dto.Username ?? string.Empty).Trim();
        string password = dto.Password ?? string.Empty;

        if (maTaiXe.Length == 0 || hoTen.Length == 0 || username.Length == 0 || password.Length == 0)
            return BadRequest(new { loi = "Vui lòng nhập đầy đủ thông tin" });

        if (await _driverRepo.UsernameDaTonTaiAsync(username))
            return BadRequest(new { loi = "Username đã tồn tại" });

        if (await _driverRepo.MaTaiXeDaTonTaiAsync(maTaiXe))
            return BadRequest(new { loi = "Mã tài xế đã tồn tại" });

        var driver = new Driver
        {
            MaTaiXe = maTaiXe,
            HoTen = hoTen,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };

        await _driverRepo.ThemMoiAsync(driver);

        return Ok(new { thongBao = "Đăng ký thành công", maTaiXe = driver.MaTaiXe });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var driver = await _driverRepo.TimTheoUsernameAsync(dto.Username);
        if (driver is null || !BCrypt.Net.BCrypt.Verify(dto.Password, driver.PasswordHash))
            return Unauthorized(new { loi = "Sai username hoặc mật khẩu" });

        string token = _jwtService.TaoToken(driver);
        return Ok(new { token, maTaiXe = driver.MaTaiXe, hoTen = driver.HoTen });
    }

    [HttpGet("google/bat-dau")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleBatDau([FromQuery] string? thuThach)
    {
        if (await _schemeProvider.GetSchemeAsync(SchemeGoogle) is null)
            return VeApp("loi", "GOOGLE_CHUA_CAU_HINH");

        if (string.IsNullOrWhiteSpace(thuThach) || thuThach.Length != 64 || !thuThach.All(Uri.IsHexDigit))
            return VeApp("loi", "GOOGLE_THAT_BAI");

        var thuocTinh = new AuthenticationProperties { RedirectUri = "/api/auth/google/hoan-tat" };
        thuocTinh.Items["thuThach"] = thuThach.ToLowerInvariant();

        return Challenge(thuocTinh, SchemeGoogle);
    }

    [HttpGet("google/hoan-tat")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleHoanTat()
    {
        if (await _schemeProvider.GetSchemeAsync(SchemeGoogleNgoai) is null)
            return VeApp("loi", "GOOGLE_CHUA_CAU_HINH");

        var ketQua = await HttpContext.AuthenticateAsync(SchemeGoogleNgoai);
        await HttpContext.SignOutAsync(SchemeGoogleNgoai);

        if (!ketQua.Succeeded || ketQua.Principal is null)
            return VeApp("loi", "GOOGLE_THAT_BAI");

        string? email = ketQua.Principal.FindFirstValue(ClaimTypes.Email);
        string? daXacMinh = ketQua.Principal.FindFirstValue("email_verified");

        string? thuThach = null;
        ketQua.Properties?.Items.TryGetValue("thuThach", out thuThach);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(thuThach))
            return VeApp("loi", "GOOGLE_THAT_BAI");

        if (string.Equals(daXacMinh, "false", StringComparison.OrdinalIgnoreCase))
            return VeApp("loi", "EMAIL_CHUA_XAC_MINH");

        string emailChuan = email.Trim().ToLowerInvariant();
        var driver = await _db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Email == emailChuan);
        if (driver is null)
            return VeApp("loi", "EMAIL_CHUA_DANG_KY");

        string ma = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _cache.Set(KhoaMa(ma), new GoogleMaTamThoi(driver.Id, thuThach), HanMaDangNhap);

        return VeApp("code", ma);
    }

    [HttpPost("google/doi-ma")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleDoiMa(GoogleDoiMaDto dto)
    {
        string ma = (dto.Code ?? string.Empty).Trim();
        string xacMinh = (dto.XacMinh ?? string.Empty).Trim();

        if (ma.Length != 64 || xacMinh.Length == 0
            || !_cache.TryGetValue(KhoaMa(ma), out GoogleMaTamThoi? tamThoi) || tamThoi is null)
        {
            return Unauthorized(new { loi = "Mã đăng nhập không hợp lệ hoặc đã hết hạn" });
        }

        _cache.Remove(KhoaMa(ma));

        string bam = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xacMinh))).ToLowerInvariant();
        bool khop = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(bam),
            Encoding.ASCII.GetBytes(tamThoi.ThuThach));

        if (!khop)
            return Unauthorized(new { loi = "Mã đăng nhập không hợp lệ hoặc đã hết hạn" });

        var driver = await _driverRepo.TimTheoIdAsync(tamThoi.DriverId);
        if (driver is null)
            return Unauthorized(new { loi = "Tài khoản không còn tồn tại" });

        string token = _jwtService.TaoToken(driver);
        return Ok(new { token, maTaiXe = driver.MaTaiXe, hoTen = driver.HoTen });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await _driverRepo.TimTheoIdAsync(driverId);
        if (driver is null) return NotFound();

        return Ok(new
        {
            maTaiXe = driver.MaTaiXe,
            hoTen = driver.HoTen,
            username = driver.Username,
            soDienThoai = driver.SoDienThoai
        });
    }

    [HttpPut("thong-tin")]
    [Authorize]
    public async Task<IActionResult> CapNhatThongTin(CapNhatThongTinDto dto)
    {
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await _driverRepo.TimTheoIdAsync(driverId);
        if (driver is null) return NotFound();

        driver.HoTen = dto.HoTen;
        driver.SoDienThoai = dto.SoDienThoai;
        await _driverRepo.CapNhatAsync(driver);

        return Ok(new { thongBao = "Cập nhật thông tin thành công" });
    }

    [HttpPut("doi-mat-khau")]
    [Authorize]
    public async Task<IActionResult> DoiMatKhau(DoiMatKhauDto dto)
    {
        int driverId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await _driverRepo.TimTheoIdAsync(driverId);
        if (driver is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(dto.MatKhauCu, driver.PasswordHash))
            return BadRequest(new { loi = "Mật khẩu cũ không đúng" });

        driver.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.MatKhauMoi);
        await _driverRepo.CapNhatAsync(driver);

        return Ok(new { thongBao = "Đổi mật khẩu thành công" });
    }

    private static string KhoaMa(string ma) => $"google-ma:{ma}";

    private IActionResult VeApp(string khoa, string giaTri) =>
        Redirect($"{DiaChiQuayVeApp}?{khoa}={Uri.EscapeDataString(giaTri)}");

    private sealed record GoogleMaTamThoi(int DriverId, string ThuThach);
}