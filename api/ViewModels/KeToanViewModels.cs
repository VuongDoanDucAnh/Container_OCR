using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.ViewModels;

public class LuongTaiXeItem
{
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public int SoContainer { get; set; }
    public int SoChuaPhanLoai { get; set; }
    public decimal TongTien { get; set; }
}

public class TuyenThongKeItem
{
    public string TenTuyen { get; set; } = string.Empty;
    public int SoDon { get; set; }
    public decimal DoanhThu { get; set; }
    public decimal LuongTaiXe { get; set; }
    public int SoChuyenHoanThanh { get; set; }

    public decimal LoiNhuanTamTinh => DoanhThu - LuongTaiXe;
}

public class KeToanViewModel
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public List<LoaiHinhChuyen> DsLoaiHinh { get; set; } = new();
    public List<LuongTaiXeItem> BangLuong { get; set; } = new();
}