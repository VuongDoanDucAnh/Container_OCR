using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.ViewModels;

public class ChuyenItem
{
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTenTaiXe { get; set; } = string.Empty;
    public TrangThaiChuyen TrangThai { get; set; }
    public DateTime? ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }
}

public class DonHangItem
{
    public string MaDon { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public string TenTuyen { get; set; } = string.Empty;
    public string DiemDi { get; set; } = string.Empty;
    public string DiemDen { get; set; } = string.Empty;
    public double KhoangCachKm { get; set; }
    public string LoaiHang { get; set; } = string.Empty;
    public string? GhiChu { get; set; }
    public List<ChuyenItem> Chuyens { get; set; } = new();

    public bool ChuaPhanCong => Chuyens.Count == 0;

    public bool CanChuY => Chuyens.Count == 0 || Chuyens.All(c => c.TrangThai == TrangThaiChuyen.ChuaNhan);
}

public class TuyenTongHopItem
{
    public string TenTuyen { get; set; } = string.Empty;
    public string DiemDi { get; set; } = string.Empty;
    public string DiemDen { get; set; } = string.Empty;
    public double KhoangCachKm { get; set; }
    public int SoDon { get; set; }
    public int SoDangChay { get; set; }
    public int SoHoanThanh { get; set; }
}

public class TongQuanViewModel
{
    public DateTime Ngay { get; set; }
    public int TongDon { get; set; }
    public int DonChuaPhanCong { get; set; }
    public int ChuyenChuaNhan { get; set; }
    public int ChuyenDangChay { get; set; }
    public int ChuyenHoanThanh { get; set; }
    public int TaiXeDangChay { get; set; }
    public int TinChuaDoc { get; set; }
    public int QuetHomNay { get; set; }
    public List<DonHangItem> CanChuY { get; set; } = new();
    public List<DonHangItem> DangChay { get; set; } = new();

    public int TongChuyen => ChuyenChuaNhan + ChuyenDangChay + ChuyenHoanThanh;

    public int PhanTramHoanThanh => TongChuyen == 0 ? 0 : ChuyenHoanThanh * 100 / TongChuyen;
}

public class DonVaTuyenViewModel
{
    public DateTime Ngay { get; set; }
    public List<DonHangItem> Don { get; set; } = new();
    public List<TuyenTongHopItem> TheoTuyen { get; set; } = new();
}

public class ChuyenTaiXeItem
{
    public int DriverId { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string TenTuyen { get; set; } = string.Empty;
    public TrangThaiChuyen TrangThai { get; set; }
}

public class TaiXeItem
{
    public int Id { get; set; }
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public DateTime NgayTao { get; set; }
    public int TongContainerDaQuet { get; set; }
    public int QuetHomNay { get; set; }
    public List<ChuyenTaiXeItem> Chuyens { get; set; } = new();

    public int ThuTuUuTien =>
        Chuyens.Any(c => c.TrangThai == TrangThaiChuyen.DangChay) ? 0 :
        Chuyens.Any(c => c.TrangThai == TrangThaiChuyen.ChuaNhan) ? 1 :
        Chuyens.Count > 0 ? 2 : 3;

    public string TrangThaiTongHop => ThuTuUuTien switch
    {
        0 => "Đang chạy",
        1 => "Chờ nhận chuyến",
        2 => "Đã hoàn thành",
        _ => "Không có chuyến"
    };

    public string LopCss => ThuTuUuTien switch
    {
        0 => "chay",
        1 => "cho",
        2 => "xong",
        _ => "khong"
    };
}

public class TaiXeViewModel
{
    public DateTime Ngay { get; set; }
    public List<TaiXeItem> TaiXes { get; set; } = new();
}

public static class NhanHienThi
{
    public static string TrangThai(TrangThaiChuyen trangThai) => trangThai switch
    {
        TrangThaiChuyen.ChuaNhan => "Chưa nhận",
        TrangThaiChuyen.DangChay => "Đang chạy",
        _ => "Hoàn thành"
    };

    public static string LopCss(TrangThaiChuyen trangThai) => trangThai switch
    {
        TrangThaiChuyen.ChuaNhan => "cho",
        TrangThaiChuyen.DangChay => "chay",
        _ => "xong"
    };
}