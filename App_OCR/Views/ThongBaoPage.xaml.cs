using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace App_OCR;

public partial class ThongBaoPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = ApiCauHinh.DiaChiApi;

    private readonly ObservableCollection<TinNhanItem> _danhSachTin = new();
    private readonly (string Hien, string? Ma)[] _dsLoaiSuCo =
    {
        ("Không phải báo sự cố", null),
        ("Xe hỏng", "XE_HONG"),
        ("Kẹt xe", "KET_XE"),
        ("Sai thùng container", "SAI_THUNG"),
        ("Tai nạn", "TAI_NAN"),
        ("Sự cố khác", "KHAC")
    };

    private FileResult? _anhDinhKem;
    private IDispatcherTimer? _timer;
    private int _sauId = 0;
    private bool _dangTaiTin = false;

    public ThongBaoPage()
    {
        InitializeComponent();
        CvTin.ItemsSource = _danhSachTin;
        PkLoaiSuCo.ItemsSource = _dsLoaiSuCo.Select(x => x.Hien).ToList();
        PkLoaiSuCo.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _danhSachTin.Clear();
        _sauId = 0;

        await TaiTinMoiAsync();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick += async (s, e) => await TaiTinMoiAsync();
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        _timer = null;
    }

    private async Task TaiTinMoiAsync()
    {
        if (_dangTaiTin) return;
        _dangTaiTin = true;

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");

            using var yeuCau = new HttpRequestMessage(
                HttpMethod.Get, $"{ApiBaseUrl}/api/thongbao?sauId={_sauId}");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);
            if (!phanHoi.IsSuccessStatusCode) return;

            var tinMoi = await phanHoi.Content.ReadFromJsonAsync<List<TinNhanItem>>();
            if (tinMoi is null || tinMoi.Count == 0) return;

            foreach (var tin in tinMoi)
            {
                _danhSachTin.Add(tin);
                _sauId = tin.Id;
            }

            if (_danhSachTin.Count > 0)
            {
                CvTin.ScrollTo(_danhSachTin[^1], position: ScrollToPosition.End, animate: true);
            }
        }
        catch (Exception)
        {
            // Bỏ qua lỗi mạng tạm thời khi đang tự động làm mới, tránh làm phiền người dùng
        }
        finally
        {
            _dangTaiTin = false;
        }
    }

    private async void BtnDinhKemAnh_Clicked(object sender, EventArgs e)
    {
        string lua = await DisplayActionSheetAsync("Đính kèm ảnh bằng chứng", "Huỷ", null, "Chụp ảnh", "Chọn từ thư viện");

        FileResult? ketQua = lua switch
        {
            "Chụp ảnh" => MediaPicker.Default.IsCaptureSupported
                ? await MediaPicker.Default.CapturePhotoAsync()
                : null,
            "Chọn từ thư viện" => await MediaPicker.Default.PickPhotoAsync(),
            _ => null
        };

        if (ketQua is null) return;

        _anhDinhKem = ketQua;

        using var luongGoc = await ketQua.OpenReadAsync();
        var boNhoAnh = new MemoryStream();
        await luongGoc.CopyToAsync(boNhoAnh);
        boNhoAnh.Position = 0;

        ImgDinhKem.Source = ImageSource.FromStream(() => boNhoAnh);
        GridAnhDinhKem.IsVisible = true;
    }

    private void BtnBoAnh_Clicked(object sender, EventArgs e)
    {
        _anhDinhKem = null;
        ImgDinhKem.Source = null;
        GridAnhDinhKem.IsVisible = false;
    }

    private async void BtnGuiTin_Clicked(object sender, EventArgs e)
    {
        string noiDung = EdNoiDung.Text?.Trim() ?? string.Empty;
        if (noiDung.Length == 0 && _anhDinhKem is null)
        {
            await DisplayAlertAsync("Chưa có nội dung", "Vui lòng nhập nội dung hoặc đính kèm ảnh.", "OK");
            return;
        }

        BtnGuiTin.IsEnabled = false;

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");
            string? loaiSuCo = _dsLoaiSuCo[PkLoaiSuCo.SelectedIndex].Ma;

            HttpResponseMessage phanHoi;

            if (_anhDinhKem is not null)
            {
                using var noiDungGui = new MultipartFormDataContent
                {
                    { new StringContent(noiDung), "noiDung" }
                };
                if (loaiSuCo is not null)
                {
                    noiDungGui.Add(new StringContent(loaiSuCo), "loaiSuCo");
                }

                using var luongAnh = await _anhDinhKem.OpenReadAsync();
                using var noiDungAnh = new StreamContent(luongAnh);
                noiDungAnh.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                noiDungGui.Add(noiDungAnh, "anh", _anhDinhKem.FileName);

                using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/thongbao/voi-anh")
                {
                    Content = noiDungGui
                };
                yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                phanHoi = await _httpClient.SendAsync(yeuCau);
            }
            else
            {
                var noiDungJson = new { NoiDung = noiDung, LoaiSuCo = loaiSuCo };

                using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/thongbao")
                {
                    Content = JsonContent.Create(noiDungJson)
                };
                yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                phanHoi = await _httpClient.SendAsync(yeuCau);
            }

            if (phanHoi.IsSuccessStatusCode)
            {
                var ketQua = await phanHoi.Content.ReadFromJsonAsync<GuiTinResponse>();
                if (ketQua?.Tin is not null)
                {
                    _danhSachTin.Add(ketQua.Tin);
                    _sauId = ketQua.Tin.Id;
                    CvTin.ScrollTo(_danhSachTin[^1], position: ScrollToPosition.End, animate: true);
                }

                EdNoiDung.Text = string.Empty;
                PkLoaiSuCo.SelectedIndex = 0;
                _anhDinhKem = null;
                ImgDinhKem.Source = null;
                GridAnhDinhKem.IsVisible = false;
            }
            else
            {
                var loi = await phanHoi.Content.ReadFromJsonAsync<ThongBaoLoiResponse>();
                await DisplayAlertAsync("Không gửi được", loi?.HuongGiaiQuyet ?? loi?.Loi ?? "Vui lòng thử lại.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi kết nối", ex.Message, "OK");
        }
        finally
        {
            BtnGuiTin.IsEnabled = true;
        }
    }
}

