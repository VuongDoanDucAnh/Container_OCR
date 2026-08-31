using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace DoAnOlympics.Api.Services;

public interface IGoogleSheetsService
{
    Task GhiDongLuongAsync(string maTaiXe, string hoTen, string maContainer, DateTime thoiGian);
}

public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly IConfiguration _config;

    public GoogleSheetsService(IConfiguration config)
    {
        _config = config;
    }

    public async Task GhiDongLuongAsync(string maTaiXe, string hoTen, string maContainer, DateTime thoiGian)
    {
        string duongDanServiceAccount = _config["GoogleSheets:ServiceAccountJsonPath"]
            ?? throw new InvalidOperationException("Chưa cấu hình GoogleSheets:ServiceAccountJsonPath");
        string spreadsheetId = _config["GoogleSheets:SpreadsheetId"]
            ?? throw new InvalidOperationException("Chưa cấu hình GoogleSheets:SpreadsheetId");
        string tenSheet = _config["GoogleSheets:SheetName"] ?? "BangLuong";

        GoogleCredential credential;
        await using (var stream = new FileStream(duongDanServiceAccount, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(SheetsService.Scope.Spreadsheets);
        }

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DoAn Olympics Container OCR"
        });

        var valueRange = new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object> { maTaiXe, hoTen, maContainer, thoiGian.ToString("yyyy-MM-dd HH:mm:ss") }
            }
        };

        var appendRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, $"{tenSheet}!A:D");
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        await appendRequest.ExecuteAsync();
    }
}
