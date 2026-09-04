using System.Net.Http.Json;

namespace App_OCR;

public partial class MainPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "http://10.0.2.2:5232";

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
                    LblResult.TextColor = Colors.Green;
                    LblResult.Text = "Đăng nhập thành công";
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
    }
}

public class LoginResponse
{
    public string? Token { get; set; }
    public string? MaTaiXe { get; set; }
    public string? HoTen { get; set; }
}