using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace DoAnOlympics.Api.Services;

public class DongDonHangExcel
{
    public int SoDong { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public DateTime? NgayChay { get; set; }
    public string TenKhachHang { get; set; } = string.Empty;
    public string TenTuyen { get; set; } = string.Empty;
    public string? DiemDi { get; set; }
    public string? DiemDen { get; set; }
    public double? KhoangCachKm { get; set; }
    public string LoaiHang { get; set; } = string.Empty;
    public List<string> Loi { get; set; } = new();
}

public class KetQuaDocExcel
{
    public List<DongDonHangExcel> Dong { get; set; } = new();
    public string? LoiChung { get; set; }
}

public static class DocFileDonHangExcel
{
    public const long KichThuocToiDa = 5 * 1024 * 1024;
    public const int SoDongToiDa = 500;
    private const int SoDongTimTieuDe = 15;
    private const int DoDaiMaDonToiDa = 30;

    private static readonly string[] DinhDangNgay =
    {
        "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd"
    };

    private static readonly Regex MauThongTinTuyen = new(
        @"\(\s*(?<di>[^()]+?)\s*(?:➔|→|->|=>)\s*(?<den>[^()]+?)\s*-\s*(?<km>\d+(?:[.,]\d+)*)\s*km\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ChuanHoa(string? chuoi)
    {
        if (string.IsNullOrWhiteSpace(chuoi)) return string.Empty;

        string phanRa = chuoi.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(phanRa.Length);
        foreach (char kyTu in phanRa)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(kyTu) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(kyTu == 'đ' || kyTu == 'Đ' ? 'd' : kyTu);
        }

        string ketQua = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return Regex.Replace(ketQua, @"\s+", " ").Trim();
    }

    public static KetQuaDocExcel Doc(Stream stream)
    {
        var kq = new KetQuaDocExcel();

        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            kq.LoiChung = "Không đọc được file. Vui lòng dùng file .xlsx hợp lệ.";
            return kq;
        }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws is null)
            {
                kq.LoiChung = "File không có sheet nào.";
                return kq;
            }

            int dongCuoi = ws.LastRowUsed()?.RowNumber() ?? 0;
            int cotCuoi = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

            Dictionary<string, int>? cot = null;
            int dongTieuDe = 0;
            for (int r = 1; r <= Math.Min(SoDongTimTieuDe, dongCuoi); r++)
            {
                cot = TimCot(ws, r, cotCuoi);
                if (cot is not null)
                {
                    dongTieuDe = r;
                    break;
                }
            }

            if (cot is null)
            {
                kq.LoiChung = "Không tìm thấy dòng tiêu đề. File cần có các cột: Mã Đơn, Ngày Đặt, Khách Hàng, Tuyến Vận Chuyển, Loại Hàng.";
                return kq;
            }

