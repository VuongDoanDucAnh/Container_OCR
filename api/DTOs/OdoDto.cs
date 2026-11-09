namespace DoAnOlympics.Api.DTOs;

public class DocOdoKetQuaAi
{
    public bool LaDongHoOdo { get; set; }
    public int? Odo { get; set; }
    public string? DonVi { get; set; }
    public List<string> TruongNghiNgo { get; set; } = new();
}

public class DocOdoResponseDto
{
    public bool ThanhCong { get; set; }
    public bool LaDongHoOdo { get; set; }
    public int? Odo { get; set; }
    public string? DonVi { get; set; }
    public string? AnhPublicId { get; set; }
    public string? CanhBao { get; set; }
    public List<string> TruongNghiNgo { get; set; } = new();
    public string? Loi { get; set; }
    public string? HuongGiaiQuyet { get; set; }
    public string? MaLoi { get; set; }
}

public class BatDauChuyenDto
{
    public int? OdoBatDau { get; set; }
    public string? AnhOdoPublicId { get; set; }
}