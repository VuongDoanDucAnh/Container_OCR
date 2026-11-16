using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnOlympics.Api.Data;
using DoAnOlympics.Api.Models;
using DoAnOlympics.Api.Services;

namespace DoAnOlympics.Api.Controllers;

public class NhapExcelForm
{
    public IFormFile? File { get; set; }
    public bool TaoTuyenMoi { get; set; }
}

[Route("quanly/don-hang/nhap-excel")]
[Authorize(AuthenticationSchemes = QuanLyAuth.Scheme, Roles = QuanLyAuth.Role)]
public class QuanLyNhapExcelController : Controller
{
    private const string HopLe = "hopLe";
    private const string TuyenMoi = "tuyenMoi";
    private const string DaCo = "daCo";
    private const string Loi = "loi";

    private readonly AppDbContext _db;

    public QuanLyNhapExcelController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public IActionResult NhapExcel()
    {
        return View();
    }

    [HttpPost("xem-truoc")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(DocFileDonHangExcel.KichThuocToiDa)]
    public async Task<IActionResult> XemTruoc([FromForm] NhapExcelForm form)
    {
        var (dsPhanTich, thongBaoLoi) = await PhanTichAsync(form.File);
        if (dsPhanTich is null)
        {
            return BadRequest(new { loi = thongBaoLoi });
        }

        return Ok(new
        {
            tongSoDong = dsPhanTich.Count,
            soDonSanSangTao = dsPhanTich.Count(x => x.TrangThai == HopLe),
            soDonCanTaoTuyenMoi = dsPhanTich.Count(x => x.TrangThai == TuyenMoi),
            soDongDaCoTrongHeThong = dsPhanTich.Count(x => x.TrangThai == DaCo),
            soDongLoi = dsPhanTich.Count(x => x.TrangThai == Loi),
            tuyenMoi = dsPhanTich
                .Where(x => x.TrangThai == TuyenMoi)
                .GroupBy(x => x.KhoaTuyen)
                .Select(g => new
                {
                    tenTuyen = g.First().Dong.TenTuyen,
                    diemDi = g.First().Dong.DiemDi,
                    diemDen = g.First().Dong.DiemDen,
                    khoangCachKm = g.First().Dong.KhoangCachKm,
                    soDon = g.Count()
                })
                .ToList(),
            dongDaCo = dsPhanTich
                .Where(x => x.TrangThai == DaCo)
                .Select(x => new { soDong = x.Dong.SoDong, maDon = x.Dong.MaDon })
                .ToList(),
            dongLoi = dsPhanTich
                .Where(x => x.TrangThai == Loi)
                .Select(x => new { soDong = x.Dong.SoDong, maDon = x.Dong.MaDon, loi = x.Dong.Loi })
                .ToList()
        });
    }

