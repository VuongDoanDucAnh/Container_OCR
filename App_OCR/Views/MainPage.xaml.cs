using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Authentication;

namespace App_OCR;

public partial class MainPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = ApiCauHinh.DiaChiApi;
    private const string DiaChiQuayVe = "appocr://dang-nhap";

    public MainPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    private async void BtnLogin_Clicked(object sender, EventArgs e)
    {
        var username = EntryUsername.Text;
        var password = EntryPassword.Text;

        var loginData = new
        {
            Username = username,
            Password = password
        };

        BtnLogin.IsEnabled = false;
        LoadingLogin.IsVisible = true;
        LoadingLogin.IsRunning = true;
        LblResult.Text = "";

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{ApiBaseUrl}/api/auth/login", loginData);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

                if (result?.Token is not null)
                {
                    await SecureStorage.Default.SetAsync("jwt_token", result.Token);
                    await SecureStorage.Default.SetAsync("ma_tai_xe", result.MaTaiXe ?? string.Empty);
                    LblResult.TextColor = Colors.Green;
                    LblResult.Text = "Đăng nhập thành công";
                    await Navigation.PopModalAsync();
                }
                else
                {
                    LblResult.TextColor = Colors.Red;
                    LblResult.Text = "Đăng nhập thành công nhưng không nhận được token";
                }
            }
            else
            {
                LblResult.TextColor = Colors.Red;
                LblResult.Text = $"Đăng nhập thất bại: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblResult.TextColor = Colors.Red;
            LblResult.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            LoadingLogin.IsVisible = false;
            LoadingLogin.IsRunning = false;
        }
    }

    private async void BtnGoogle_Clicked(object sender, EventArgs e)
    {
        BtnGoogle.IsEnabled = false;
        BtnLogin.IsEnabled = false;
        LoadingLogin.IsVisible = true;
        LoadingLogin.IsRunning = true;
        LblResult.TextColor = Colors.Red;
        LblResult.Text = "";

        try
        {
            var xacMinh = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var thuThach = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xacMinh))).ToLowerInvariant();

            var ketQuaGoogle = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri($"{ApiBaseUrl}/api/auth/google/bat-dau?thuThach={thuThach}"),
                new Uri(DiaChiQuayVe));

            if (ketQuaGoogle.Properties.TryGetValue("loi", out var maLoi))
            {
                LblResult.Text = DichMaLoiGoogle(maLoi);
                return;
            }

            if (!ketQuaGoogle.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                LblResult.Text = "Không nhận được mã đăng nhập từ Google";
                return;
            }

            var phanHoi = await _httpClient.PostAsJsonAsync(
                $"{ApiBaseUrl}/api/auth/google/doi-ma",
                new { code, xacMinh });

            if (!phanHoi.IsSuccessStatusCode)
            {
                LblResult.Text = "Đăng nhập Google thất bại, vui lòng thử lại";
                return;
            }

            var ketQua = await phanHoi.Content.ReadFromJsonAsync<LoginResponse>();
            if (ketQua?.Token is null)
            {
                LblResult.Text = "Đăng nhập thành công nhưng không nhận được token";
                return;
            }

            await SecureStorage.Default.SetAsync("jwt_token", ketQua.Token);
            await SecureStorage.Default.SetAsync("ma_tai_xe", ketQua.MaTaiXe ?? string.Empty);
            await Navigation.PopModalAsync();
        }
        catch (TaskCanceledException)
        {
            LblResult.Text = "Bạn đã hủy đăng nhập Google";
        }
        catch (Exception ex)
        {
            LblResult.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnGoogle.IsEnabled = true;
            BtnLogin.IsEnabled = true;
            LoadingLogin.IsVisible = false;
            LoadingLogin.IsRunning = false;
        }
    }

    private static string DichMaLoiGoogle(string maLoi) => maLoi switch
    {
        "EMAIL_CHUA_DANG_KY" => "Email Google này chưa được quản lý gán cho tài xế nào",
        "EMAIL_CHUA_XAC_MINH" => "Email Google này chưa được xác minh",
        "GOOGLE_CHUA_CAU_HINH" => "Máy chủ chưa cấu hình đăng nhập Google",
        _ => "Đăng nhập Google thất bại, vui lòng thử lại"
    };

    private async void LblDangKy_Tapped(object sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new RegisterPage());
    }
}

public class LoginResponse
{
    public string? Token { get; set; }
    public string? MaTaiXe { get; set; }
    public string? HoTen { get; set; }
}