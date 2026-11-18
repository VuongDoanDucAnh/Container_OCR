using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.ViewModels;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly/ke-toan")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyKeToanController : Controller
{
    private readonly AppDbContext _db;
    private readonly IBaoCaoAiService _aiService;

    public QuanLyKeToanController(AppDbContext db, IBaoCaoAiService aiService)
    {
        _db = db;
        _aiService = aiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> KeToan(string? tuNgay, string? denNgay)
    {
        var vm = await TaoViewModelAsync(tuNgay, denNgay);

        if (TempData["TomTat"] is string tomTat) ViewBag.TomTat = tomTat;
        if (TempData["LoiTomTat"] is string loiTomTat) ViewBag.LoiTomTat = loiTomTat;
        if (TempData["CauHoiAI"] is string cauHoiAI) ViewBag.CauHoiAI = cauHoiAI;
        if (TempData["CauTraLoiAI"] is string cauTraLoiAI) ViewBag.CauTraLoiAI = cauTraLoiAI;
        if (TempData["LoiCauTraLoiAI"] is string loiCauTraLoiAI) ViewBag.LoiCauTraLoiAI = loiCauTraLoiAI;

        return View(vm);
    }

    [HttpPost("gia-cuoc/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatGiaCuoc(int id, decimal donGiaMoi)
    {
        if (donGiaMoi < 0) return RedirectToAction(nameof(KeToan));

        var lh = await _db.LoaiHinhChuyens.FirstOrDefaultAsync(l => l.Id == id);
        if (lh is null) return NotFound();

        lh.DonGia = donGiaMoi;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(KeToan));
    }

    [HttpPost("tom-tat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoTomTat(string? tuNgay, string? denNgay)
    {
        var vm = await TaoViewModelAsync(tuNgay, denNgay);

        var sb = new StringBuilder();
        sb.AppendLine($"Kỳ báo cáo: {vm.TuNgay:dd/MM/yyyy} đến {vm.DenNgay:dd/MM/yyyy}");
        foreach (var d in vm.BangLuong)
        {
            sb.AppendLine($"- {d.MaTaiXe} ({d.HoTen}): {d.SoContainer} container đã phân loại, " +
                $"{d.SoChuaPhanLoai} chưa phân loại, tổng tiền {d.TongTien:N0}đ");
        }

        try
        {
            string tomTat = await _aiService.TomTatAsync(sb.ToString());
            TempData["TomTat"] = tomTat;
        }
        catch (Exception ex)
        {
            TempData["LoiTomTat"] = "Không tạo được tóm tắt AI: " + ex.Message;
        }

        return RedirectToAction(nameof(KeToan), new { tuNgay, denNgay });
    }

    [HttpPost("hoi-ai")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoiAI(string? tuNgay, string? denNgay, string? cauHoi)
    {
        string cauHoiSach = (cauHoi ?? string.Empty).Trim();
        TempData["CauHoiAI"] = cauHoiSach;

        if (cauHoiSach.Length == 0)
        {
            TempData["LoiCauTraLoiAI"] = "Vui lòng nhập câu hỏi trước khi gửi.";
            return RedirectToAction(nameof(KeToan), new { tuNgay, denNgay });
        }

        var vm = await TaoViewModelAsync(tuNgay, denNgay);
        var theoTuyen = await LayTheoTuyenAsync(vm.TuNgay, vm.DenNgay);

        var sb = new StringBuilder();
        sb.AppendLine($"Kỳ báo cáo: {vm.TuNgay:dd/MM/yyyy} đến {vm.DenNgay:dd/MM/yyyy}");
        sb.AppendLine("Số liệu theo tài xế:");
        foreach (var d in vm.BangLuong)
        {
            sb.AppendLine($"- {d.MaTaiXe} ({d.HoTen}): {d.SoContainer} container đã phân loại, " +
                $"{d.SoChuaPhanLoai} chưa phân loại, tổng lương {d.TongTien:N0}đ");
        }
        sb.AppendLine("Số liệu theo tuyến (lợi nhuận tạm tính = Doanh thu - Lương tài xế, CHƯA trừ tiền dầu " +
            "vì chi phí dầu hiện ghi theo tài xế, không tách được theo từng tuyến riêng lẻ):");
        foreach (var t in theoTuyen)
        {
            sb.AppendLine($"- {t.TenTuyen}: {t.SoDon} đơn, doanh thu {t.DoanhThu:N0}đ, " +
                $"lương tài xế {t.LuongTaiXe:N0}đ, lợi nhuận tạm tính {t.LoiNhuanTamTinh:N0}đ, " +
                $"{t.SoChuyenHoanThanh} chuyến hoàn thành");
        }

        try
        {
            string traLoi = await _aiService.TraLoiAsync(sb.ToString(), cauHoiSach);
            TempData["CauTraLoiAI"] = traLoi;
        }
        catch (Exception ex)
        {
            TempData["LoiCauTraLoiAI"] = "Không tạo được câu trả lời: " + ex.Message;
        }

        return RedirectToAction(nameof(KeToan), new { tuNgay, denNgay });
    }

    private async Task<List<TuyenThongKeItem>> LayTheoTuyenAsync(DateTime tu, DateTime den)
    {
        var donHangs = await _db.DonHangs
            .AsNoTracking()
            .Where(d => d.NgayChay >= tu && d.NgayChay < den.AddDays(1))
            .Select(d => new
            {
                TenTuyen = d.TuyenDuong!.TenTuyen,
                d.DoanhThu,
                SoChuyenHoanThanh = d.PhanCongChuyens.Count(p => p.TrangThai == TrangThaiChuyen.HoanThanh),
                LuongChuyenNay = d.PhanCongChuyens
                    .SelectMany(p => p.ContainerRecords)
                    .Where(c => c.ChecksumHopLe && c.LoaiHinhChuyenId != null)
                    .Sum(c => (decimal?)c.LoaiHinhChuyen!.DonGia) ?? 0m
            })
            .ToListAsync();

        return donHangs
            .GroupBy(d => d.TenTuyen)
            .Select(g => new TuyenThongKeItem
            {
                TenTuyen = g.Key,
                SoDon = g.Count(),
                DoanhThu = g.Sum(x => x.DoanhThu),
                LuongTaiXe = g.Sum(x => x.LuongChuyenNay),
                SoChuyenHoanThanh = g.Sum(x => x.SoChuyenHoanThanh)
            })
            .OrderByDescending(x => x.DoanhThu)
            .ToList();
    }

    private async Task<KeToanViewModel> TaoViewModelAsync(string? tuNgay, string? denNgay)
    {
        var (tu, den) = XacDinhKhoangNgay(tuNgay, denNgay);
        var tuUtc = tu.ToUniversalTime();
        var denUtc = den.AddDays(1).ToUniversalTime();

        var dsLoaiHinh = await _db.LoaiHinhChuyens.AsNoTracking().OrderBy(l => l.Id).ToListAsync();

        var banGhi = await _db.ContainerRecords
            .AsNoTracking()
            .Where(c => c.ChecksumHopLe && c.ThoiGianQuet >= tuUtc && c.ThoiGianQuet < denUtc)
            .Select(c => new
            {
                c.DriverId,
                c.LoaiHinhChuyenId,
                DonGia = c.LoaiHinhChuyen != null ? c.LoaiHinhChuyen.DonGia : 0m
            })
            .ToListAsync();

        var taiXes = await _db.Drivers.AsNoTracking().OrderBy(d => d.MaTaiXe).ToListAsync();

        var bangLuong = taiXes
            .Select(tx =>
            {
                var cua = banGhi.Where(b => b.DriverId == tx.Id).ToList();
                return new LuongTaiXeItem
                {
                    MaTaiXe = tx.MaTaiXe,
                    HoTen = tx.HoTen,
                    SoContainer = cua.Count(b => b.LoaiHinhChuyenId != null),
                    SoChuaPhanLoai = cua.Count(b => b.LoaiHinhChuyenId == null),
                    TongTien = cua.Where(b => b.LoaiHinhChuyenId != null).Sum(b => b.DonGia)
                };
            })
            .Where(x => x.SoContainer > 0 || x.SoChuaPhanLoai > 0)
            .OrderByDescending(x => x.TongTien)
            .ToList();

        return new KeToanViewModel { TuNgay = tu, DenNgay = den, DsLoaiHinh = dsLoaiHinh, BangLuong = bangLuong };
    }

    private static (DateTime Tu, DateTime Den) XacDinhKhoangNgay(string? tuNgay, string? denNgay)
    {
        var homNay = DateTime.Today;
        int lechThu = homNay.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)homNay.DayOfWeek - 1;
        var tu = homNay.AddDays(-lechThu);
        var den = homNay;

        if (DateTime.TryParseExact(tuNgay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tParse))
        {
            tu = tParse.Date;
        }
        if (DateTime.TryParseExact(denNgay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dParse))
        {
            den = dParse.Date;
        }

        return (tu, den);
    }
}