namespace OrderHub.Core.Ai;

/// <summary>
/// AI 服務暫時不可用。呼叫端應轉成 503 等明確回應，而不是 500。
/// </summary>
public class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
