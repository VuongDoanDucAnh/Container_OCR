using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace App_OCR;

public partial class TrangChuPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = "http://10.0.2.2:5232";

    public TrangChuPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TaiDuLieuAsync(hienSpinnerToanTrang: true);
    }

    private async void RvTrangChu_Refreshing(object sender, EventArgs e)
    {
        await TaiDuLieuAsync(hienSpinnerToanTrang: false);
        RvTrangChu.IsRefreshing = false;
    }

    private async Task TaiDuLieuAsync(bool hienSpinnerToanTrang)
    {
        if (hienSpinnerToanTrang)
        {
            LoadingTrangChu.IsVisible = true;
            LoadingTrangChu.IsRunning = true;
        }

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/trangchu");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                var ketQua = await phanHoi.Content.ReadFromJsonAsync<TrangChuResponse>();

                LblLoiChao.Text = $"Xin chào, {ketQua?.HoTen}";
                LblNgay.Text = DateTime.Now.ToString("dddd, dd/MM/yyyy",
                    System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
                LblSoQuet.Text = (ketQua?.SoQuetHomNay ?? 0).ToString();
                LblTinChuaDoc.Text = (ketQua?.SoTinChuaDoc ?? 0).ToString();

                CvChuyen.ItemsSource = ketQua?.ChuyenHomNay ?? new List<ChuyenHomNayItem>();
            }
            else
            {
                await DisplayAlertAsync("Lỗi", $"Không tải được trang chủ: {phanHoi.StatusCode}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi kết nối", ex.Message, "OK");
        }
        finally
        {
            LoadingTrangChu.IsVisible = false;
            LoadingTrangChu.IsRunning = false;
        }
    }

    private async void BtnChiDuong_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button nut || nut.BindingContext is not ChuyenHomNayItem chuyen)
            return;

        try
        {
            var diemDen = Uri.EscapeDataString(chuyen.DiemDen ?? string.Empty);
            var diemDi = Uri.EscapeDataString(chuyen.DiemDi ?? string.Empty);
            var uri = new Uri($"https://www.google.com/maps/dir/?api=1&origin={diemDi}&destination={diemDen}");
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Không mở được bản đồ", "Vui lòng kiểm tra đã cài Google Maps trên máy.", "OK");
        }
    }
    private async void BtnChiPhi_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ChiPhiPage());
    }
    private async void BtnBatDau_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button nut || nut.BindingContext is not ChuyenHomNayItem chuyen)
            return;

        nut.IsEnabled = false;

        try
        {
            bool thanhCong = await GoiApiChuyenAsync(chuyen.PhanCongId, "bat-dau");

            if (thanhCong)
            {
                await TaiDuLieuAsync(hienSpinnerToanTrang: false);
            }
            else
            {
                await DisplayAlertAsync("Không thực hiện được",
                    "Chuyến có thể đã được cập nhật. Hãy kéo xuống để làm mới danh sách.", "OK");
            }
        }
        finally
        {
            nut.IsEnabled = true;
        }
    }

    private async void BtnHoanThanh_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button nut || nut.BindingContext is not ChuyenHomNayItem chuyen) return;

        await Navigation.PushAsync(new KyNhanPage(chuyen.PhanCongId, chuyen.MaDon ?? string.Empty, chuyen.KhachHang ?? string.Empty));
    }

    private async Task<bool> GoiApiChuyenAsync(int phanCongId, string hanhDong)
    {
        var token = await SecureStorage.Default.GetAsync("jwt_token");

        using var yeuCau = new HttpRequestMessage(
            HttpMethod.Post, $"{ApiBaseUrl}/api/trangchu/chuyen/{phanCongId}/{hanhDong}");
        yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var phanHoi = await _httpClient.SendAsync(yeuCau);
        return phanHoi.IsSuccessStatusCode;
    }
}

public class TrangChuResponse
{
    public string? MaTaiXe { get; set; }
    public string? HoTen { get; set; }
    public string? Ngay { get; set; }
    public int SoQuetHomNay { get; set; }
    public int SoTinChuaDoc { get; set; }
    public List<ChuyenHomNayItem> ChuyenHomNay { get; set; } = new();
}

public class ChuyenHomNayItem
{
    public int PhanCongId { get; set; }
    public string? MaDon { get; set; }
    public string? KhachHang { get; set; }
    public string? LoaiHang { get; set; }
    public string? GhiChu { get; set; }
    public string? TenTuyen { get; set; }
    public string? DiemDi { get; set; }
    public string? DiemDen { get; set; }
    public double KhoangCachKm { get; set; }
    public string? TrangThai { get; set; }
    public DateTime? ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }

    public bool HienNutBatDau => TrangThai == "ChuaNhan";
    public bool HienNutHoanThanh => TrangThai == "DangChay";
    public bool CoGhiChu => !string.IsNullOrWhiteSpace(GhiChu);

    public string NhanTrangThai => TrangThai switch
    {
        "ChuaNhan" => "Chờ nhận",
        "DangChay" => "Đang chạy",
        _ => "Hoàn thành"
    };

    public Color MauTrangThai => TrangThai switch
    {
        "ChuaNhan" => Color.FromArgb("#E65100"),
        "DangChay" => Color.FromArgb("#1565C0"),
        _ => Color.FromArgb("#2E7D32")
    };

}