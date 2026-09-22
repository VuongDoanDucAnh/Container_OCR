namespace DoAnOlympics.Api.Models;

public class TuyenDuong
{
    public int Id { get; set; }
    public string TenTuyen { get; set; } = string.Empty;
    public string DiemDi { get; set; } = string.Empty;
    public string DiemDen { get; set; } = string.Empty;
    public double KhoangCachKm { get; set; }

    public ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();
}