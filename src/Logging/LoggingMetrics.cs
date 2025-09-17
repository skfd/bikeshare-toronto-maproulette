namespace prepareBikeParking.Logging;

public static class LoggingMetrics
{
    public class OperationMetrics
    {
        public string OperationName { get; set; } = "";
        public long ElapsedMs { get; set; }
        public bool Success { get; set; }
        public string? ErrorType { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }

    public class DataProcessingMetrics
    {
        public int ItemsProcessed { get; set; }
        public int ItemsFailed { get; set; }
        public int ItemsSkipped { get; set; }
        public long TotalProcessingTimeMs { get; set; }
        public double AverageProcessingTimeMs => ItemsProcessed > 0 ? TotalProcessingTimeMs / (double)ItemsProcessed : 0;
    }

    public class ApiCallMetrics
    {
        public string ApiName { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public int StatusCode { get; set; }
        public long ResponseTimeMs { get; set; }
        public long ResponseSizeBytes { get; set; }
        public int RetryCount { get; set; }
        public bool CacheHit { get; set; }
    }

    public class ComparisonMetrics
    {
        public int TotalStations { get; set; }
        public int StationsAdded { get; set; }
        public int StationsRemoved { get; set; }
        public int StationsMoved { get; set; }
        public int StationsRenamed { get; set; }
        public int StationsUnchanged { get; set; }
        public long ComparisonTimeMs { get; set; }

        public double ChangePercentage =>
            TotalStations > 0 ? ((StationsAdded + StationsRemoved + StationsMoved + StationsRenamed) / (double)TotalStations) * 100 : 0;
    }

    public class SystemHealthMetrics
    {
        public long MemoryUsageMB { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ThreadCount { get; set; }
        public long GCGen0Collections { get; set; }
        public long GCGen1Collections { get; set; }
        public long GCGen2Collections { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}