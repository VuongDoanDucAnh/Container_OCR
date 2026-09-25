namespace DoAnOlympics.Api.Models;

public class ContainerRecord
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public Driver? Driver { get; set; }

    public string MaContainer { get; set; } = string.Empty;
    public bool ChecksumHopLe { get; set; }
    public DateTime ThoiGianQuet { get; set; } = DateTime.UtcNow;
    public bool DaGhiSheet { get; set; } = false;

    public int? PhanCongChuyenId { get; set; }
    public PhanCongChuyen? PhanCongChuyen { get; set; }

    public int? LoaiHinhChuyenId { get; set; }
    public LoaiHinhChuyen? LoaiHinhChuyen { get; set; }
    public string? DiaDiem { get; set; }
}