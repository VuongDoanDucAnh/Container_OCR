namespace DoAnOlympics.Api.Models;

public enum TrangThaiChuyen
{
    ChuaNhan,
    DangChay,
    HoanThanh
}

public class PhanCongChuyen
{
    public int Id { get; set; }
    public int DonHangId { get; set; }
    public DonHang? DonHang { get; set; }
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }
    public TrangThaiChuyen TrangThai { get; set; } = TrangThaiChuyen.ChuaNhan;
    public DateTime? ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }
}