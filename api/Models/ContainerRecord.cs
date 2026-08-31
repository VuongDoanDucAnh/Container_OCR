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
}
