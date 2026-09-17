using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace App_OCR;

public partial class ScanPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "http://10.0.2.2:5232";
    private FileResult? _anhDaChon;

    public ScanPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    private async void BtnChupAnh_Clicked(object sender, EventArgs e)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            LblKetQua.Text = "Thiết bị không hỗ trợ Camera";
            return;
        }

        var ketQua = await MediaPicker.Default.CapturePhotoAsync();
        await HienThiAnhVaLuu(ketQua);
    }

    private async void BtnChonThuVien_Clicked(object sender, EventArgs e)
    {
        var ketQua = await MediaPicker.Default.PickPhotoAsync();
        await HienThiAnhVaLuu(ketQua);
    }

    private async Task HienThiAnhVaLuu(FileResult? ketQua)
    {
        if (ketQua is null) return;

        _anhDaChon = ketQua;

        using var luongGoc = await ketQua.OpenReadAsync();
        var boNhoAnh = new MemoryStream();
        await luongGoc.CopyToAsync(boNhoAnh);
        boNhoAnh.Position = 0;

        ImgPreview.Source = ImageSource.FromStream(() => boNhoAnh);

        BtnGui.IsEnabled = true;
        BtnChupLai.IsVisible = true;
        LblKetQua.Text = "";
    }

    private void BtnChupLai_Clicked(object sender, EventArgs e)
    {
        _anhDaChon = null;
        ImgPreview.Source = null;
        BtnGui.IsEnabled = false;
        BtnChupLai.IsVisible = false;
        LblKetQua.Text = "";
    }

    private async void BtnGui_Clicked(object sender, EventArgs e)
    {
        if (_anhDaChon is null) return;

        BtnGui.IsEnabled = false;
        BtnChupAnh.IsEnabled = false;
        BtnChonThuVien.IsEnabled = false;
        BtnChupLai.IsEnabled = false;
        LoadingScan.IsVisible = true;
        LoadingScan.IsRunning = true;
        LblKetQua.Text = "Đang gửi...";

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var noiDungGui = new MultipartFormDataContent();
            using var luongAnh = await _anhDaChon.OpenReadAsync();
            using var noiDungAnh = new StreamContent(luongAnh);
            noiDungAnh.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            noiDungGui.Add(noiDungAnh, "anh", _anhDaChon.FileName);

            using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/container/scan")
            {
                Content = noiDungGui
            };
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);
            var ketQuaJson = await phanHoi.Content.ReadFromJsonAsync<ScanResponse>();

            if (phanHoi.IsSuccessStatusCode)
            {
                LblKetQua.TextColor = Colors.Green;
                LblKetQua.Text = $"Thành công: {ketQuaJson?.MaContainer}";

                _anhDaChon = null;
                ImgPreview.Source = null;
                BtnChupLai.IsVisible = false;

                await Task.Delay(1000);
                await Shell.Current.GoToAsync("//lichsu");
            }
            else
            {
                LblKetQua.TextColor = Colors.Red;
                LblKetQua.Text = ketQuaJson?.HuongGiaiQuyet ?? $"Lỗi: {phanHoi.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblKetQua.TextColor = Colors.Red;
            LblKetQua.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnChupAnh.IsEnabled = true;
            BtnChonThuVien.IsEnabled = true;
            BtnChupLai.IsEnabled = true;
            BtnGui.IsEnabled = _anhDaChon is not null;
            LoadingScan.IsVisible = false;
            LoadingScan.IsRunning = false;
        }
    }
}

public class ScanResponse
{
    public bool ThanhCong { get; set; }
    public string? MaContainer { get; set; }
    public bool ChecksumHopLe { get; set; }
    public string? Loi { get; set; }
    public string? HuongGiaiQuyet { get; set; }
    public string? MaLoi { get; set; }
}