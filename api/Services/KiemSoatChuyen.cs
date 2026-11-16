using DoAnOlympics.Api.Models;

namespace DoAnOlympics.Api.Services;

public static class KiemSoatChuyen
{
    public const double DungSaiOdoDauMacDinh = 5;
    public const double NguongLechQuangDuongMacDinh = 30;

    public static CanhBaoChuyen? KiemTraOdoDau(
        int phanCongChuyenId,
        int driverId,
        int odoBatDau,
        int? odoCuoiChuyenTruoc,
        double dungSaiKm)
    {
        if (odoCuoiChuyenTruoc is null)
        {
            return null;
        }

        int lech = odoBatDau - odoCuoiChuyenTruoc.Value;
        if (Math.Abs(lech) <= dungSaiKm)
        {
            return null;
        }

        return new CanhBaoChuyen
        {
            PhanCongChuyenId = phanCongChuyenId,
            DriverId = driverId,
            Loai = LoaiCanhBao.OdoDauLech,
            MoTa = $"ODO bắt đầu ({odoBatDau:N0} km) khác ODO kết thúc chuyến trước ({odoCuoiChuyenTruoc.Value:N0} km), lệch {lech:+#,0;-#,0} km (dung sai {dungSaiKm:0.#} km)",
            GiaTriDo = odoBatDau,
            GiaTriMongDoi = odoCuoiChuyenTruoc.Value
        };
    }

    public static CanhBaoChuyen? KiemTraQuangDuong(
        int phanCongChuyenId,
        int driverId,
        int odoBatDau,
        int odoKetThuc,
        double kmMotChieu,
        double nguongPhanTram)
    {
        if (kmMotChieu <= 0)
        {
            return null;
        }

        double kyVong = kmMotChieu * 2;
        double thucTe = odoKetThuc - odoBatDau;
        double lechPhanTram = Math.Abs(thucTe - kyVong) / kyVong * 100;

        if (lechPhanTram <= nguongPhanTram)
        {
            return null;
        }

        return new CanhBaoChuyen
        {
            PhanCongChuyenId = phanCongChuyenId,
            DriverId = driverId,
            Loai = LoaiCanhBao.QuangDuongLech,
            MoTa = $"Quãng đường theo ODO {thucTe:N0} km, dự kiến khoảng {kyVong:N0} km (2 lần {kmMotChieu:0.#} km của tuyến), lệch {lechPhanTram:0.#}% (ngưỡng {nguongPhanTram:0.#}%)",
            GiaTriDo = thucTe,
            GiaTriMongDoi = kyVong
        };
    }
}