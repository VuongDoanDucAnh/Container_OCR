using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace App_OCR;

public partial class ScanPage : ContentPage
{
    private const string ApiBaseUrl = ApiCauHinh.DiaChiApi;
    private readonly HttpClient _httpClient = new();
    private readonly HangDoiAnh _hangDoi = new();
    private readonly DongBoAnh _dongBo;
    private readonly List<LoaiHinhChuyenItem> _dsLoaiHinh = new();

    private FileResult? _anhDaChon;
    private byte[]? _duLieuAnh;
    private string _maTaiXeHienTai = string.Empty;

    public ScanPage()
    {
        InitializeComponent();
        _dongBo = new DongBoAnh(_hangDoi, _httpClient, ApiBaseUrl);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        await LayMaTaiXeHienTaiAsync();
        await TaiLoaiHinhChuyenAsync();
        await CapNhatSoChoGuiAsync();

        if (CoKetNoiMang() && !string.IsNullOrEmpty(_maTaiXeHienTai))
        {
            _ = _dongBo.DongBoTatCaAsync(_maTaiXeHienTai, tuDong: true);
            await CapNhatSoChoGuiAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet && !string.IsNullOrEmpty(_maTaiXeHienTai))
        {
            await _dongBo.DongBoTatCaAsync(_maTaiXeHienTai, tuDong: true);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await CapNhatSoChoGuiAsync();
            });
        }
    }

    private async Task LayMaTaiXeHienTaiAsync()
    {
        _maTaiXeHienTai = await SecureStorage.Default.GetAsync("ma_tai_xe") ?? string.Empty;
    }

    private async Task CapNhatSoChoGuiAsync()
    {
        if (string.IsNullOrEmpty(_maTaiXeHienTai)) return;

        var soChoGui = await _hangDoi.DemChoGuiAsync(_maTaiXeHienTai);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (soChoGui > 0)
            {
                LblSoChoGui.Text = $"Chờ gửi: {soChoGui}";
                BrdChoGui.IsVisible = true;
            }
            else
            {
                BrdChoGui.IsVisible = false;
            }
        });
    }

    private static bool CoKetNoiMang()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private async Task TaiLoaiHinhChuyenAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");
            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/api/loaihinhchuyen");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var phanHoi = await _httpClient.SendAsync(yeuCau);
            if (!phanHoi.IsSuccessStatusCode) return;

            var ds = await phanHoi.Content.ReadFromJsonAsync<List<LoaiHinhChuyenItem>>();
            if (ds is null) return;

            _dsLoaiHinh.Clear();
            _dsLoaiHinh.AddRange(ds);
            PkLoaiHinh.ItemsSource = _dsLoaiHinh.Select(x => x.Ten).ToList();
        }
        catch (Exception)
        {
        }
    }

    private async void BtnChupAnh_Clicked(object sender, EventArgs e)
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            LblKetQua.Text = "Thiết bị không hỗ trợ Camera";
            return;
        }

        var ketQua = await MediaPicker.Default.CapturePhotoAsync();
        await XuLyChonAnh(ketQua);
    }

    private async void BtnChonThuVien_Clicked(object sender, EventArgs e)
    {
        var ketQua = await MediaPicker.Default.PickPhotoAsync();
        await XuLyChonAnh(ketQua);
    }

    private async Task XuLyChonAnh(FileResult? ketQua)
    {
        if (ketQua is null) return;
        _anhDaChon = ketQua;

        using var luongGoc = await ketQua.OpenReadAsync();
        using var boNho = new MemoryStream();
        await luongGoc.CopyToAsync(boNho);
        _duLieuAnh = boNho.ToArray();

        ImgPreview.Source = ImageSource.FromStream(() => new MemoryStream(_duLieuAnh));
        VslChoAnh.IsVisible = false;
        BtnChupLai.IsVisible = true;
        LblKetQua.Text = "";

        await LuuVaoHangDoiVaGuiNgayAsync();
    }

    private async Task<(double? ViDo, double? KinhDo)> LayToaDoHienTaiAsync()
    {
        try
        {
            var trangThaiQuyen = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (trangThaiQuyen != PermissionStatus.Granted)
            {
                trangThaiQuyen = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (trangThaiQuyen != PermissionStatus.Granted)
            {
                return (null, null);
            }

            var yeuCauViTri = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var viTri = await Geolocation.Default.GetLocationAsync(yeuCauViTri, cts.Token);

            if (viTri is null)
            {
                viTri = await Geolocation.Default.GetLastKnownLocationAsync();
            }

            if (viTri is not null)
            {
                return (viTri.Latitude, viTri.Longitude);
            }
        }
        catch (Exception)
        {
        }

        return (null, null);
    }

    private async Task LuuVaoHangDoiVaGuiNgayAsync()
    {
        if (_duLieuAnh is null || _duLieuAnh.Length == 0) return;

        if (string.IsNullOrEmpty(_maTaiXeHienTai))
        {
            await LayMaTaiXeHienTaiAsync();
            if (string.IsNullOrEmpty(_maTaiXeHienTai))
            {
                LblKetQua.Text = "Chưa xác định được tài xế, vui lòng đăng nhập lại";
                return;
            }
        }

        int? loaiHinhChuyenId = null;
        if (PkLoaiHinh.SelectedIndex >= 0 && PkLoaiHinh.SelectedIndex < _dsLoaiHinh.Count)
        {
            loaiHinhChuyenId = _dsLoaiHinh[PkLoaiHinh.SelectedIndex].Id;
        }

        string? diaDiem = string.IsNullOrWhiteSpace(EdDiaDiem.Text) ? null : EdDiaDiem.Text.Trim();

        DatTrangThaiGui(dangGui: true);
        LblKetQua.Text = "Đang lấy tọa độ và lưu...";

        var (viDo, kinhDo) = await LayToaDoHienTaiAsync();

        AnhChoGui muc;
        using (var luongLuu = new MemoryStream(_duLieuAnh))
        {
            muc = await _hangDoi.ThemAsync(
                luongLuu,
                _maTaiXeHienTai,
                DateTime.UtcNow,
                viDo,
                kinhDo,
                loaiHinhChuyenId,
                diaDiem);
        }

        await CapNhatSoChoGuiAsync();

        if (!CoKetNoiMang())
        {
            DatTrangThaiGui(dangGui: false);
            LblKetQua.Text = "Không có mạng: Ảnh đã lưu vào máy, sẽ tự gửi khi có kết nối";
            return;
        }

        LblKetQua.Text = "Đang gửi lên máy chủ...";
        var ketQuaGui = await _dongBo.GuiMotAnhAsync(muc);
        await CapNhatSoChoGuiAsync();

        DatTrangThaiGui(dangGui: false);

        if (ketQuaGui.Loai == LoaiKetQuaGui.ThanhCong)
        {
            LblKetQua.Text = $"Thành công: {ketQuaGui.MaContainer}";
            DatLaiForm();
            await Task.Delay(1000);
            await Shell.Current.GoToAsync("//lichsu");
        }
        else if (ketQuaGui.Loai == LoaiKetQuaGui.HetPhien)
        {
            LblKetQua.Text = ketQuaGui.ThongBao ?? "Phiên đã hết hạn, vui lòng đăng nhập lại";
        }
        else if (ketQuaGui.Loai is LoaiKetQuaGui.CanChupLai or LoaiKetQuaGui.XungDot)
        {
            LblKetQua.Text = ketQuaGui.ThongBao ?? "Lỗi xử lý container";
        }
        else
        {
            LblKetQua.Text = "Lỗi gửi: Ảnh đã lưu trong hàng đợi, sẽ thử gửi lại";
        }
    }

    private async void BtnDongBoNgay_Clicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_maTaiXeHienTai))
        {
            await LayMaTaiXeHienTaiAsync();
            if (string.IsNullOrEmpty(_maTaiXeHienTai))
            {
                await DisplayAlertAsync("Chưa xác thực", "Vui lòng đăng nhập lại để thực hiện đồng bộ.", "OK");
                return;
            }
        }

        if (!CoKetNoiMang())
        {
            await DisplayAlertAsync("Mất kết nối", "Hiện không có mạng Internet. Hãy kiểm tra kết nối mạng rồi thử lại.", "OK");
            return;
        }

        BtnDongBoNgay.IsEnabled = false;
        LoadingScan.IsVisible = true;
        LoadingScan.IsRunning = true;
        LblKetQua.Text = "Đang đồng bộ hàng đợi...";

        try
        {
            var tongKet = await _dongBo.DongBoTatCaAsync(_maTaiXeHienTai, tuDong: false);
            await CapNhatSoChoGuiAsync();

            if (tongKet.BoQua)
            {
                LblKetQua.Text = "Đang có đợt gửi khác chạy, vui lòng chờ";
                return;
            }

            if (tongKet.HetPhien)
            {
                LblKetQua.Text = "Phiên đã hết hạn, vui lòng đăng nhập lại";
                return;
            }

            LblKetQua.Text = $"Đã gửi: {tongKet.DaGui} | Cần chụp lại: {tongKet.CanXuLy} | Còn chờ: {tongKet.ConCho}";

            var dsLoi = await _hangDoi.LayAnhLoiAsync(_maTaiXeHienTai);
            if (dsLoi.Count > 0)
            {
                bool muonXoa = await DisplayAlertAsync(
                    "Ảnh cần xử lý",
                    $"Có {dsLoi.Count} ảnh bị lỗi không thể gửi (mờ hoặc trùng). Bạn có muốn xoá bỏ các ảnh lỗi này?",
                    "Xoá ảnh lỗi",
                    "Giữ lại");

                if (muonXoa)
                {
                    await _hangDoi.XoaAnhLoiAsync(_maTaiXeHienTai);
                    await CapNhatSoChoGuiAsync();
                }
            }
        }
        finally
        {
            BtnDongBoNgay.IsEnabled = true;
            LoadingScan.IsVisible = false;
            LoadingScan.IsRunning = false;
        }
    }

    private void BtnChupLai_Clicked(object sender, EventArgs e)
    {
        DatLaiForm();
    }

    private void DatLaiForm()
    {
        _anhDaChon = null;
        _duLieuAnh = null;
        ImgPreview.Source = null;
        VslChoAnh.IsVisible = true;
        BtnChupLai.IsVisible = false;
        LblKetQua.Text = "";
        PkLoaiHinh.SelectedIndex = -1;
        EdDiaDiem.Text = string.Empty;
    }

    private void DatTrangThaiGui(bool dangGui)
    {
        BtnChupAnh.IsEnabled = !dangGui;
        BtnChonThuVien.IsEnabled = !dangGui;
        BtnChupLai.IsEnabled = !dangGui;
        BtnDongBoNgay.IsEnabled = !dangGui;
        LoadingScan.IsVisible = dangGui;
        LoadingScan.IsRunning = dangGui;
    }
}

public class LoaiHinhChuyenItem
{
    public int Id { get; set; }
    public string? Ten { get; set; }
}