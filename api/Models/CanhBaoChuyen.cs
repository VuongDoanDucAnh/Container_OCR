using System.ComponentModel.DataAnnotations;

namespace DoAnOlympics.Api.Models;

public enum LoaiCanhBao
{
    OdoDauLech,
    QuangDuongLech
}

public enum TrangThaiCanhBao
{
    Mo,
    DaXuLy
}

public class CanhBaoChuyen
{
    public int Id { get; set; }
    public int PhanCongChuyenId { get; set; }
    public PhanCongChuyen? PhanCongChuyen { get; set; }
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }
    public LoaiCanhBao Loai { get; set; }

    [MaxLength(500)]
    public string MoTa { get; set; } = string.Empty;

    public double GiaTriDo { get; set; }
    public double GiaTriMongDoi { get; set; }
    public TrangThaiCanhBao TrangThai { get; set; } = TrangThaiCanhBao.Mo;

    [MaxLength(500)]
    public string? GhiChuTaiXe { get; set; }

    [MaxLength(500)]
    public string? GhiChuQuanLy { get; set; }

    public DateTime ThoiGianTao { get; set; } = DateTime.UtcNow;
    public DateTime? ThoiGianXuLy { get; set; }
    public byte[]? RowVersion { get; set; }
}