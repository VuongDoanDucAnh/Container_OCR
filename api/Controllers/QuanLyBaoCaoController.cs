using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;
using DoAnOlympics.Api.ViewModels;

namespace DoAnOlympics.Api.Controllers;

[Route("quanly/ke-toan/bao-cao-thang")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyBaoCaoController : Controller
{
    private readonly AppDbContext _db;

    public QuanLyBaoCaoController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> BaoCaoThang(string? thang)
    {
        var vm = await TaoBaoCaoAsync(thang);
        return View(vm);
    }

    [HttpGet("xuat-excel")]
    public async Task<IActionResult> XuatExcel(string? thang)
    {
        var vm = await TaoBaoCaoAsync(thang);
        byte[] fileBytes = TaoFileExcel(vm);
        string tenFile = $"BaoCaoThuChi_{vm.Thang:MM-yyyy}.xlsx";
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", tenFile);
    }

    private async Task<BaoCaoThangViewModel> TaoBaoCaoAsync(string? thang)
    {
        DateTime dauThang;
        if (DateTime.TryParseExact(thang, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var thangDaChon))
        {
            dauThang = new DateTime(thangDaChon.Year, thangDaChon.Month, 1);
        }
        else
        {
            var homNay = DateTime.Today;
            dauThang = new DateTime(homNay.Year, homNay.Month, 1);
        }

        var cuoiThang = dauThang.AddMonths(1);
        var dauThangUtc = dauThang.ToUniversalTime();
        var cuoiThangUtc = cuoiThang.ToUniversalTime();

        var taiXes = await _db.Drivers
            .AsNoTracking()
            .Where(d => d.BienSoXe != null)
            .OrderBy(d => d.BienSoXe)
            .ToListAsync();

        var donHangTrongThang = await _db.DonHangs
            .AsNoTracking()
            .Where(d => d.NgayChay >= dauThang && d.NgayChay < cuoiThang)
            .Select(d => new { d.DoanhThu, TaiXeIds = d.PhanCongChuyens.Select(p => p.DriverId).Distinct().ToList() })
            .ToListAsync();

        var chuyenHoanThanh = await _db.PhanCongChuyens
            .AsNoTracking()
            .Where(p => p.TrangThai == TrangThaiChuyen.HoanThanh && p.DonHang!.NgayChay >= dauThang && p.DonHang!.NgayChay < cuoiThang)
            .Select(p => p.DriverId)
            .ToListAsync();

        var containerHopLe = await _db.ContainerRecords
            .AsNoTracking()
            .Where(c => c.ChecksumHopLe && c.LoaiHinhChuyenId != null && c.ThoiGianQuet >= dauThangUtc && c.ThoiGianQuet < cuoiThangUtc)
            .Select(c => new { c.DriverId, DonGia = c.LoaiHinhChuyen!.DonGia })
            .ToListAsync();

        var chiPhi = await _db.ChiPhiPhatSinhs
            .AsNoTracking()
            .Where(c => c.ThoiGianGui >= dauThangUtc && c.ThoiGianGui < cuoiThangUtc)
            .Select(c => new { c.DriverId, c.SoTien })
            .ToListAsync();

        var danhSach = taiXes.Select(tx => new BaoCaoThangXeItem
        {
            BienSo = tx.BienSoXe ?? "-",
            HoTen = tx.HoTen,
            DoanhThu = donHangTrongThang.Where(d => d.TaiXeIds.Contains(tx.Id)).Sum(d => d.DoanhThu),
            LuongTX = containerHopLe.Where(c => c.DriverId == tx.Id).Sum(c => c.DonGia),
            TienDau = chiPhi.Where(c => c.DriverId == tx.Id).Sum(c => c.SoTien),
            SoChuyen = chuyenHoanThanh.Count(d => d == tx.Id)
        }).ToList();

        return new BaoCaoThangViewModel { Thang = dauThang, DanhSachXe = danhSach };
    }

    private static byte[] TaoFileExcel(BaoCaoThangViewModel vm)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Bao cao");
        int soCot = vm.DanhSachXe.Count + 2;

        ws.Cell(1, 1).Value = $"BẢNG TỔNG HỢP THU – CHI & LỢI NHUẬN | {vm.DanhSachXe.Count} XE | THÁNG {vm.Thang:MM/yyyy}";
        ws.Range(1, 1, 1, soCot).Merge().Style.Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#00695C"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Row(1).Height = 22;

        ws.Cell(2, 1).Value = "Khoản mục";
        for (int i = 0; i < vm.DanhSachXe.Count; i++)
        {
            var xe = vm.DanhSachXe[i];
            ws.Cell(2, 2 + i).Value = $"{xe.BienSo} ({xe.HoTen})";
        }
        ws.Cell(2, soCot).Value = $"TỔNG {vm.DanhSachXe.Count} XE";
        ws.Range(2, 1, 2, soCot).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#B2DFDB"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Range(2, 1, 2, soCot).Style.Alignment.SetWrapText(true);

        ws.Cell(3, 1).Value = $"LỢI NHUẬN THÁNG {vm.Thang:M/yyyy}";
        for (int i = 0; i < vm.DanhSachXe.Count; i++)
        {
            ws.Cell(3, 2 + i).Value = vm.DanhSachXe[i].LoiNhuan;
        }
        ws.Cell(3, soCot).Value = vm.TongLoiNhuan;
        ws.Range(3, 1, 3, soCot).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8F5E9"));
        ws.Range(3, 2, 3, soCot).Style.NumberFormat.Format = "#,##0";

        ws.Cell(5, 1).Value = "D. PHÂN TÍCH TỶ LỆ";
        ws.Range(5, 1, 5, soCot).Merge().Style.Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#1565C0"));

        ws.Cell(6, 1).Value = "Chỉ số";
        for (int i = 0; i < vm.DanhSachXe.Count; i++)
        {
            ws.Cell(6, 2 + i).Value = vm.DanhSachXe[i].BienSo;
        }
        ws.Cell(6, soCot).Value = "Bình quân";
        ws.Range(6, 1, 6, soCot).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#90CAF9"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        void DongChiSo(int hang, string nhan, Func<BaoCaoThangXeItem, decimal> lay, decimal binhQuan, bool phanTram)
        {
            ws.Cell(hang, 1).Value = nhan;
            for (int i = 0; i < vm.DanhSachXe.Count; i++)
            {
                ws.Cell(hang, 2 + i).Value = lay(vm.DanhSachXe[i]);
            }
            ws.Cell(hang, soCot).Value = binhQuan;
            var rng = ws.Range(hang, 2, hang, soCot);
            rng.Style.NumberFormat.Format = phanTram ? "0.0\"%\"" : "#,##0";
            ws.Range(hang, 1, hang, soCot).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F1F8F6"));
        }

        DongChiSo(7, "Số chuyến", x => x.SoChuyen, vm.BinhQuanSoChuyen, false);
        DongChiSo(8, "Lợi nhuận bình quân/chuyến", x => x.LoiNhuanBinhQuanChuyen, vm.BinhQuanLoiNhuanChuyen, false);
        DongChiSo(9, "Lương TX / Doanh thu", x => x.TyLeLuongTrenDoanhThu, vm.BinhQuanTyLeLuong, true);
        DongChiSo(10, "Tiền dầu / Doanh thu", x => x.TyLeDauTrenDoanhThu, vm.BinhQuanTyLeDau, true);
        DongChiSo(11, "Lợi nhuận / Doanh thu", x => x.TyLeLoiNhuanTrenDoanhThu, vm.BinhQuanTyLeLoiNhuan, true);

        ws.Column(1).Width = 32;
        for (int i = 0; i < vm.DanhSachXe.Count; i++) ws.Column(2 + i).Width = 20;
        ws.Column(soCot).Width = 18;
        ws.SheetView.FreezeColumns(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}