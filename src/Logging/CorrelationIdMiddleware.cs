using Serilog.Context;

namespace prepareBikeParking.Logging;

public static class CorrelationIdMiddleware
{
    private static readonly AsyncLocal<string> _correlationId = new();

    public static string CorrelationId
    {
        get => _correlationId.Value ?? GenerateCorrelationId();
        private set => _correlationId.Value = value;
    }

    public static string GenerateCorrelationId() =>
        $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";

    public static IDisposable BeginCorrelationScope(string? correlationId = null)
    {
        CorrelationId = correlationId ?? GenerateCorrelationId();
        return LogContext.PushProperty("CorrelationId", CorrelationId);
    }

    public static void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }
}