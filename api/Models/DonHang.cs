namespace DoAnOlympics.Api.Models;

public class DonHang
{
    public int Id { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public int TuyenDuongId { get; set; }
    public TuyenDuong? TuyenDuong { get; set; }
    public DateTime NgayChay { get; set; }
    public string LoaiHang { get; set; } = string.Empty;
    public string? GhiChu { get; set; }

    public ICollection<PhanCongChuyen> PhanCongChuyens { get; set; } = new List<PhanCongChuyen>();
}