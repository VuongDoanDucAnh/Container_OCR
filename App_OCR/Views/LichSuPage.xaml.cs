using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace App_OCR;

public partial class LichSuPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = "http://10.0.2.2:5232";

    public LichSuPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TaiDuLieuAsync(hienSpinnerToanTrang: true);
    }

    private async void RvLichSu_Refreshing(object sender, EventArgs e)
    {
        await TaiDuLieuAsync(hienSpinnerToanTrang: false);
        RvLichSu.IsRefreshing = false;
    }

    private async Task TaiDuLieuAsync(bool hienSpinnerToanTrang)
    {
        if (hienSpinnerToanTrang)
        {
            LoadingLichSu.IsVisible = true;
            LoadingLichSu.IsRunning = true;
        }

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/container/lich-su");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                var danhSach = await phanHoi.Content.ReadFromJsonAsync<List<LichSuItem>>();
                CvLichSu.ItemsSource = danhSach;
            }
            else
            {
                LblTrangThai.Text = $"Không tải được lịch sử: {phanHoi.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblTrangThai.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            LoadingLichSu.IsVisible = false;
            LoadingLichSu.IsRunning = false;
        }
    }
}

public class LichSuItem
{
    public string? MaContainer { get; set; }
    public bool ChecksumHopLe { get; set; }
    public DateTime? ThoiGianQuet { get; set; }
}