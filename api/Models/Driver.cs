using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DoAnOlympics.Api.Models;

[Index(nameof(Email), IsUnique = true)]
public class Driver
{
    public int Id { get; set; }
    public string MaTaiXe { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? BienSoXe { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.UtcNow;

    public ICollection<ContainerRecord> ContainerRecords { get; set; } = new List<ContainerRecord>();
}