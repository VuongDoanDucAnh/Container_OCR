using System.ComponentModel.DataAnnotations;

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
    public List<ContainerRecord> ContainerRecords { get; set; } = new();

    public byte[]? AnhGiaoHang { get; set; }
    public string? AnhGiaoHangLoaiNoiDung { get; set; }
    public byte[]? AnhChuKy { get; set; }
    public string? NguoiKyTen { get; set; }

    public int? OdoBatDau { get; set; }
    public int? OdoKetThuc { get; set; }

    [MaxLength(200)]
    public string? AnhOdoBatDauPublicId { get; set; }

    [MaxLength(200)]
    public string? AnhOdoKetThucPublicId { get; set; }
}