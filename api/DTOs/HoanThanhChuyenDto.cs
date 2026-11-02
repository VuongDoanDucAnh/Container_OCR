namespace DoAnOlympics.Api.DTOs;

public class HoanThanhChuyenDto
{
    public string? NguoiKyTen { get; set; }
    public string? AnhChuKyBase64 { get; set; }
    public string? AnhGiaoHangBase64 { get; set; }
    public string? AnhGiaoHangLoaiNoiDung { get; set; }
    public int? OdoKetThuc { get; set; }
    public string? AnhOdoPublicId { get; set; }
}