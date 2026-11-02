using System.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace DoAnOlympics.Api.Services;

public interface IAnhLuuTruService
{
    Task<string?> TaiLenAsync(byte[] anh, string thuMuc);
    string? TaoUrl(string? publicId, int chieuRongToiDa = 900);
}

public class CloudinaryAnhService : IAnhLuuTruService
{
    private readonly Cloudinary? _cloudinary;
    private readonly string _cloudName;
    private readonly ILogger<CloudinaryAnhService> _logger;

    public CloudinaryAnhService(IConfiguration config, ILogger<CloudinaryAnhService> logger)
    {
        _logger = logger;
        _cloudName = config["Cloudinary:CloudName"] ?? string.Empty;
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];

        if (!string.IsNullOrWhiteSpace(_cloudName)
            && !string.IsNullOrWhiteSpace(apiKey)
            && !string.IsNullOrWhiteSpace(apiSecret))
        {
            _cloudinary = new Cloudinary(new Account(_cloudName, apiKey, apiSecret));
            _cloudinary.Api.Secure = true;
        }
        else
        {
            _logger.LogWarning("Chưa cấu hình Cloudinary, ảnh sẽ không được lưu trữ.");
        }
    }

    public async Task<string?> TaiLenAsync(byte[] anh, string thuMuc)
    {
        if (_cloudinary is null || anh.Length == 0) return null;

        try
        {
            using var luong = new MemoryStream(anh);
            var tenTep = Guid.NewGuid().ToString("N");

            var thamSo = new ImageUploadParams
            {
                File = new FileDescription(tenTep, luong),
                Folder = thuMuc,
                PublicId = tenTep,
                Overwrite = false
            };

            var ketQua = await _cloudinary.UploadAsync(thamSo);

            if (ketQua.StatusCode == HttpStatusCode.OK && !string.IsNullOrWhiteSpace(ketQua.PublicId))
            {
                return ketQua.PublicId;
            }

            _logger.LogWarning("Cloudinary tải ảnh thất bại: {Loi}", ketQua.Error?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không tải được ảnh lên Cloudinary.");
            return null;
        }
    }

    public string? TaoUrl(string? publicId, int chieuRongToiDa = 900)
    {
        if (string.IsNullOrWhiteSpace(publicId) || string.IsNullOrWhiteSpace(_cloudName)) return null;

        return $"https://res.cloudinary.com/{_cloudName}/image/upload/c_limit,w_{chieuRongToiDa},q_auto/{publicId}";
    }
}