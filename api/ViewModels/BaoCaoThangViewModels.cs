namespace DoAnOlympics.Api.ViewModels;

public class BaoCaoThangXeItem
{
    public string BienSo { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public decimal DoanhThu { get; set; }
    public decimal LuongTX { get; set; }
    public decimal TienDau { get; set; }
    public int SoChuyen { get; set; }

    public decimal LoiNhuan => DoanhThu - LuongTX - TienDau;
    public decimal LoiNhuanBinhQuanChuyen => SoChuyen == 0 ? 0 : LoiNhuan / SoChuyen;
    public decimal TyLeLuongTrenDoanhThu => DoanhThu == 0 ? 0 : LuongTX / DoanhThu * 100;
    public decimal TyLeDauTrenDoanhThu => DoanhThu == 0 ? 0 : TienDau / DoanhThu * 100;
    public decimal TyLeLoiNhuanTrenDoanhThu => DoanhThu == 0 ? 0 : LoiNhuan / DoanhThu * 100;
}

public class BaoCaoThangViewModel
{
    public DateTime Thang { get; set; }
    public List<BaoCaoThangXeItem> DanhSachXe { get; set; } = new();

    public decimal TongLoiNhuan => DanhSachXe.Sum(x => x.LoiNhuan);
    public decimal BinhQuanSoChuyen => DanhSachXe.Count == 0 ? 0 : (decimal)DanhSachXe.Sum(x => x.SoChuyen) / DanhSachXe.Count;
    public decimal BinhQuanLoiNhuanChuyen => DanhSachXe.Count == 0 ? 0 : DanhSachXe.Average(x => x.LoiNhuanBinhQuanChuyen);
    public decimal BinhQuanTyLeLuong => DanhSachXe.Count == 0 ? 0 : DanhSachXe.Average(x => x.TyLeLuongTrenDoanhThu);
    public decimal BinhQuanTyLeDau => DanhSachXe.Count == 0 ? 0 : DanhSachXe.Average(x => x.TyLeDauTrenDoanhThu);
    public decimal BinhQuanTyLeLoiNhuan => DanhSachXe.Count == 0 ? 0 : DanhSachXe.Average(x => x.TyLeLoiNhuanTrenDoanhThu);
}