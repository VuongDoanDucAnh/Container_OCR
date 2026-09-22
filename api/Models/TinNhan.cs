namespace DoAnOlympics.Api.Models;

public enum NguoiGuiTin
{
    TaiXe,
    QuanLy
}

public class TinNhan
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }
    public NguoiGuiTin NguoiGui { get; set; }
    public string NoiDung { get; set; } = string.Empty;
    public string? LoaiSuCo { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.UtcNow;
    public bool DaDoc { get; set; }
}