            var maDaGap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int r = dongTieuDe + 1; r <= dongCuoi; r++)
            {
                string ma = Chuoi(ws.Cell(r, cot["ma"]));
                var oNgay = ws.Cell(r, cot["ngay"]);
                string khach = Chuoi(ws.Cell(r, cot["khach"]));
                string tuyenTho = Chuoi(ws.Cell(r, cot["tuyen"]));
                string loai = Chuoi(ws.Cell(r, cot["loai"]));

                if (ma.Length == 0 && khach.Length == 0 && tuyenTho.Length == 0 && loai.Length == 0 && oNgay.IsEmpty())
                {
                    continue;
                }

                if (kq.Dong.Count >= SoDongToiDa)
                {
                    kq.Dong.Clear();
                    kq.LoiChung = $"File có quá {SoDongToiDa} dòng dữ liệu. Vui lòng chia nhỏ file.";
                    return kq;
                }

                var dong = new DongDonHangExcel
                {
                    SoDong = r,
                    MaDon = ma,
                    NgayChay = DocNgay(oNgay),
                    TenKhachHang = Gon(khach),
                    LoaiHang = Gon(loai)
                };
                TachTuyen(tuyenTho, dong);

                if (ma.Length == 0)
                {
                    dong.Loi.Add("Thiếu mã đơn.");
                }
                else if (ma.Length > DoDaiMaDonToiDa)
                {
                    dong.Loi.Add($"Mã đơn dài quá {DoDaiMaDonToiDa} ký tự.");
                }
                else if (maDaGap.TryGetValue(ma, out int dongTruoc))
                {
                    dong.Loi.Add($"Trùng mã đơn với dòng {dongTruoc} trong file.");
                }
                else
                {
                    maDaGap[ma] = r;
                }

                if (dong.NgayChay is null) dong.Loi.Add("Ngày không hợp lệ (cần dạng yyyy-MM-dd hoặc dd/MM/yyyy).");
                if (dong.TenKhachHang.Length == 0) dong.Loi.Add("Thiếu tên khách hàng.");
                if (dong.TenTuyen.Length == 0) dong.Loi.Add("Thiếu tuyến vận chuyển.");
                if (dong.LoaiHang.Length == 0) dong.Loi.Add("Thiếu loại hàng.");

                kq.Dong.Add(dong);
            }
        }

        return kq;
    }

    private static Dictionary<string, int>? TimCot(IXLWorksheet ws, int dong, int cotCuoi)
    {
        var cot = new Dictionary<string, int>();
        for (int c = 1; c <= cotCuoi; c++)
        {
            string chu = ChuanHoa(ws.Cell(dong, c).GetString());
            if (chu.Length == 0) continue;

            if (chu.Contains("ma don", StringComparison.Ordinal)) cot.TryAdd("ma", c);
            else if (chu.StartsWith("ngay", StringComparison.Ordinal)) cot.TryAdd("ngay", c);
            else if (chu.Contains("khach", StringComparison.Ordinal)) cot.TryAdd("khach", c);
            else if (chu.Contains("loai hang", StringComparison.Ordinal)) cot.TryAdd("loai", c);
            else if (chu.Contains("tuyen", StringComparison.Ordinal)) cot.TryAdd("tuyen", c);
        }

        return cot.Count == 5 ? cot : null;
    }

    private static string Chuoi(IXLCell o)
    {
        return o.GetString().Replace('\u00A0', ' ').Trim();
    }

    private static string Gon(string chuoi)
    {
        return Regex.Replace(chuoi, @"\s+", " ").Trim();
    }

    private static DateTime? DocNgay(IXLCell o)
    {
        if (o.IsEmpty()) return null;

        if (o.DataType == XLDataType.DateTime && o.TryGetValue(out DateTime ngay))
        {
            return ngay.Date;
        }

        if (o.DataType == XLDataType.Number && o.TryGetValue(out double so) && so >= 36526 && so <= 73050)
        {
            return DateTime.FromOADate(so).Date;
        }

        string chuoi = o.GetString().Trim();
        if (DateTime.TryParseExact(chuoi, DinhDangNgay, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayChuoi))
        {
            return ngayChuoi.Date;
        }

        return null;
    }

    private static void TachTuyen(string tho, DongDonHangExcel dong)
    {
        string chuan = tho.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var khop = MauThongTinTuyen.Match(chuan);

        string phanTen;
        if (khop.Success)
        {
            phanTen = chuan[..khop.Index];
            dong.DiemDi = Gon(khop.Groups["di"].Value);
            dong.DiemDen = Gon(khop.Groups["den"].Value);
            dong.KhoangCachKm = DocSoKm(khop.Groups["km"].Value);
        }
        else
        {
            phanTen = chuan.Split('\n')[0];
        }

        dong.TenTuyen = Gon(phanTen);
    }

    private static double? DocSoKm(string chuoi)
    {
        if (Regex.IsMatch(chuoi, @"^\d{1,3}(\.\d{3})+$"))
        {
            chuoi = chuoi.Replace(".", string.Empty);
        }

        chuoi = chuoi.Replace(',', '.');
        if (double.TryParse(chuoi, NumberStyles.Float, CultureInfo.InvariantCulture, out double km) && km > 0)
        {
            return km;
        }

        return null;
    }
}