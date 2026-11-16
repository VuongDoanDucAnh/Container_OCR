namespace DoAnOlympics.Api.Models;

public class DonHang
{
    public int Id { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string TenKhachHang { get; set; } = string.Empty;
    public int TuyenDuongId { get; set; }
    public TuyenDuong? TuyenDuong { get; set; }
    public DateTime NgayChay { get; set; }
    public decimal DoanhThu { get; set; } = 0;
    public string LoaiHang { get; set; } = string.Empty;
    public string? GhiChu { get; set; }
    public byte[]? AnhChuKyKhachHang { get; set; }
    public string? AnhChuKyLoaiNoiDung { get; set; }
    public DateTime? ThoiGianKyNhan { get; set; }

    public ICollection<PhanCongChuyen> PhanCongChuyens { get; set; } = new List<PhanCongChuyen>();
}