using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.ViewModels;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> TongQuan()
    {
        var homNay = DateTime.Today;
        var batDauUtc = homNay.ToUniversalTime();
        var ketThucUtc = homNay.AddDays(1).ToUniversalTime();

        var don = await LayDonTheoNgayAsync(homNay);
        var chuyens = don.SelectMany(d => d.Chuyens).ToList();

        var vm = new TongQuanViewModel
        {
            Ngay = homNay,
            TongDon = don.Count,
            DonChuaPhanCong = don.Count(d => d.ChuaPhanCong),
            ChuyenChuaNhan = chuyens.Count(c => c.TrangThai == TrangThaiChuyen.ChuaNhan),
            ChuyenDangChay = chuyens.Count(c => c.TrangThai == TrangThaiChuyen.DangChay),
            ChuyenHoanThanh = chuyens.Count(c => c.TrangThai == TrangThaiChuyen.HoanThanh),
            TaiXeDangChay = chuyens
                .Where(c => c.TrangThai == TrangThaiChuyen.DangChay)
                .Select(c => c.MaTaiXe)
                .Distinct()
                .Count(),
            TinChuaDoc = await _db.TinNhans.CountAsync(t => t.NguoiGui == NguoiGuiTin.TaiXe && !t.DaDoc),
            QuetHomNay = await _db.ContainerRecords.CountAsync(c => c.ThoiGianQuet >= batDauUtc && c.ThoiGianQuet < ketThucUtc),
            CanChuY = don.Where(d => d.CanChuY).ToList(),
            DangChay = don.Where(d => d.Chuyens.Any(c => c.TrangThai == TrangThaiChuyen.DangChay)).ToList()
        };

        return View(vm);
    }

    [HttpGet("don-hang")]
    public async Task<IActionResult> DonVaTuyen(string? ngay)
    {
        var ngayXem = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(ngay)
            && DateTime.TryParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayDaChon))
        {
            ngayXem = ngayDaChon.Date;
        }

        var don = await LayDonTheoNgayAsync(ngayXem);

        var theoTuyen = don
            .GroupBy(d => new { d.TenTuyen, d.DiemDi, d.DiemDen, d.KhoangCachKm })
            .Select(g => new TuyenTongHopItem
            {
                TenTuyen = g.Key.TenTuyen,
                DiemDi = g.Key.DiemDi,
                DiemDen = g.Key.DiemDen,
                KhoangCachKm = g.Key.KhoangCachKm,
                SoDon = g.Count(),
                SoDangChay = g.Count(d => d.Chuyens.Any(c => c.TrangThai == TrangThaiChuyen.DangChay)),
                SoHoanThanh = g.Count(d => d.Chuyens.Count > 0 && d.Chuyens.All(c => c.TrangThai == TrangThaiChuyen.HoanThanh))
            })
            .OrderByDescending(t => t.SoDon)
            .ToList();

        return View(new DonVaTuyenViewModel { Ngay = ngayXem, Don = don, TheoTuyen = theoTuyen });
    }

    [HttpGet("don-hang/tao")]
    public async Task<IActionResult> TaoDonHang(string? ngay)
    {
        var ngayXem = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(ngay)
            && DateTime.TryParseExact(ngay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayDaChon))
        {
            ngayXem = ngayDaChon.Date;
        }

        ViewBag.DsTuyen = await _db.TuyenDuongs.AsNoTracking().OrderBy(t => t.TenTuyen).ToListAsync();
        return View(new TaoDonHangForm { NgayChay = ngayXem });
    }

    [HttpPost("don-hang/tao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoDonHang(TaoDonHangForm form)
    {
        string tenKhach = (form.TenKhachHang ?? string.Empty).Trim();
        string loaiHang = (form.LoaiHang ?? string.Empty).Trim();

        bool tuyenTonTai = await _db.TuyenDuongs.AnyAsync(t => t.Id == form.TuyenDuongId);

        if (tenKhach.Length == 0 || loaiHang.Length == 0 || !tuyenTonTai)
        {
            ViewBag.DsTuyen = await _db.TuyenDuongs.AsNoTracking().OrderBy(t => t.TenTuyen).ToListAsync();
            ViewBag.Loi = "Vui lòng chọn tuyến và điền đầy đủ khách hàng, loại hàng.";
            return View(form);
        }

        var ngayChay = form.NgayChay.Date;
        string maDon = await SinhMaDonAsync(ngayChay);

        var donMoi = new DonHang
        {
            MaDon = maDon,
            TenKhachHang = tenKhach,
            TuyenDuongId = form.TuyenDuongId,
            NgayChay = ngayChay,
            LoaiHang = loaiHang,
            GhiChu = string.IsNullOrWhiteSpace(form.GhiChu) ? null : form.GhiChu.Trim(),
            DoanhThu = form.DoanhThu
        };

        _db.DonHangs.Add(donMoi);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(DonVaTuyen), new { ngay = ngayChay.ToString("yyyy-MM-dd") });
    }

    [HttpPost("don-hang/doanh-thu/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatDoanhThu(int id, decimal doanhThu, string? ngay)
    {
        if (doanhThu < 0) return RedirectToAction(nameof(DonVaTuyen), new { ngay });

        var don = await _db.DonHangs.FirstOrDefaultAsync(d => d.Id == id);
        if (don is null) return NotFound();

        don.DoanhThu = doanhThu;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(DonVaTuyen), new { ngay });
    }

    [HttpGet("don-hang/gan-tai-xe/{donHangId:int}")]
    public async Task<IActionResult> GanTaiXe(int donHangId)
    {
        var don = await _db.DonHangs
            .AsNoTracking()
            .Where(d => d.Id == donHangId)
            .Select(d => new { d.Id, d.MaDon, d.TenKhachHang })
            .FirstOrDefaultAsync();

        if (don is null) return NotFound();

        ViewBag.MaDon = don.MaDon;
        ViewBag.TenKhachHang = don.TenKhachHang;
        ViewBag.DsTaiXe = await _db.Drivers.AsNoTracking().OrderBy(d => d.MaTaiXe).ToListAsync();

        return View(new GanTaiXeForm { DonHangId = don.Id });
    }

    [HttpPost("don-hang/gan-tai-xe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GanTaiXe(GanTaiXeForm form)
    {
        var don = await _db.DonHangs.FirstOrDefaultAsync(d => d.Id == form.DonHangId);
        if (don is null) return NotFound();

        bool taiXeTonTai = await _db.Drivers.AnyAsync(d => d.Id == form.DriverId);
        if (!taiXeTonTai)
        {
            ModelState.AddModelError(string.Empty, "Tài xế không hợp lệ.");
            ViewBag.MaDon = don.MaDon;
            ViewBag.TenKhachHang = don.TenKhachHang;
            ViewBag.DsTaiXe = await _db.Drivers.AsNoTracking().OrderBy(d => d.MaTaiXe).ToListAsync();
            ViewBag.Loi = "Tài xế không hợp lệ.";
            return View(form);
        }

        _db.PhanCongChuyens.Add(new PhanCongChuyen
        {
            DonHangId = form.DonHangId,
            DriverId = form.DriverId,
            TrangThai = TrangThaiChuyen.ChuaNhan
        });
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(DonVaTuyen), new { ngay = don.NgayChay.ToString("yyyy-MM-dd") });
    }

    [HttpGet("tai-xe")]
    public async Task<IActionResult> TaiXe()
    {
        var homNay = DateTime.Today;
        var batDauUtc = homNay.ToUniversalTime();
        var ketThucUtc = homNay.AddDays(1).ToUniversalTime();

        var taiXes = await _db.Drivers
            .AsNoTracking()
            .OrderBy(d => d.MaTaiXe)
            .Select(d => new TaiXeItem
            {
                Id = d.Id,
                MaTaiXe = d.MaTaiXe,
                HoTen = d.HoTen,
                NgayTao = d.NgayTao,
                BienSoXe = d.BienSoXe,
                TongContainerDaQuet = d.ContainerRecords.Count(),
                QuetHomNay = d.ContainerRecords.Count(c => c.ThoiGianQuet >= batDauUtc && c.ThoiGianQuet < ketThucUtc)
            })
            .ToListAsync();

        var chuyens = await _db.PhanCongChuyens
            .AsNoTracking()
            .Where(p => p.DonHang!.NgayChay == homNay)
            .Select(p => new ChuyenTaiXeItem
            {
                DriverId = p.DriverId,
                MaDon = p.DonHang!.MaDon,
                TenTuyen = p.DonHang!.TuyenDuong!.TenTuyen,
                TrangThai = p.TrangThai
            })
            .ToListAsync();

        foreach (var taiXe in taiXes)
        {
            taiXe.Chuyens = chuyens.Where(c => c.DriverId == taiXe.Id).ToList();
        }

        var vm = new TaiXeViewModel
        {
            Ngay = homNay,
            TaiXes = taiXes.OrderBy(t => t.ThuTuUuTien).ThenBy(t => t.MaTaiXe).ToList()
        };

        return View(vm);
    }

    [HttpPost("tai-xe/bien-so/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatBienSo(int id, string? bienSo)
    {
        var tx = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id);
        if (tx is null) return NotFound();

        tx.BienSoXe = string.IsNullOrWhiteSpace(bienSo) ? null : bienSo.Trim();
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(TaiXe));
    }

    private async Task<string> SinhMaDonAsync(DateTime ngayChay)
    {
        int soThuTu = await _db.DonHangs.CountAsync(d => d.NgayChay == ngayChay) + 1;

        for (int laoi = 0; laoi < 20; laoi++)
        {
            string ung = $"DH{ngayChay:yyyyMMdd}-{soThuTu:00}";
            if (!await _db.DonHangs.AnyAsync(d => d.MaDon == ung))
            {
                return ung;
            }
            soThuTu++;
        }

        return $"DH{ngayChay:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private async Task<List<DonHangItem>> LayDonTheoNgayAsync(DateTime ngay)
    {
        return await _db.DonHangs
            .AsNoTracking()
            .Where(d => d.NgayChay == ngay)
            .OrderBy(d => d.MaDon)
            .Select(d => new DonHangItem
            {
                Id = d.Id,
                MaDon = d.MaDon,
                TenKhachHang = d.TenKhachHang,
                TenTuyen = d.TuyenDuong!.TenTuyen,
                DiemDi = d.TuyenDuong!.DiemDi,
                DiemDen = d.TuyenDuong!.DiemDen,
                KhoangCachKm = d.TuyenDuong!.KhoangCachKm,
                LoaiHang = d.LoaiHang,
                GhiChu = d.GhiChu,
                DoanhThu = d.DoanhThu,
                CoChuKy = d.AnhChuKyKhachHang != null || d.PhanCongChuyens.Any(p => p.AnhChuKy != null),
                Chuyens = d.PhanCongChuyens
                    .Select(p => new ChuyenItem
                    {
                        MaTaiXe = p.Driver!.MaTaiXe,
                        HoTenTaiXe = p.Driver!.HoTen,
                        TrangThai = p.TrangThai,
                        ThoiGianBatDau = p.ThoiGianBatDau,
                        ThoiGianKetThuc = p.ThoiGianKetThuc,
                        MaContainerDaQuet = p.ContainerRecords.Select(c => c.MaContainer).ToList()
                    })
                    .ToList()
            })
            .ToListAsync();
    }

    [HttpGet("don-hang/chu-ky/{id:int}")]
    public async Task<IActionResult> XemChuKy(int id)
    {
        var anhChuyen = await _db.PhanCongChuyens
            .AsNoTracking()
            .Where(p => p.DonHangId == id && p.AnhChuKy != null)
            .OrderByDescending(p => p.ThoiGianKetThuc)
            .Select(p => p.AnhChuKy)
            .FirstOrDefaultAsync();

        if (anhChuyen is not null) return File(anhChuyen, "image/png");

        var don = await _db.DonHangs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (don?.AnhChuKyKhachHang is null) return NotFound();

        return File(don.AnhChuKyKhachHang, don.AnhChuKyLoaiNoiDung ?? "image/png");
    }
}