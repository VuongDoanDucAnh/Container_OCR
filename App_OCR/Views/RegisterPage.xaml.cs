using System.Net.Http.Json;

namespace App_OCR;

public partial class RegisterPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = "http://10.0.2.2:5232";

    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void BtnDangKy_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryMaTaiXe.Text) ||
            string.IsNullOrWhiteSpace(EntryHoTen.Text) ||
            string.IsNullOrWhiteSpace(EntryUsername.Text) ||
            string.IsNullOrWhiteSpace(EntryPassword.Text))
        {
            LblKetQua.Text = "Vui lòng điền đầy đủ thông tin";
            return;
        }

        BtnDangKy.IsEnabled = false;
        LoadingDangKy.IsVisible = true;
        LoadingDangKy.IsRunning = true;
        LblKetQua.Text = "";

        var dangKyData = new
        {
            MaTaiXe = EntryMaTaiXe.Text,
            HoTen = EntryHoTen.Text,
            Username = EntryUsername.Text,
            Password = EntryPassword.Text
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}/api/auth/register", dangKyData);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Thành công", "Đăng ký thành công, mời đăng nhập", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                var loi = await response.Content.ReadAsStringAsync();
                LblKetQua.TextColor = Colors.Red;
                LblKetQua.Text = $"Đăng ký thất bại: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblKetQua.TextColor = Colors.Red;
            LblKetQua.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnDangKy.IsEnabled = true;
            LoadingDangKy.IsVisible = false;
            LoadingDangKy.IsRunning = false;
        }
    }
}