using System.Text;
using System.Text.Json;

namespace DoAnOlympics.Api.Services;

public record GeminiOcrResult(bool DocDuoc, string? MaContainer, string? GhiChu);

public interface IGeminiOcrClient
{
    Task<GeminiOcrResult> DocMaContainerAsync(byte[] anhBytes, string mimeType);
}
public class GeminiOcrClient : IGeminiOcrClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private const string Model = "gemini-3.6-flash";

    public GeminiOcrClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<GeminiOcrResult> DocMaContainerAsync(byte[] anhBytes, string mimeType)
    {
        string apiKey = _config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Chưa cấu hình Gemini:ApiKey trong user-secrets");

        string base64Anh = Convert.ToBase64String(anhBytes);

        const string prompt = """
            Bạn là hệ thống OCR chuyên đọc mã container theo chuẩn ISO 6346 (4 chữ cái + 7 số).
            LƯU Ý QUAN TRỌNG: ảnh chụp có thể bị xoay 180 độ hoặc lật gương do góc chụp của tài xế.
            Hãy tự xoay/lật ảnh trong đầu để đọc đúng chiều trước khi trả lời.
            Chỉ trả về mã container bạn đọc được, không thêm giải thích.
            Nếu không đọc được rõ ràng, đặt doc_duoc = false.
            """;

        var requestBody = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new { inline_data = new { mime_type = mimeType, data = base64Anh } }
                    }
                }
            },
            generationConfig = new
            {
                thinkingConfig = new { thinkingLevel = "low" },
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        doc_duoc = new { type = "BOOLEAN" },
                        ma_container = new { type = "STRING" }
                    },
                    required = new[] { "doc_duoc" }
                }
            }
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");

        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        string responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new GeminiOcrResult(false, null, $"Lỗi Gemini API ({(int)response.StatusCode}): {responseJson}");
        }

        using var doc = JsonDocument.Parse(responseJson);

        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0)
        {
            return new GeminiOcrResult(false, null, "Gemini không trả về candidate nào (có thể bị chặn bởi safety filter)");
        }

        var content = candidates[0].GetProperty("content");
        if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
        {
            string finishReason = candidates[0].TryGetProperty("finishReason", out var fr) ? fr.GetString() ?? "" : "";
            return new GeminiOcrResult(false, null, $"Không có nội dung trả về, finishReason={finishReason} (thường do hết token cho phần 'thinking')");
        }

        string? textOutput = parts[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(textOutput))
        {
            return new GeminiOcrResult(false, null, "Nội dung trả về rỗng");
        }

        using var parsed = JsonDocument.Parse(textOutput);
        bool docDuoc = parsed.RootElement.GetProperty("doc_duoc").GetBoolean();
        string? maContainer = parsed.RootElement.TryGetProperty("ma_container", out var maEl) ? maEl.GetString() : null;

        return new GeminiOcrResult(docDuoc, maContainer, null);
    }
}
