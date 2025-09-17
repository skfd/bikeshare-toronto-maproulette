using Serilog;
using Serilog.Events;
using System.Diagnostics;

namespace prepareBikeParking.Logging;

public static class StructuredLoggingExtensions
{
    public static ILogger ForBikeShareSystem(this ILogger logger, string systemName, int systemId)
    {
        return logger.ForContext("BikeShareSystem", systemName)
                     .ForContext("SystemId", systemId);
    }

    public static ILogger ForOperation(this ILogger logger, string operationName)
    {
        return logger.ForContext("Operation", operationName);
    }

    public static IDisposable TimedOperation(this ILogger logger, string operationName)
    {
        return new TimedOperationLogger(logger, operationName);
    }

    public static void LogPerformanceMetric(this ILogger logger, string metricName, double value, string? unit = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["MetricName"] = metricName,
            ["MetricValue"] = value,
            ["MetricType"] = "Performance"
        };

        if (!string.IsNullOrEmpty(unit))
            properties["MetricUnit"] = unit;

        logger.Information("Performance metric recorded: {MetricName}={MetricValue}{MetricUnit}",
            metricName, value, unit ?? "");
    }

    public static void LogDataQualityIssue(this ILogger logger, string issueType, string details, object? data = null)
    {
        logger.Warning("Data quality issue detected: {IssueType}. Details: {Details}. Data: {@Data}",
            issueType, details, data);
    }

    public static void LogApiCall(this ILogger logger, string apiName, string endpoint, int statusCode, long elapsedMs)
    {
        var level = statusCode >= 200 && statusCode < 300 ? LogEventLevel.Information : LogEventLevel.Warning;

        logger.Write(level,
            "API call to {ApiName} completed. Endpoint: {Endpoint}, Status: {StatusCode}, Duration: {ElapsedMs}ms",
            apiName, endpoint, statusCode, elapsedMs);
    }
}

internal class TimedOperationLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly DateTime _startTime;

    public TimedOperationLogger(ILogger logger, string operationName)
    {
        _operationName = operationName;
        _stopwatch = Stopwatch.StartNew();
        _startTime = DateTime.UtcNow;
        _logger = logger
            .ForContext("Operation", operationName)
            .ForContext("StartTime", _startTime);
        _logger.Debug("Starting operation: {Operation}", operationName);
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        var level = _stopwatch.ElapsedMilliseconds > 5000 ? LogEventLevel.Warning : LogEventLevel.Information;

        _logger
            .ForContext("Operation", _operationName)
            .ForContext("StartTime", _startTime)
            .ForContext("ElapsedMs", _stopwatch.ElapsedMilliseconds)
            .ForContext("EndTime", DateTime.UtcNow)
            .Write(level, "Completed operation: {Operation} in {ElapsedMs}ms",
                   _operationName, _stopwatch.ElapsedMilliseconds);
    }
}