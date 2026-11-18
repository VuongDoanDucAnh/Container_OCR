namespace DoAnOlympics.Api.DTOs;

public class DocHoaDonKetQuaAi
{
    public bool LaHoaDon { get; set; }
    public decimal? SoLit { get; set; }
    public decimal? DonGia { get; set; }
    public decimal? ThanhTien { get; set; }
    public string? LoaiNhienLieu { get; set; }
    public string? NgayGio { get; set; }
    public string? TenCuaHang { get; set; }
    public List<string> TruongNghiNgo { get; set; } = new();
}

public class DocHoaDonResponseDto
{
    public bool ThanhCong { get; set; }
    public string? Loi { get; set; }
    public string? HuongGiaiQuyet { get; set; }
    public string? MaLoi { get; set; }
    public bool LaHoaDon { get; set; }
    public decimal? SoLit { get; set; }
    public decimal? DonGia { get; set; }
    public decimal? ThanhTien { get; set; }
    public string? LoaiNhienLieu { get; set; }
    public string? NgayGio { get; set; }
    public string? TenCuaHang { get; set; }
    public bool KhopSoLieu { get; set; }
    public string? CanhBao { get; set; }
    public List<string> TruongNghiNgo { get; set; } = new();
}