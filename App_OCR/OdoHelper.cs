using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace App_OCR;

public class KetQuaOdo
{
    public int Odo { get; set; }
    public string? AnhPublicId { get; set; }
}

public static class OdoHelper
{
    private const string ApiBaseUrl = ApiCauHinh.DiaChiApi;
    private static readonly TimeSpan ThoiGianCho = TimeSpan.FromSeconds(45);
    private static readonly HttpClient Http = new();

    public static async Task<KetQuaOdo?> ChupVaXacNhanAsync(Page trang, string tieuDe)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await trang.DisplayAlertAsync("Không có mạng", "Cần kết nối mạng để ghi nhận số ODO.", "OK");
            return null;
        }

        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await trang.DisplayAlertAsync("Không hỗ trợ", "Thiết bị không hỗ trợ Camera.", "OK");
            return null;
        }

        FileResult? anh;
        try
        {
            anh = await MediaPicker.Default.CapturePhotoAsync();
        }
        catch (Exception ex)
        {
            await trang.DisplayAlertAsync("Lỗi", $"Không thể chụp ảnh: {ex.Message}", "OK");
            return null;
        }

        if (anh is null) return null;

        DocOdoPhanHoi? ketQua;
        try
        {
            ketQua = await GoiDocOdoAsync(anh);
        }
        catch (OperationCanceledException)
        {
            await trang.DisplayAlertAsync("Hết thời gian chờ", "Máy chủ phản hồi quá lâu, vui lòng thử lại.", "OK");
            return null;
        }
        catch (Exception)
        {
            await trang.DisplayAlertAsync("Lỗi kết nối", "Không kết nối được máy chủ, vui lòng thử lại.", "OK");
            return null;
        }

        if (ketQua is null)
        {
            await trang.DisplayAlertAsync("Lỗi máy chủ", "Không đọc được phản hồi từ máy chủ, vui lòng thử lại.", "OK");
            return null;
        }

        if (ketQua.MaLoi == "HET_PHIEN")
        {
            await trang.DisplayAlertAsync("Hết phiên", ketQua.Loi ?? "Vui lòng đăng nhập lại.", "OK");
            return null;
        }

        bool choNhapTay = ketQua.MaLoi == "LOI_DICH_VU_AI";

        if (!ketQua.ThanhCong && !choNhapTay)
        {
            var noiDung = string.IsNullOrWhiteSpace(ketQua.HuongGiaiQuyet)
                ? ketQua.Loi ?? "Không đọc được ảnh đồng hồ."
                : $"{ketQua.Loi}. {ketQua.HuongGiaiQuyet}";
            await trang.DisplayAlertAsync("Chưa dùng được ảnh này", noiDung, "OK");
            return null;
        }

        var thongDiep = ketQua.ThanhCong
            ? ketQua.CanhBao ?? "Kiểm tra lại số ODO trên đồng hồ rồi xác nhận"
            : "Hệ thống đọc đồng hồ đang gián đoạn. Hãy nhập tay số ODO đang hiển thị";

        while (true)
        {
            var nhap = await trang.DisplayPromptAsync(
                tieuDe,
                thongDiep,
                "Xác nhận",
                "Hủy",
                "Số ODO (km)",
                7,
                Keyboard.Numeric,
                ketQua.Odo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            if (nhap is null) return null;

            if (int.TryParse(nhap.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var odo))
            {
                return new KetQuaOdo { Odo = odo, AnhPublicId = ketQua.AnhPublicId };
            }

            thongDiep = "Số ODO không hợp lệ, vui lòng chỉ nhập chữ số";
        }
    }

    public static async Task<string?> DocThongBaoLoiAsync(HttpResponseMessage phanHoi)
    {
        try
        {
            var loi = await phanHoi.Content.ReadFromJsonAsync<LoiMayChu>();
            if (loi is null || string.IsNullOrWhiteSpace(loi.Loi)) return null;

            return string.IsNullOrWhiteSpace(loi.HuongGiaiQuyet)
                ? loi.Loi
                : $"{loi.Loi}. {loi.HuongGiaiQuyet}";
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static async Task<DocOdoPhanHoi?> GoiDocOdoAsync(FileResult anh)
    {
        var token = await SecureStorage.Default.GetAsync("jwt_token");

        using var luongAnh = await anh.OpenReadAsync();
        using var noiDungAnh = new StreamContent(luongAnh);
        noiDungAnh.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(anh.ContentType) ? "image/jpeg" : anh.ContentType);

        using var noiDungGui = new MultipartFormDataContent();
        noiDungGui.Add(
            noiDungAnh,
            "anh",
            string.IsNullOrWhiteSpace(anh.FileName) ? "odo.jpg" : anh.FileName);

        using var huy = new CancellationTokenSource(ThoiGianCho);
        using var yeuCau = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/trangchu/doc-odo")
        {
            Content = noiDungGui
        };
        yeuCau.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var phanHoi = await Http.SendAsync(yeuCau, huy.Token);

        if (phanHoi.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new DocOdoPhanHoi
            {
                Loi = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại",
                MaLoi = "HET_PHIEN"
            };
        }

        try
        {
            return await phanHoi.Content.ReadFromJsonAsync<DocOdoPhanHoi>();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed class DocOdoPhanHoi
    {
        public bool ThanhCong { get; set; }
        public int? Odo { get; set; }
        public string? AnhPublicId { get; set; }
        public string? CanhBao { get; set; }
        public string? Loi { get; set; }
        public string? HuongGiaiQuyet { get; set; }
        public string? MaLoi { get; set; }
    }

    private sealed class LoiMayChu
    {
        public string? Loi { get; set; }
        public string? HuongGiaiQuyet { get; set; }
    }
}