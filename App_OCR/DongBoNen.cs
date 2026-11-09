namespace App_OCR;

public static class DongBoNen
{
    private const string ApiBaseUrl = ApiCauHinh.DiaChiApi;
    private static readonly HttpClient Http = new();
    private static readonly Lazy<DongBoAnh> DongBo = new(() => new DongBoAnh(new HangDoiAnh(), Http, ApiBaseUrl));
    private static bool _daBat;

    public static void Bat()
    {
        if (_daBat) return;

        _daBat = true;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public static void ThuDongBoNgay()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

        _ = ChayAsync();
    }

    private static void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _ = ChayAsync();
        }
    }

    private static async Task ChayAsync()
    {
        try
        {
            var maTaiXe = await SecureStorage.Default.GetAsync("ma_tai_xe");
            if (string.IsNullOrEmpty(maTaiXe)) return;

            await DongBo.Value.DongBoTatCaAsync(maTaiXe, tuDong: true);
        }
        catch (Exception)
        {
        }
    }
}