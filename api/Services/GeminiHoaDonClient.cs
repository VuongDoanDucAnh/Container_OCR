using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoAnOlympics.Api.DTOs;

namespace DoAnOlympics.Api.Services;

public interface IGeminiHoaDonClient
{
    Task<DocHoaDonKetQuaAi> DocHoaDonAsync(byte[] anhBytes, string contentType);
}

public class GeminiHoaDonClient : IGeminiHoaDonClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    private const string SystemPrompt = @"Bạn là hệ thống trích xuất dữ liệu từ ảnh hóa đơn bán lẻ nhiên liệu (xăng, dầu diesel) tại Việt Nam.
                Chỉ ghi lại những gì nhìn thấy trong ảnh. Tuyệt đối không suy đoán, không tự tính toán thay hóa đơn.
                Ảnh có thể bị nghiêng, xoay, mờ, nhàu hoặc chụp xiên: hãy tự xoay về đúng chiều trước khi đọc.

                Hãy trích xuất và trả về ĐÚNG cấu trúc JSON thuần (không kèm markdown):
                {
                  ""la_hoa_don"": true,
                  ""so_lit"": 45.32,
                  ""don_gia"": 20500,
                  ""thanh_tien"": 929060,
                  ""loai_nhien_lieu"": ""DO 0,05S"",
                  ""ngay_gio"": ""2026-10-23 14:30:00"",
                  ""ten_cua_hang"": ""Cửa hàng xăng dầu Petrolimex số 01"",
                  ""truong_nghi_ngo"": []
                }

                Quy tắc xử lý số:
                - Hóa đơn Việt Nam thường dùng dấu chấm (.) ngăn cách hàng nghìn và dấu phẩy (,) cho phần thập phân (ví dụ 20.500 là 20500; 45,32 là 45.32).
                - Các trường số (so_lit, don_gia, thanh_tien) bắt buộc trả về kiểu số thuần túy (number), dùng dấu chấm (.) cho phần thập phân nếu có, không để dấu ngăn hàng nghìn.
                - Không được tự ý làm tròn số.

                Quy tắc độ chắc chắn:
                - Trường nào bị mờ, rách hoặc không nhìn thấy rõ thì để giá trị null.
                - Nếu trường nào nét chữ bị nhòe/không chắc chắn, hãy thêm tên trường đó vào mảng ""truong_nghi_ngo"".
                - Nếu ảnh không phải hóa đơn nhiên liệu, trả về la_hoa_don = false và các trường số là null.";

    public GeminiHoaDonClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"] ?? string.Empty;
        _model = config["Gemini:VisionModel"] ?? config["Gemini:Model"] ?? "gemini-2.5-flash";
    }

    public async Task<DocHoaDonKetQuaAi> DocHoaDonAsync(byte[] anhBytes, string contentType)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình Gemini:ApiKey");
        }

        var base64Anh = Convert.ToBase64String(anhBytes);
        var mimeType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = SystemPrompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Anh
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody, jsonOptions), Encoding.UTF8, "application/json");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var response = await _http.PostAsync(url, jsonContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Lỗi gọi Gemini Vision: {response.StatusCode}");
        }

        var rawJson = await response.Content.ReadAsStringAsync();
        return ParseGeminiResponse(rawJson);
    }

    private static DocHoaDonKetQuaAi ParseGeminiResponse(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return new DocHoaDonKetQuaAi { LaHoaDon = false };
        }

        var textContent = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(textContent))
        {
            return new DocHoaDonKetQuaAi { LaHoaDon = false };
        }

        var cleanJson = textContent.Trim();
        if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanJson = cleanJson[7..];
        }
        else if (cleanJson.StartsWith("```"))
        {
            cleanJson = cleanJson[3..];
        }
        if (cleanJson.EndsWith("```"))
        {
            cleanJson = cleanJson[..^3];
        }
        cleanJson = cleanJson.Trim();

        var jsonDoc = JsonSerializer.Deserialize<GeminiHoaDonJsonModel>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (jsonDoc is null)
        {
            return new DocHoaDonKetQuaAi { LaHoaDon = false };
        }

        return new DocHoaDonKetQuaAi
        {
            LaHoaDon = jsonDoc.LaHoaDon,
            SoLit = jsonDoc.SoLit,
            DonGia = jsonDoc.DonGia,
            ThanhTien = jsonDoc.ThanhTien,
            LoaiNhienLieu = jsonDoc.LoaiNhienLieu,
            NgayGio = jsonDoc.NgayGio,
            TenCuaHang = jsonDoc.TenCuaHang,
            TruongNghiNgo = jsonDoc.TruongNghiNgo ?? new List<string>()
        };
    }

    private sealed class GeminiHoaDonJsonModel
    {
        [JsonPropertyName("la_hoa_don")]
        public bool LaHoaDon { get; set; }

        [JsonPropertyName("so_lit")]
        public decimal? SoLit { get; set; }

        [JsonPropertyName("don_gia")]
        public decimal? DonGia { get; set; }

        [JsonPropertyName("thanh_tien")]
        public decimal? ThanhTien { get; set; }

        [JsonPropertyName("loai_nhien_lieu")]
        public string? LoaiNhienLieu { get; set; }

        [JsonPropertyName("ngay_gio")]
        public string? NgayGio { get; set; }

        [JsonPropertyName("ten_cua_hang")]
        public string? TenCuaHang { get; set; }

        [JsonPropertyName("truong_nghi_ngo")]
        public List<string>? TruongNghiNgo { get; set; }
    }
}