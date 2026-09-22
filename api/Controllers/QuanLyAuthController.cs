using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly")]
public class QuanLyAuthController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyAuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("dangnhap")]
    [AllowAnonymous]
    public async Task<IActionResult> DangNhap(string? returnUrl = null)
    {
        var ketQua = await HttpContext.AuthenticateAsync(QuanLyAuth.Scheme);
        if (ketQua.Succeeded)
        {
            return RedirectToAction("TongQuan", "QuanLy");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost("dangnhap")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DangNhap(string? tenDangNhap, string? matKhau, string? returnUrl = null)
    {
        tenDangNhap = tenDangNhap?.Trim() ?? string.Empty;
        matKhau ??= string.Empty;

        var quanLy = await _db.QuanLys.FirstOrDefaultAsync(q => q.TenDangNhap == tenDangNhap);
        if (quanLy == null || !BCrypt.Net.BCrypt.Verify(matKhau, quanLy.MatKhauHash))
        {
            ViewBag.Loi = "Tên đăng nhập hoặc mật khẩu không đúng.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, quanLy.Id.ToString()),
            new(ClaimTypes.Name, quanLy.HoTen),
            new(ClaimTypes.Role, QuanLyAuth.Role)
        };

        var identity = new ClaimsIdentity(claims, QuanLyAuth.Scheme);
        await HttpContext.SignInAsync(QuanLyAuth.Scheme, new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("TongQuan", "QuanLy");
    }

    [HttpPost("dangxuat")]
    [Authorize(AuthenticationSchemes = QuanLyAuth.Scheme)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DangXuat()
    {
        await HttpContext.SignOutAsync(QuanLyAuth.Scheme);
        return RedirectToAction("DangNhap");
    }
}