using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SQLite;

namespace App_OCR;

public enum TrangThaiAnh
{
    ChoGui = 0,
    DangGui = 1,
    CanChupLai = 2,
    XungDot = 3
}

public enum LoaiKetQuaGui
{
    ThanhCong,
    CanChupLai,
    XungDot,
    ThuLaiSau,
    HetPhien
}

[Table("AnhChoGui")]
public class AnhChoGui
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MaTaiXe { get; set; } = string.Empty;
    public string DuongDanAnh { get; set; } = string.Empty;
    public DateTime ThoiGianChupUtc { get; set; }
    public double? ViDo { get; set; }
    public double? KinhDo { get; set; }
    public int? LoaiHinhChuyenId { get; set; }
    public string? DiaDiem { get; set; }
    public TrangThaiAnh TrangThai { get; set; } = TrangThaiAnh.ChoGui;
    public int SoLanThu { get; set; }
    public string? LoiCuoi { get; set; }
    public DateTime ThoiGianTaoUtc { get; set; } = DateTime.UtcNow;
}

public class KetQuaGui
{
    public LoaiKetQuaGui Loai { get; set; }
    public string? MaContainer { get; set; }
    public string? MaLoi { get; set; }
    public string? ThongBao { get; set; }
}

public class TongKetDongBo
{
    public int DaGui { get; set; }
    public int CanXuLy { get; set; }
    public int ConCho { get; set; }
    public bool HetPhien { get; set; }
    public bool BoQua { get; set; }
}

public class HangDoiAnh
{
    private readonly SQLiteAsyncConnection _db;
    private readonly string _thuMucAnh;
    private readonly SemaphoreSlim _khoaKhoiTao = new(1, 1);
    private bool _daKhoiTao;

    public HangDoiAnh()
    {
        var thuMucGoc = FileSystem.AppDataDirectory;
        _db = new SQLiteAsyncConnection(Path.Combine(thuMucGoc, "hangdoi.db3"));
        _thuMucAnh = Path.Combine(thuMucGoc, "hangdoi");
    }

    private async Task KhoiTaoAsync()
    {
        if (_daKhoiTao) return;

        await _khoaKhoiTao.WaitAsync();
        try
        {
            if (_daKhoiTao) return;

            Directory.CreateDirectory(_thuMucAnh);
            await _db.CreateTableAsync<AnhChoGui>();
            await _db.ExecuteAsync(
                "UPDATE AnhChoGui SET TrangThai = ? WHERE TrangThai = ?",
                (int)TrangThaiAnh.ChoGui,
                (int)TrangThaiAnh.DangGui);
            _daKhoiTao = true;
        }
        finally
        {
            _khoaKhoiTao.Release();
        }
    }

    public async Task<AnhChoGui> ThemAsync(
        Stream anh,
        string maTaiXe,
        DateTime thoiGianChupUtc,
        double? viDo,
        double? kinhDo,
        int? loaiHinhChuyenId,
        string? diaDiem)
    {
        await KhoiTaoAsync();

        var muc = new AnhChoGui
        {
            MaTaiXe = maTaiXe,
            ThoiGianChupUtc = thoiGianChupUtc,
            ViDo = viDo,
            KinhDo = kinhDo,
            LoaiHinhChuyenId = loaiHinhChuyenId,
            DiaDiem = diaDiem
        };
        muc.DuongDanAnh = Path.Combine(_thuMucAnh, $"{muc.Id}.jpg");

        try
        {
            await using (var tep = File.Create(muc.DuongDanAnh))
            {
                await anh.CopyToAsync(tep);
            }

            await _db.InsertAsync(muc);
        }
        catch
        {
            XoaTep(muc.DuongDanAnh);
            throw;
        }

        return muc;
    }

    public async Task<List<AnhChoGui>> LayChoGuiAsync(string maTaiXe)
    {
        await KhoiTaoAsync();
        return await _db.Table<AnhChoGui>()
            .Where(a => a.MaTaiXe == maTaiXe && a.TrangThai == TrangThaiAnh.ChoGui)
            .OrderBy(a => a.ThoiGianChupUtc)
            .ToListAsync();
    }

    public async Task<List<AnhChoGui>> LayAnhLoiAsync(string maTaiXe)
    {
        await KhoiTaoAsync();
        return await _db.Table<AnhChoGui>()
            .Where(a => a.MaTaiXe == maTaiXe
                && (a.TrangThai == TrangThaiAnh.CanChupLai || a.TrangThai == TrangThaiAnh.XungDot))
            .OrderBy(a => a.ThoiGianChupUtc)
            .ToListAsync();
    }

    public async Task<int> DemChoGuiAsync(string maTaiXe)
    {
        await KhoiTaoAsync();
        return await _db.Table<AnhChoGui>()
            .Where(a => a.MaTaiXe == maTaiXe
                && (a.TrangThai == TrangThaiAnh.ChoGui || a.TrangThai == TrangThaiAnh.DangGui))
            .CountAsync();
    }

