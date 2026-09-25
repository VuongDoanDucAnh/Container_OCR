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

public class KeToanViewModel
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public List<LoaiHinhChuyen> DsLoaiHinh { get; set; } = new();
    public List<LuongTaiXeItem> BangLuong { get; set; } = new();
}