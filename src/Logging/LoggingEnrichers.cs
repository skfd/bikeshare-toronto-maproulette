using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace prepareBikeParking.Logging;

public class SystemContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "MachineName", Environment.MachineName));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "ProcessId", Environment.ProcessId));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "ProcessName", Process.GetCurrentProcess().ProcessName));
    }
}

public class ExecutionContextEnricher : ILogEventEnricher
{
    private readonly string _systemName;
    private readonly int? _systemId;

    public ExecutionContextEnricher(string? systemName = null, int? systemId = null)
    {
        _systemName = systemName ?? "Unknown";
        _systemId = systemId;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!string.IsNullOrEmpty(_systemName))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "BikeShareSystem", _systemName));
        }

        if (_systemId.HasValue)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "SystemId", _systemId.Value));
        }
    }
}

public class PerformanceEnricher : ILogEventEnricher
{
    private readonly Stopwatch _stopwatch;

    public PerformanceEnricher()
    {
        _stopwatch = Stopwatch.StartNew();
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "ElapsedMs", _stopwatch.ElapsedMilliseconds));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "MemoryMB", GC.GetTotalMemory(false) / (1024 * 1024)));
    }
}