    public async Task<int> DemAsync(string maTaiXe)
    {
        await KhoiTaoAsync();
        return await _db.Table<AnhChoGui>()
            .Where(a => a.MaTaiXe == maTaiXe)
            .CountAsync();
    }

    public async Task CapNhatTrangThaiAsync(
        string id,
        TrangThaiAnh trangThai,
        string? loi = null,
        bool tangSoLanThu = false)
    {
        await KhoiTaoAsync();

        var muc = await _db.FindAsync<AnhChoGui>(id);
        if (muc is null) return;

        muc.TrangThai = trangThai;
        muc.LoiCuoi = loi;
        if (tangSoLanThu) muc.SoLanThu++;
        await _db.UpdateAsync(muc);
    }

    public async Task XoaAsync(string id)
    {
        await KhoiTaoAsync();

        var muc = await _db.FindAsync<AnhChoGui>(id);
        if (muc is null) return;

        await _db.DeleteAsync(muc);
        XoaTep(muc.DuongDanAnh);
    }

    public async Task XoaAnhLoiAsync(string maTaiXe)
    {
        var dsLoi = await LayAnhLoiAsync(maTaiXe);
        foreach (var muc in dsLoi)
        {
            await XoaAsync(muc.Id);
        }
    }

    private static void XoaTep(string duongDan)
    {
        try
        {
            if (File.Exists(duongDan)) File.Delete(duongDan);
        }
        catch (IOException)
        {
        }
    }
}

public class DongBoAnh
{
    private const int SoLanThuToiDa = 5;
    private static readonly TimeSpan ThoiGianChoMoiAnh = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KhoangCachTuDong = TimeSpan.FromSeconds(10);
    private static readonly SemaphoreSlim _khoaDongBo = new(1, 1);
    private static DateTime _lanTuDongCuoiUtc = DateTime.MinValue;

    private readonly HangDoiAnh _hangDoi;
    private readonly HttpClient _http;
    private readonly string _diaChiApi;

    public DongBoAnh(HangDoiAnh hangDoi, HttpClient http, string diaChiApi)
    {
        _hangDoi = hangDoi;
        _http = http;
        _diaChiApi = diaChiApi;
    }

    public async Task<KetQuaGui> GuiMotAnhAsync(AnhChoGui muc)
    {
        await _khoaDongBo.WaitAsync();
        try
        {
            return await GuiTrongKhoaAsync(muc);
        }
        finally
        {
            _khoaDongBo.Release();
        }
    }

    public async Task<TongKetDongBo> DongBoTatCaAsync(string maTaiXe, bool tuDong)
    {
        var tongKet = new TongKetDongBo();

        if (tuDong && DateTime.UtcNow - _lanTuDongCuoiUtc < KhoangCachTuDong)
        {
            tongKet.BoQua = true;
            return tongKet;
        }

        if (!await _khoaDongBo.WaitAsync(0))
        {
            tongKet.BoQua = true;
            return tongKet;
        }

        try
        {
            _lanTuDongCuoiUtc = DateTime.UtcNow;

            var dsCho = await _hangDoi.LayChoGuiAsync(maTaiXe);
            foreach (var muc in dsCho)
            {
                var ketQua = await GuiTrongKhoaAsync(muc);

                if (ketQua.Loai == LoaiKetQuaGui.ThanhCong)
                {
                    tongKet.DaGui++;
                    continue;
                }

                if (ketQua.Loai is LoaiKetQuaGui.CanChupLai or LoaiKetQuaGui.XungDot)
                {
                    tongKet.CanXuLy++;
                    continue;
                }

                if (ketQua.Loai == LoaiKetQuaGui.HetPhien)
                {
                    tongKet.HetPhien = true;
                }

                break;
            }

            tongKet.ConCho = await _hangDoi.DemChoGuiAsync(maTaiXe);
        }
        finally
        {
            _khoaDongBo.Release();
        }

        return tongKet;
    }

    private async Task<KetQuaGui> GuiTrongKhoaAsync(AnhChoGui muc)
    {
        var token = await SecureStorage.Default.GetAsync("jwt_token");
        if (string.IsNullOrEmpty(token))
        {
            return new KetQuaGui
            {
                Loai = LoaiKetQuaGui.HetPhien,
                ThongBao = "Chưa đăng nhập hoặc phiên đã hết hạn, vui lòng đăng nhập lại để gửi tiếp"
            };
        }

        if (!File.Exists(muc.DuongDanAnh))
        {
            const string loiMatTep = "Không tìm thấy tệp ảnh trên thiết bị";
            await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.CanChupLai, loiMatTep);
            return new KetQuaGui { Loai = LoaiKetQuaGui.CanChupLai, ThongBao = loiMatTep };
        }