public class TinNhanItem
{
    public int Id { get; set; }
    public string? NguoiGui { get; set; }
    public string? NoiDung { get; set; }
    public string? LoaiSuCo { get; set; }
    public DateTime ThoiGian { get; set; }
    public string? AnhBase64 { get; set; }
    public string? AnhLoai { get; set; }

    public bool IsMine => NguoiGui == "TaiXe";
    public bool CoSuCo => !string.IsNullOrWhiteSpace(LoaiSuCo);
    public bool CoAnh => !string.IsNullOrWhiteSpace(AnhBase64);
    public string GioHienThi => ThoiGian.ToLocalTime().ToString("HH:mm dd/MM");

    public ImageSource? AnhSource => CoAnh
        ? ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(AnhBase64!)))
        : null;

    public string NhanSuCo => LoaiSuCo switch
    {
        "XE_HONG" => "Báo sự cố: Xe hỏng",
        "KET_XE" => "Báo sự cố: Kẹt xe",
        "SAI_THUNG" => "Báo sự cố: Sai thùng container",
        "TAI_NAN" => "Báo sự cố: Tai nạn",
        "KHAC" => "Báo sự cố: Sự cố khác",
        _ => string.Empty
    };
}

public class GuiTinResponse
{
    public bool ThanhCong { get; set; }
    public TinNhanItem? Tin { get; set; }
}

public class ThongBaoLoiResponse
{
    public string? Loi { get; set; }
    public string? HuongGiaiQuyet { get; set; }
    public string? MaLoi { get; set; }
}