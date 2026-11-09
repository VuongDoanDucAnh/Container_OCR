using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoAnOlympics.Api.DTOs;

namespace DoAnOlympics.Api.Services;

public interface IGeminiOdoClient
{
    Task<DocOdoKetQuaAi> DocOdoAsync(byte[] anhBytes, string contentType);
}

public class GeminiOdoClient : IGeminiOdoClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    private const string SystemPrompt = @"Bạn là hệ thống đọc đồng hồ công-tơ-mét (odometer) trên xe tải, đầu kéo container tại Việt Nam.
                Chỉ ghi lại những gì nhìn thấy trong ảnh. Tuyệt đối không suy đoán chữ số bị che, mờ hoặc lóa sáng.
                Ảnh có thể bị nghiêng, bẩn, lóa hoặc chụp xiên: hãy tự xoay về đúng chiều trước khi đọc.

                Hãy trích xuất và trả về ĐÚNG cấu trúc JSON thuần (không kèm markdown):
                {
                  ""la_dong_ho_odo"": true,
                  ""odo_km"": 152340,
                  ""don_vi"": ""km"",
                  ""truong_nghi_ngo"": []
                }

                Quy tắc:
                - odo_km là TỔNG số km xe đã đi (thường ghi ODO hoặc TOTAL), không phải quãng đường chuyến (TRIP A, TRIP B).
                - odo_km là số nguyên thuần túy, không có dấu ngăn cách hàng nghìn. Nếu có chữ số thập phân nhỏ thì bỏ phần thập phân.
                - Nếu đồng hồ hiển thị dặm (mi, miles) thì đặt don_vi là ""mi"", ngược lại đặt là ""km"".
                - Nếu không đọc rõ toàn bộ chữ số thì để odo_km là null và thêm ""odo_km"" vào mảng ""truong_nghi_ngo"".
                - Nếu ảnh không phải đồng hồ công-tơ-mét, trả về la_dong_ho_odo = false và odo_km = null.";

    public GeminiOdoClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"] ?? string.Empty;
        _model = config["Gemini:VisionModel"] ?? config["Gemini:Model"] ?? "gemini-2.5-flash";
    }

    public async Task<DocOdoKetQuaAi> DocOdoAsync(byte[] anhBytes, string contentType)
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

    private static DocOdoKetQuaAi ParseGeminiResponse(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return new DocOdoKetQuaAi { LaDongHoOdo = false };
        }

        var textContent = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(textContent))
        {
            return new DocOdoKetQuaAi { LaDongHoOdo = false };
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

        var jsonDoc = JsonSerializer.Deserialize<GeminiOdoJsonModel>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        });

        if (jsonDoc is null)
        {
            return new DocOdoKetQuaAi { LaDongHoOdo = false };
        }

        int? odo = null;
        if (jsonDoc.OdoKm is >= 0 and <= 9999999)
        {
            odo = (int)Math.Truncate(jsonDoc.OdoKm.Value);
        }

        return new DocOdoKetQuaAi
        {
            LaDongHoOdo = jsonDoc.LaDongHoOdo,
            Odo = odo,
            DonVi = jsonDoc.DonVi,
            TruongNghiNgo = jsonDoc.TruongNghiNgo ?? new List<string>()
        };
    }

    private sealed class GeminiOdoJsonModel
    {
        [JsonPropertyName("la_dong_ho_odo")]
        public bool LaDongHoOdo { get; set; }

        [JsonPropertyName("odo_km")]
        public decimal? OdoKm { get; set; }

        [JsonPropertyName("don_vi")]
        public string? DonVi { get; set; }

        [JsonPropertyName("truong_nghi_ngo")]
        public List<string>? TruongNghiNgo { get; set; }
    }
}