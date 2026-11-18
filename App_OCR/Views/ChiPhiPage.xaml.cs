using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace App_OCR;

public partial class ChiPhiPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = ApiCauHinh.DiaChiApi;
    private static readonly TimeSpan ThoiGianChoDocHoaDon = TimeSpan.FromSeconds(45);

    public ChiPhiPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TaiDanhSachAsync();
    }

    private async void RvChiPhi_Refreshing(object sender, EventArgs e)
    {
        await TaiDanhSachAsync();
        RvChiPhi.IsRefreshing = false;
    }

    private async Task TaiDanhSachAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/chiphi");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);
            if (phanHoi.IsSuccessStatusCode)
            {
                var ds = await phanHoi.Content.ReadFromJsonAsync<List<ChiPhiItem>>();
                CvChiPhi.ItemsSource = ds;
            }
        }
        catch (Exception)
        {
            // Bỏ qua lỗi tải danh sách, không chặn màn hình gửi chi phí mới
        }
    }

    private async void BtnQuetHoaDon_Clicked(object sender, EventArgs e)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            LblKetQua.Text = "Không có mạng, vui lòng nhập tay chi phí";
            return;
        }

        string lua = await DisplayActionSheetAsync(
            "Ảnh hóa đơn nhiên liệu",
            "Hủy",
            null,
            "Chụp ảnh",
            "Chọn từ thư viện");

        FileResult? anh = null;
        if (lua == "Chụp ảnh")
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                LblKetQua.Text = "Thiết bị không hỗ trợ Camera";
                return;
            }

            anh = await MediaPicker.Default.CapturePhotoAsync();
        }
        else if (lua == "Chọn từ thư viện")
        {
            anh = await MediaPicker.Default.PickPhotoAsync();
        }

        if (anh is null) return;

        await DocHoaDonAsync(anh);
    }

    private async Task DocHoaDonAsync(FileResult anh)
    {
        BtnQuetHoaDon.IsEnabled = false;
        BtnGuiChiPhi.IsEnabled = false;
        LblKetQua.Text = "Đang đọc hóa đơn...";

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var luongAnh = await anh.OpenReadAsync();
            using var noiDungAnh = new StreamContent(luongAnh);
            noiDungAnh.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(anh.ContentType) ? "image/jpeg" : anh.ContentType);

            using var noiDungGui = new MultipartFormDataContent();
            noiDungGui.Add(
                noiDungAnh,
                "anh",
                string.IsNullOrWhiteSpace(anh.FileName) ? "hoadon.jpg" : anh.FileName);

            using var huy = new CancellationTokenSource(ThoiGianChoDocHoaDon);
            using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/chiphi/doc-hoa-don")
            {
                Content = noiDungGui
            };
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var phanHoi = await _httpClient.SendAsync(yeuCau, huy.Token);

            if (phanHoi.StatusCode == HttpStatusCode.Unauthorized)
            {
                LblKetQua.Text = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại";
                return;
            }

            DocHoaDonPhanHoi? ketQua = null;
            try
            {
                ketQua = await phanHoi.Content.ReadFromJsonAsync<DocHoaDonPhanHoi>();
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
            }

            if (ketQua is null)
            {
                LblKetQua.Text = $"Lỗi máy chủ ({(int)phanHoi.StatusCode}), vui lòng nhập tay chi phí";
                return;
            }

            if (!ketQua.ThanhCong)
            {
                LblKetQua.Text = ketQua.HuongGiaiQuyet ?? ketQua.Loi ?? "Không đọc được hóa đơn, vui lòng nhập tay";
                return;
            }

            DienFormTuHoaDon(ketQua);
        }
        catch (OperationCanceledException)
        {
            LblKetQua.Text = "Hết thời gian chờ đọc hóa đơn, vui lòng thử lại hoặc nhập tay";
        }
        catch (Exception)
        {
            LblKetQua.Text = "Không kết nối được máy chủ, vui lòng nhập tay chi phí";
        }
        finally
        {
            BtnQuetHoaDon.IsEnabled = true;
            BtnGuiChiPhi.IsEnabled = true;
        }
    }

    private void DienFormTuHoaDon(DocHoaDonPhanHoi ketQua)
    {
        if (ketQua.SoLit.HasValue) EdSoLit.Text = DinhDangSo(ketQua.SoLit.Value);
        if (ketQua.DonGia.HasValue) EdGiaDau.Text = DinhDangSo(ketQua.DonGia.Value);
        if (ketQua.ThanhTien.HasValue) EdSoTien.Text = DinhDangSo(ketQua.ThanhTien.Value);

        if (string.IsNullOrWhiteSpace(EdGhiChu.Text))
        {
            var ghiChu = string.Join(
                " - ",
                new[] { ketQua.TenCuaHang, ketQua.LoaiNhienLieu }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (ghiChu.Length > 0) EdGhiChu.Text = ghiChu;
        }

        var canhBao = new List<string>();

        if (!ketQua.KhopSoLieu && !string.IsNullOrWhiteSpace(ketQua.CanhBao))
        {
            canhBao.Add(ketQua.CanhBao);
        }

        if (ketQua.TruongNghiNgo is { Count: > 0 })
        {
            canhBao.Add($"Cần kiểm tra kỹ: {string.Join(", ", ketQua.TruongNghiNgo.Select(TenTruongHienThi))}");
        }

        if (!ketQua.SoLit.HasValue || !ketQua.DonGia.HasValue || !ketQua.ThanhTien.HasValue)
        {
            canhBao.Add("Còn ô chưa đọc được, vui lòng nhập tay");
        }

        LblKetQua.Text = canhBao.Count == 0
            ? "Đã điền từ hóa đơn, vui lòng kiểm tra lại rồi bấm gửi"
            : string.Join("\n", canhBao);
    }

    private static string DinhDangSo(decimal giaTri)
    {
        return giaTri.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string TenTruongHienThi(string ten) => ten switch
    {
        "so_lit" => "số lít",
        "don_gia" => "đơn giá",
        "thanh_tien" => "thành tiền",
        "loai_nhien_lieu" => "loại nhiên liệu",
        "ngay_gio" => "ngày giờ",
        "ten_cua_hang" => "tên cửa hàng",
        _ => ten
    };

    private async void BtnGuiChiPhi_Clicked(object sender, EventArgs e)
    {
        if (!decimal.TryParse(EdSoTien.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var soTien)
            || !decimal.TryParse(EdGiaDau.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var giaDau)
            || !decimal.TryParse(EdSoLit.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var soLit)
            || soTien <= 0 || giaDau <= 0 || soLit <= 0)
        {
            LblKetQua.Text = "Vui lòng nhập đúng số tiền, giá dầu và số lít (lớn hơn 0)";
            return;
        }

        BtnGuiChiPhi.IsEnabled = false;
        LblKetQua.Text = "";

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            var noiDung = new
            {
                SoTien = soTien,
                GiaDau = giaDau,
                SoLit = soLit,
                GhiChu = EdGhiChu.Text
            };

            using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/chiphi")
            {
                Content = JsonContent.Create(noiDung)
            };
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                LblKetQua.Text = "Đã gửi, kế toán sẽ giải chi vào cuối tuần";
                EdSoTien.Text = string.Empty;
                EdGiaDau.Text = string.Empty;
                EdSoLit.Text = string.Empty;
                EdGhiChu.Text = string.Empty;
                await TaiDanhSachAsync();
            }
            else
            {
                LblKetQua.Text = "Không gửi được, vui lòng kiểm tra lại thông tin";
            }
        }
        catch (Exception ex)
        {
            LblKetQua.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnGuiChiPhi.IsEnabled = true;
        }
    }
}

