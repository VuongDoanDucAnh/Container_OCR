using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.Graphics.Skia;

namespace App_OCR;

public class ChuKyDrawable : IDrawable
{
    public List<List<PointF>> NetVes { get; } = new();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = 3;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        foreach (var net in NetVes)
        {
            if (net.Count < 2) continue;
            for (int i = 0; i < net.Count - 1; i++)
            {
                canvas.DrawLine(net[i], net[i + 1]);
            }
        }
    }
}

public partial class KyNhanPage : ContentPage
{
    private readonly int _phanCongId;
    private readonly HttpClient _httpClient = new();
    private const string ApiBaseUrl = "http://10.0.2.2:5232";
    private readonly ChuKyDrawable _drawable = new();
    private List<PointF>? _netHienTai;
    private byte[]? _anhBienBanBytes;

    public KyNhanPage(int phanCongId, string maDon, string khachHang)
    {
        InitializeComponent();
        _phanCongId = phanCongId;
        LblMaDon.Text = $"Đơn hàng: {maDon}";
        LblKhachHang.Text = $"Khách hàng: {khachHang}";
        GvChuKy.Drawable = _drawable;
    }

    private void GvChuKy_StartInteraction(object sender, TouchEventArgs e)
    {
        _netHienTai = new List<PointF> { e.Touches[0] };
        _drawable.NetVes.Add(_netHienTai);
        GvChuKy.Invalidate();
    }

    private void GvChuKy_DragInteraction(object sender, TouchEventArgs e)
    {
        if (_netHienTai != null)
        {
            _netHienTai.Add(e.Touches[0]);
            GvChuKy.Invalidate();
        }
    }

    private void BtnXoaChuKy_Clicked(object sender, EventArgs e)
    {
        _drawable.NetVes.Clear();
        _netHienTai = null;
        GvChuKy.Invalidate();
    }

    private async void BtnChupBienBan_Clicked(object sender, EventArgs e)
    {
        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                using var stream = await photo.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                _anhBienBanBytes = ms.ToArray();

                ImgBienBan.Source = ImageSource.FromStream(() => new MemoryStream(_anhBienBanBytes));
                ImgBienBan.IsVisible = true;
                BtnChupBienBan.Text = "📷 Chụp lại ảnh biên bản";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể chụp ảnh: {ex.Message}", "OK");
        }
    }

    private async void BtnXacNhanHoanThanh_Clicked(object sender, EventArgs e)
    {
        string nguoiKy = TxtNguoiKyTen.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nguoiKy))
        {
            await DisplayAlert("Thiếu thông tin", "Vui lòng nhập họ tên người ký nhận.", "OK");
            return;
        }

        if (_drawable.NetVes.Count == 0)
        {
            bool tiepTuc = await DisplayAlert("Chưa có chữ ký", "Khách hàng chưa ký xác nhận. Bạn có chắc chắn muốn kết thúc chuyến?", "Tiếp tục", "Quay lại");
            if (!tiepTuc) return;
        }

        string? chuKyBase64 = null;
        if (_drawable.NetVes.Count > 0)
        {
            int rongAnh = (int)GvChuKy.Width;
            int caoAnh = (int)GvChuKy.Height;
            if (rongAnh > 0 && caoAnh > 0)
            {
                var boiCanh = new SkiaBitmapExportContext(rongAnh, caoAnh, 1f);
                var canvas = boiCanh.Canvas;
                canvas.FillColor = Colors.White;
                canvas.FillRectangle(0, 0, rongAnh, caoAnh);
                _drawable.Draw(canvas, new RectF(0, 0, rongAnh, caoAnh));

                using var boNhoAnh = new MemoryStream();
                boiCanh.WriteToStream(boNhoAnh);
                chuKyBase64 = Convert.ToBase64String(boNhoAnh.ToArray());
            }
        }

        BtnXacNhanHoanThanh.IsEnabled = false;
        LoadingPod.IsVisible = true;
        LoadingPod.IsRunning = true;

        try
        {
            var token = await SecureStorage.Default.GetAsync("jwt_token");
            using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/trangchu/chuyen/{_phanCongId}/hoan-thanh");
            yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                nguoiKyTen = nguoiKy,
                anhGiaoHangBase64 = _anhBienBanBytes != null ? Convert.ToBase64String(_anhBienBanBytes) : null,
                anhGiaoHangLoaiNoiDung = "image/jpeg",
                anhChuKyBase64 = chuKyBase64
            };

            yeuCau.Content = JsonContent.Create(body);
            var phanHoi = await _httpClient.SendAsync(yeuCau);

            if (phanHoi.IsSuccessStatusCode)
            {
                await DisplayAlert("Thành công", "Đã cập nhật hoàn thành chuyến kèm biên bản e-POD!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert("Thất bại", "Không thể cập nhật chuyến. Kéo làm mới lại danh sách.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi kết nối", ex.Message, "OK");
        }
        finally
        {
            LoadingPod.IsVisible = false;
            LoadingPod.IsRunning = false;
            BtnXacNhanHoanThanh.IsEnabled = true;
        }
    }
}