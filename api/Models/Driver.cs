namespace DoAnOlympics.Api.Models;

/// <summary>
/// Tài xế - mỗi người có 1 mã số riêng (MaTaiXe) dùng để đối chiếu bảng lương.
/// </summary>
public class Driver
{
    public int Id { get; set; }
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;

    public ICollection<ContainerRecord> ContainerRecords { get; set; } = new List<ContainerRecord>();
}
