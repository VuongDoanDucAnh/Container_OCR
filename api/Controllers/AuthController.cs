using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DoAnOlympics.Api.DTOs;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Repositories;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDriverRepository _driverRepo;
    private readonly IJwtService _jwtService;

    public AuthController(IDriverRepository driverRepo, IJwtService jwtService)
    {
        _driverRepo = driverRepo;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _driverRepo.UsernameDaTonTaiAsync(dto.Username))
            return BadRequest(new { loi = "Username đã tồn tại" });

        var driver = new Driver
        {
            MaTaiXe = dto.MaTaiXe,
            HoTen = dto.HoTen,
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
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
}