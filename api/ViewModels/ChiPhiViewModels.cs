namespace DoAnOlympics.Api.ViewModels;

public class ChiPhiItem
{
    public int Id { get; set; }
    public decimal SoTien { get; set; }
    public decimal GiaDau { get; set; }
    public decimal SoLit { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiGianGui { get; set; }
    public bool DaGiaiChi { get; set; }
}

public class ChiPhiTaiXeNhom
{
    public int DriverId { get; set; }
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public List<ChiPhiItem> DanhSach { get; set; } = new();
    public decimal TongChoGiaiChi => DanhSach.Where(c => !c.DaGiaiChi).Sum(c => c.SoTien);
    public bool CoChoGiaiChi => DanhSach.Any(c => !c.DaGiaiChi);
}

public class ChiPhiViewModel
{
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public List<ChiPhiTaiXeNhom> TheoTaiXe { get; set; } = new();
}