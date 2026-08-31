using Microsoft.AspNetCore.Mvc;
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
}
