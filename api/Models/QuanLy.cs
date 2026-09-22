namespace DoAnOlympics.Api.Models;

public class QuanLy
{
    public int Id { get; set; }
    public string TenDangNhap { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;
}