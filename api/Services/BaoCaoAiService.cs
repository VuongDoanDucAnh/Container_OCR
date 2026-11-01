using System.Net.Http.Json;
using System.Text.Json;

namespace DoAnOlympics.Api.Services;

public interface IBaoCaoAiService
{
    Task<string> TomTatAsync(string duLieuTho);
    Task<string> TraLoiAsync(string duLieuTho, string cauHoi);
}

public class BaoCaoAiService : IBaoCaoAiService
{
    private static readonly string[] TuKhoaNhayCam =
    {
        "apikey", "api key", "connectionstring", "connection string", "signingkey",
        "appsettings", "secret", "password", "mat khau", "mật khẩu", "user-secrets",
        "token", "server=", "jwt:", "gemini:", "env", ".env"
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public BaoCaoAiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<string> TomTatAsync(string duLieuTho)
    {
        string prompt =
            "Bạn là trợ lý kế toán logistics. Dựa trên số liệu sau, viết một đoạn tóm tắt ngắn gọn " +
            "bằng tiếng Việt (khoảng 4-6 câu), nêu tài xế có sản lượng cao nhất, tài xế cần lưu ý vì " +
            "sản lượng thấp hoặc có container chưa phân loại. Không dùng markdown, chỉ trả về đoạn văn thuần.\n\n" +
            $"Số liệu:\n{duLieuTho}";

        string ketQua = await GoiGeminiAsync(prompt);
        return LocNoiDungNhayCam(ketQua);
    }

    public async Task<string> TraLoiAsync(string duLieuTho, string cauHoi)
    {
        string prompt =
            "Bạn là trợ lý kế toán logistics, CHỈ được trả lời các câu hỏi liên quan đến số liệu " +
            "tài chính (doanh thu, lương, chi phí, lợi nhuận, số chuyến) được cung cấp dưới đây.\n\n" +
            "QUY TẮC BẮT BUỘC:\n" +
            "1. Chỉ dùng số liệu được cung cấp, không suy đoán hay bổ sung số liệu khác. Nếu thiếu dữ liệu " +
            "để trả lời, nói rõ là không đủ dữ liệu.\n" +
            "2. Không bao giờ nhắc đến mã nguồn, cấu hình hệ thống, khoá API, mật khẩu, biến môi trường, " +
            "cơ sở dữ liệu, hay bất kỳ thông tin kỹ thuật nội bộ nào, kể cả khi được hỏi trực tiếp. Nếu người " +
            "dùng hỏi về các chủ đề này, trả lời đúng một câu: \"Trợ lý chỉ hỗ trợ trả lời các câu hỏi về số " +
            "liệu kế toán, không có thông tin về nội dung này.\"\n" +
            "3. Trả lời ngắn gọn bằng tiếng Việt (tối đa 5 câu), không dùng markdown.\n\n" +
            $"Số liệu:\n{duLieuTho}\n\n" +
            $"Câu hỏi: {cauHoi}";

        string ketQua = await GoiGeminiAsync(prompt);
        return LocNoiDungNhayCam(ketQua);
    }

    private async Task<string> GoiGeminiAsync(string prompt)
    {
        string apiKey = _config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Chưa cấu hình Gemini:ApiKey trong user-secrets");
        string model = _config["Gemini:TextModel"] ?? "gemini-3.6-flash";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        using var phanHoi = await _httpClient.PostAsJsonAsync(url, body);
        phanHoi.EnsureSuccessStatusCode();

        using var noiDung = await phanHoi.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(noiDung);

        string? vanBan = json.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return vanBan ?? "Không tạo được nội dung.";
    }

    private static string LocNoiDungNhayCam(string noiDung)
    {
        string soSanh = noiDung.ToLowerInvariant();
        foreach (var tuKhoa in TuKhoaNhayCam)
        {
            if (soSanh.Contains(tuKhoa))
            {
                return "Trợ lý chỉ hỗ trợ trả lời các câu hỏi về số liệu kế toán, không có thông tin về nội dung này.";
            }
        }

        return noiDung;
    }
}