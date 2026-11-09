namespace DoAnOlympics.Api.Models;

public enum TrangThaiChiPhi
{
    ChoGiaiChi,
    DaGiaiChi
}

public class ChiPhiPhatSinh
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }
    public decimal SoTien { get; set; }
    public decimal GiaDau { get; set; }
    public decimal SoLit { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiGianGui { get; set; } = DateTime.UtcNow;
    public TrangThaiChiPhi TrangThai { get; set; } = TrangThaiChiPhi.ChoGiaiChi;
    public DateTime? ThoiGianGiaiChi { get; set; }
}