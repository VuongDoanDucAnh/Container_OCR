using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly/email-tai-xe")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyEmailController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyEmailController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> DanhSach()
    {
        var taiXes = await _db.Drivers.AsNoTracking().OrderBy(d => d.MaTaiXe).ToListAsync();
        return View(taiXes);
    }

    [HttpPost("cap-nhat/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhat(int id, string? email)
    {
        var taiXe = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id);
        if (taiXe is null) return NotFound();

        string? emailChuan = null;

        if (!string.IsNullOrWhiteSpace(email))
        {
            emailChuan = ChuanHoaEmail(email);
            if (emailChuan is null)
            {
                TempData["Loi"] = "Email không hợp lệ.";
                return RedirectToAction(nameof(DanhSach));
            }

            bool daDuocGan = await _db.Drivers.AnyAsync(d => d.Id != id && d.Email == emailChuan);
            if (daDuocGan)
            {
                TempData["Loi"] = $"Email {emailChuan} đã được gán cho tài xế khác.";
                return RedirectToAction(nameof(DanhSach));
            }
        }

        taiXe.Email = emailChuan;
        await _db.SaveChangesAsync();

        TempData["ThongBao"] = emailChuan is null
            ? $"Đã gỡ email của tài xế {taiXe.MaTaiXe}."
            : $"Đã gán {emailChuan} cho tài xế {taiXe.MaTaiXe}.";

        return RedirectToAction(nameof(DanhSach));
    }

    private static string? ChuanHoaEmail(string email)
    {
        string gon = email.Trim();

        if (gon.Length > 256 || !MailAddress.TryCreate(gon, out var diaChi) || diaChi.Address != gon)
        {
            return null;
        }

        return gon.ToLowerInvariant();
    }
}