        await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.DangGui);

        try
        {
            using var huy = new CancellationTokenSource(ThoiGianChoMoiAnh);
            using var noiDungGui = new MultipartFormDataContent();
            using var luongAnh = File.OpenRead(muc.DuongDanAnh);
            using var noiDungAnh = new StreamContent(luongAnh);
            noiDungAnh.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            noiDungGui.Add(noiDungAnh, "anh", Path.GetFileName(muc.DuongDanAnh));

            noiDungGui.Add(new StringContent(muc.Id), "maYeuCau");

            var gioChup = DateTime.SpecifyKind(muc.ThoiGianChupUtc, DateTimeKind.Utc);
            noiDungGui.Add(
                new StringContent(gioChup.ToString("O", CultureInfo.InvariantCulture)),
                "thoiGianChup");

            if (muc.ViDo.HasValue && muc.KinhDo.HasValue)
            {
                noiDungGui.Add(
                    new StringContent(muc.ViDo.Value.ToString(CultureInfo.InvariantCulture)),
                    "viDo");
                noiDungGui.Add(
                    new StringContent(muc.KinhDo.Value.ToString(CultureInfo.InvariantCulture)),
                    "kinhDo");
            }

            if (muc.LoaiHinhChuyenId.HasValue)
            {
                noiDungGui.Add(
                    new StringContent(muc.LoaiHinhChuyenId.Value.ToString(CultureInfo.InvariantCulture)),
                    "loaiHinhChuyenId");
            }

            if (!string.IsNullOrWhiteSpace(muc.DiaDiem))
            {
                noiDungGui.Add(new StringContent(muc.DiaDiem), "diaDiem");
            }

            using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{_diaChiApi}/api/container/scan")
            {
                Content = noiDungGui
            };
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var phanHoi = await _http.SendAsync(yeuCau, huy.Token);
            return await XuLyPhanHoiAsync(muc, phanHoi);
        }
        catch (Exception ex)
        {
            var thongBao = ex is OperationCanceledException
                ? "Hết thời gian chờ máy chủ phản hồi"
                : "Không kết nối được máy chủ";
            return await ThuLaiSauAsync(muc, thongBao, tinhVaoGioiHan: false);
        }
    }

    private async Task<KetQuaGui> XuLyPhanHoiAsync(AnhChoGui muc, HttpResponseMessage phanHoi)
    {
        if (phanHoi.StatusCode == HttpStatusCode.Unauthorized)
        {
            const string loiPhien = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại để gửi tiếp";
            await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.ChoGui, loiPhien);
            return new KetQuaGui { Loai = LoaiKetQuaGui.HetPhien, ThongBao = loiPhien };
        }

        PhanHoiScan? noiDung = null;
        try
        {
            noiDung = await phanHoi.Content.ReadFromJsonAsync<PhanHoiScan>();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
        }

        if (phanHoi.IsSuccessStatusCode && noiDung?.ThanhCong == true)
        {
            await _hangDoi.XoaAsync(muc.Id);
            return new KetQuaGui
            {
                Loai = LoaiKetQuaGui.ThanhCong,
                MaContainer = noiDung.MaContainer,
                MaLoi = noiDung.MaLoi,
                ThongBao = noiDung.CanhBao
            };
        }

        var maLoi = noiDung?.MaLoi;
        var thongBao = noiDung?.HuongGiaiQuyet ?? noiDung?.Loi ?? $"Lỗi máy chủ ({(int)phanHoi.StatusCode})";

        if (maLoi is "KHONG_DOC_DUOC" or "CHECKSUM_SAI")
        {
            await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.CanChupLai, thongBao);
            return new KetQuaGui { Loai = LoaiKetQuaGui.CanChupLai, MaLoi = maLoi, ThongBao = thongBao };
        }

        if (maLoi is "LAP_DON" or "TRUNG_KHAC_TAI_XE")
        {
            await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.XungDot, thongBao);
            return new KetQuaGui { Loai = LoaiKetQuaGui.XungDot, MaLoi = maLoi, ThongBao = thongBao };
        }

        var ketQua = await ThuLaiSauAsync(muc, thongBao, tinhVaoGioiHan: maLoi != "HET_LUOT");
        ketQua.MaLoi = maLoi;
        return ketQua;
    }

    private async Task<KetQuaGui> ThuLaiSauAsync(AnhChoGui muc, string thongBao, bool tinhVaoGioiHan)
    {
        var soLanThu = muc.SoLanThu + 1;

        if (tinhVaoGioiHan && soLanThu >= SoLanThuToiDa)
        {
            await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.XungDot, thongBao, tangSoLanThu: true);
            return new KetQuaGui
            {
                Loai = LoaiKetQuaGui.XungDot,
                ThongBao = $"Đã thử gửi {soLanThu} lần không được: {thongBao}"
            };
        }

        await _hangDoi.CapNhatTrangThaiAsync(muc.Id, TrangThaiAnh.ChoGui, thongBao, tangSoLanThu: true);
        return new KetQuaGui { Loai = LoaiKetQuaGui.ThuLaiSau, ThongBao = thongBao };
    }

    private sealed class PhanHoiScan
    {
        public bool ThanhCong { get; set; }
        public string? MaContainer { get; set; }
        public string? Loi { get; set; }
        public string? HuongGiaiQuyet { get; set; }
        public string? MaLoi { get; set; }
        public string? CanhBao { get; set; }
    }
}