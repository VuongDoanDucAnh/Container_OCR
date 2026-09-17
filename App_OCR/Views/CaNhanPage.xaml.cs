using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace App_OCR;

public partial class CaNhanPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = "http://10.0.2.2:5232";

    public CaNhanPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TaiThongTinAsync();
    }

    private async Task TaiThongTinAsync()
    {
        LoadingCaNhan.IsVisible = true;
        LoadingCaNhan.IsRunning = true;

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/auth/me");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                var thongTin = await phanHoi.Content.ReadFromJsonAsync<ThongTinTaiXe>();
                LblMaTaiXe.Text = $"Mã tài xế: {thongTin?.MaTaiXe}";
                EntryHoTen.Text = thongTin?.HoTen;
                EntrySoDienThoai.Text = thongTin?.SoDienThoai;
            }
            else
            {
                LblKetQuaThongTin.TextColor = Colors.Red;
                LblKetQuaThongTin.Text = $"Không tải được thông tin: {phanHoi.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblKetQuaThongTin.TextColor = Colors.Red;
            LblKetQuaThongTin.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            LoadingCaNhan.IsVisible = false;
            LoadingCaNhan.IsRunning = false;
        }
    }

    private async void BtnLuuThongTin_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryHoTen.Text))
        {
            LblKetQuaThongTin.TextColor = Colors.Red;
            LblKetQuaThongTin.Text = "Họ tên không được để trống";
            return;
        }

        BtnLuuThongTin.IsEnabled = false;
        LblKetQuaThongTin.Text = "";

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            var capNhatData = new
            {
                HoTen = EntryHoTen.Text,
                SoDienThoai = EntrySoDienThoai.Text
            };

            using var yeuCau = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/api/auth/thong-tin")
            {
                Content = JsonContent.Create(capNhatData)
            };
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                LblKetQuaThongTin.TextColor = Colors.Green;
                LblKetQuaThongTin.Text = "Cập nhật thành công";
            }
            else
            {
                LblKetQuaThongTin.TextColor = Colors.Red;
                LblKetQuaThongTin.Text = $"Cập nhật thất bại: {phanHoi.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblKetQuaThongTin.TextColor = Colors.Red;
            LblKetQuaThongTin.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnLuuThongTin.IsEnabled = true;
        }
    }

    private async void BtnDoiMatKhau_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryMatKhauCu.Text) || string.IsNullOrWhiteSpace(EntryMatKhauMoi.Text))
        {
            LblKetQuaMatKhau.TextColor = Colors.Red;
            LblKetQuaMatKhau.Text = "Vui lòng nhập đầy đủ mật khẩu cũ và mới";
            return;
        }

        BtnDoiMatKhau.IsEnabled = false;
        LblKetQuaMatKhau.Text = "";

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            var doiMatKhauData = new
            {
                MatKhauCu = EntryMatKhauCu.Text,
                MatKhauMoi = EntryMatKhauMoi.Text
            };

            using var yeuCau = new HttpRequestMessage(HttpMethod.Put, $"{ApiBaseUrl}/api/auth/doi-mat-khau")
            {
                Content = JsonContent.Create(doiMatKhauData)
            };
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                LblKetQuaMatKhau.TextColor = Colors.Green;
                LblKetQuaMatKhau.Text = "Đổi mật khẩu thành công";
                EntryMatKhauCu.Text = "";
                EntryMatKhauMoi.Text = "";
            }
            else
            {
                var loiJson = await phanHoi.Content.ReadFromJsonAsync<LoiDoiMatKhau>();
                LblKetQuaMatKhau.TextColor = Colors.Red;
                LblKetQuaMatKhau.Text = loiJson?.Loi ?? $"Đổi mật khẩu thất bại: {phanHoi.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LblKetQuaMatKhau.TextColor = Colors.Red;
            LblKetQuaMatKhau.Text = $"Lỗi kết nối: {ex.Message}";
        }
        finally
        {
            BtnDoiMatKhau.IsEnabled = true;
        }
    }

    private async void BtnDangXuat_Clicked(object sender, EventArgs e)
    {
        bool xacNhan = await DisplayAlertAsync("Đăng xuất", "Bạn có chắc muốn đăng xuất không?", "Có", "Không");
        if (!xacNhan) return;

        SecureStorage.Default.Remove("jwt_token");
        await Shell.Current.GoToAsync("//trangchu");
        await Navigation.PushModalAsync(new MainPage());
    }
}

public class ThongTinTaiXe
{
    public string? MaTaiXe { get; set; }
    public string? HoTen { get; set; }
    public string? Username { get; set; }
    public string? SoDienThoai { get; set; }
}

public class LoiDoiMatKhau
{
    public string? Loi { get; set; }
}