    [HttpPost("xac-nhan")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(DocFileDonHangExcel.KichThuocToiDa)]
    public async Task<IActionResult> XacNhan([FromForm] NhapExcelForm form)
    {
        var (dsPhanTich, thongBaoLoi) = await PhanTichAsync(form.File);
        if (dsPhanTich is null)
        {
            return BadRequest(new { loi = thongBaoLoi });
        }

        var dongCanTao = dsPhanTich
            .Where(x => x.TrangThai == HopLe || (form.TaoTuyenMoi && x.TrangThai == TuyenMoi))
            .ToList();

        int soDongBoQuaDoChuaCoTuyen = form.TaoTuyenMoi ? 0 : dsPhanTich.Count(x => x.TrangThai == TuyenMoi);
        int soDongDaCo = dsPhanTich.Count(x => x.TrangThai == DaCo);
        int soDongLoi = dsPhanTich.Count(x => x.TrangThai == Loi);

        if (dongCanTao.Count == 0)
        {
            return Ok(new
            {
                soDonDaTao = 0,
                soTuyenMoiDaTao = 0,
                soDongDaCoTrongHeThong = soDongDaCo,
                soDongLoi,
                soDongBoQuaDoChuaCoTuyen,
                danhSachMaDon = new List<string>()
            });
        }

        var tuyenMoiTheoKhoa = new Dictionary<string, TuyenDuong>();
        var donMoi = new List<DonHang>();

        foreach (var x in dongCanTao)
        {
            var d = x.Dong;
            var don = new DonHang
            {
                MaDon = d.MaDon,
                TenKhachHang = d.TenKhachHang,
                NgayChay = d.NgayChay!.Value.Date,
                LoaiHang = d.LoaiHang,
                DoanhThu = 0
            };

            if (x.TrangThai == HopLe)
            {
                don.TuyenDuongId = x.TuyenId!.Value;
            }
            else
            {
                if (!tuyenMoiTheoKhoa.TryGetValue(x.KhoaTuyen, out var tuyen))
                {
                    tuyen = new TuyenDuong
                    {
                        TenTuyen = d.TenTuyen,
                        DiemDi = d.DiemDi!,
                        DiemDen = d.DiemDen!,
                        KhoangCachKm = d.KhoangCachKm!.Value
                    };
                    tuyenMoiTheoKhoa[x.KhoaTuyen] = tuyen;
                }

                don.TuyenDuong = tuyen;
            }

            donMoi.Add(don);
        }

        _db.TuyenDuongs.AddRange(tuyenMoiTheoKhoa.Values);
        _db.DonHangs.AddRange(donMoi);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { loi = "Có mã đơn bị trùng phát sinh trong lúc nhập. Không có đơn nào được tạo, vui lòng xem trước và thử lại." });
        }

        return Ok(new
        {
            soDonDaTao = donMoi.Count,
            soTuyenMoiDaTao = tuyenMoiTheoKhoa.Count,
            soDongDaCoTrongHeThong = soDongDaCo,
            soDongLoi,
            soDongBoQuaDoChuaCoTuyen,
            danhSachMaDon = donMoi.Select(d => d.MaDon).ToList()
        });
    }

    private async Task<(List<DongPhanTich>? Ds, string? ThongBaoLoi)> PhanTichAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return (null, "Chưa chọn file Excel.");
        }

        if (file.Length > DocFileDonHangExcel.KichThuocToiDa)
        {
            return (null, "File quá lớn (tối đa 5 MB).");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Chỉ hỗ trợ file .xlsx.");
        }

        KetQuaDocExcel doc;
        using (var boNho = new MemoryStream())
        {
            await file.CopyToAsync(boNho);
            boNho.Position = 0;
            doc = DocFileDonHangExcel.Doc(boNho);
        }

        if (doc.LoiChung is not null)
        {
            return (null, doc.LoiChung);
        }

        var tuyenDb = await _db.TuyenDuongs.AsNoTracking().ToListAsync();
        var tuyenTheoTen = new Dictionary<string, int>();
        foreach (var t in tuyenDb)
        {
            tuyenTheoTen.TryAdd(DocFileDonHangExcel.ChuanHoa(t.TenTuyen), t.Id);
        }

        var dsMaCanKiemTra = doc.Dong
            .Where(d => d.Loi.Count == 0)
            .Select(d => d.MaDon)
            .Distinct()
            .ToList();

        var maDaCo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dsMaCanKiemTra.Count > 0)
        {
            var timThay = await _db.DonHangs
                .AsNoTracking()
                .Where(d => dsMaCanKiemTra.Contains(d.MaDon))
                .Select(d => d.MaDon)
                .ToListAsync();
            maDaCo.UnionWith(timThay);
        }

        var ketQua = new List<DongPhanTich>();
        var tuyenMoiDauTien = new Dictionary<string, DongDonHangExcel>();

        foreach (var d in doc.Dong)
        {
            var dp = new DongPhanTich { Dong = d };

            if (d.Loi.Count > 0)
            {
                dp.TrangThai = Loi;
            }
            else if (maDaCo.Contains(d.MaDon))
            {
                dp.TrangThai = DaCo;
            }
            else
            {
                string khoa = DocFileDonHangExcel.ChuanHoa(d.TenTuyen);
                dp.KhoaTuyen = khoa;

                if (tuyenTheoTen.TryGetValue(khoa, out int tuyenId))
                {
                    dp.TrangThai = HopLe;
                    dp.TuyenId = tuyenId;
                }
                else if (d.DiemDi is null || d.DiemDen is null || d.KhoangCachKm is null)
                {
                    d.Loi.Add("Tuyến chưa có trong hệ thống và thiếu điểm đi, điểm đến hoặc số km để tạo mới.");
                    dp.TrangThai = Loi;
                }
                else if (tuyenMoiDauTien.TryGetValue(khoa, out var dongDauTien))
                {
                    if (KhacThongTinTuyen(dongDauTien, d))
                    {
                        d.Loi.Add($"Thông tin tuyến khác với dòng {dongDauTien.SoDong} có cùng tên tuyến.");
                        dp.TrangThai = Loi;
                    }
                    else
                    {
                        dp.TrangThai = TuyenMoi;
                    }
                }
                else
                {
                    tuyenMoiDauTien[khoa] = d;
                    dp.TrangThai = TuyenMoi;
                }
            }

            ketQua.Add(dp);
        }

        return (ketQua, null);
    }

    private static bool KhacThongTinTuyen(DongDonHangExcel a, DongDonHangExcel b)
    {
        return DocFileDonHangExcel.ChuanHoa(a.DiemDi) != DocFileDonHangExcel.ChuanHoa(b.DiemDi)
            || DocFileDonHangExcel.ChuanHoa(a.DiemDen) != DocFileDonHangExcel.ChuanHoa(b.DiemDen)
            || a.KhoangCachKm != b.KhoangCachKm;
    }

    private class DongPhanTich
    {
        public DongDonHangExcel Dong { get; set; } = new();
        public string TrangThai { get; set; } = string.Empty;
        public string KhoaTuyen { get; set; } = string.Empty;
        public int? TuyenId { get; set; }
    }
}