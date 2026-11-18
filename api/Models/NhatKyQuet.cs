using System.ComponentModel.DataAnnotations;

namespace DoAnOlympics.Api.Models;

public class NhatKyQuet
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public Guid? MaYeuCau { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    public string KetQua { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? MaContainerDoc { get; set; }

    public int DoTreMs { get; set; }
    public int KichThuocAnhByte { get; set; }
}