public class ChiPhiItem
{
    public int Id { get; set; }
    public decimal SoTien { get; set; }
    public decimal GiaDau { get; set; }
    public decimal SoLit { get; set; }
    public string? GhiChu { get; set; }
    public DateTime ThoiGianGui { get; set; }
    public bool DaGiaiChi { get; set; }

    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);
    public string SoTienHienThi => $"{SoTien:N0} đ";
    public string ChiTietHienThi => $"{SoLit:N1} lít × {GiaDau:N0} đ/lít";
    public string ThoiGianHienThi => ThoiGianGui.ToLocalTime().ToString("HH:mm dd/MM/yyyy");
    public string TrangThaiHienThi => DaGiaiChi ? "Đã giải chi" : "Chờ giải chi";
    public Color MauTrangThai => DaGiaiChi ? Color.FromArgb("#2E7D32") : Color.FromArgb("#E65100");
}

public class DocHoaDonPhanHoi
{
    public bool ThanhCong { get; set; }
    public string? Loi { get; set; }
    public string? HuongGiaiQuyet { get; set; }
    public decimal? SoLit { get; set; }
    public decimal? DonGia { get; set; }
    public decimal? ThanhTien { get; set; }
    public string? LoaiNhienLieu { get; set; }
    public string? TenCuaHang { get; set; }
    public bool KhopSoLieu { get; set; }
    public string? CanhBao { get; set; }
    public List<string> TruongNghiNgo { get; set; } = new();
}