namespace OrderHub.Infrastructure.Gemini;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// 來自 user-secrets 的 Gemini:ApiKey；未設定時 client 會退回環境變數 GEMINI_API_KEY。
    /// </summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gemini-3.5-flash";
    public string Endpoint { get; set; } =
        "https://generativelanguage.googleapis.com/v1/interactions";
    public int MaxRetries { get; set; } = 4;
}
