namespace Lag;

internal enum Verdict
{
    Excellent,
    Good,
    Unstable,
    Poor,
    Offline
}

internal sealed record TestProgress(
    int Stage,
    string Phase,
    string Detail,
    LiveTelemetry? Telemetry = null);

internal sealed record LiveTelemetry(
    double? LatencyMs = null,
    double? JitterMs = null,
    double? ProbeLossPercent = null,
    double? DownloadMbps = null,
    double? UploadMbps = null,
    double? LoadedLatencyMs = null,
    long? DownloadBytes = null,
    long? UploadBytes = null,
    int? SamplesCompleted = null)
{
    public LiveTelemetry Merge(LiveTelemetry? update) => update is null
        ? this
        : new(
            update.LatencyMs ?? LatencyMs,
            update.JitterMs ?? JitterMs,
            update.ProbeLossPercent ?? ProbeLossPercent,
            update.DownloadMbps ?? DownloadMbps,
            update.UploadMbps ?? UploadMbps,
            update.LoadedLatencyMs ?? LoadedLatencyMs,
            update.DownloadBytes ?? DownloadBytes,
            update.UploadBytes ?? UploadBytes,
            update.SamplesCompleted ?? SamplesCompleted);
}

internal sealed class NetworkReport
{
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public string Platform { get; init; } = $"{Environment.OSVersion.Platform}/{RuntimeInformation.OSArchitecture}";
    public required ConnectionInfo Connection { get; init; }
    public required NetworkMetrics Metrics { get; init; }
    public int Score { get; set; }
    public Verdict Verdict { get; set; }
    public string Diagnosis { get; set; } = "";
    public List<string> LikelySymptoms { get; } = [];
    public List<string> TryFirst { get; } = [];
    public List<string> Notes { get; } = [];
}

internal sealed class ConnectionInfo
{
    public string Type { get; set; } = "Network";
    public string? Interface { get; set; }
    public bool ProxyDetected { get; set; }
    public string? ProxySource { get; set; }
    public bool VpnDetected { get; set; }
    public string? VpnInterface { get; set; }
}

internal sealed class NetworkMetrics
{
    public bool Reachable { get; set; }
    public double LatencyMs { get; set; }
    public double JitterMs { get; set; }
    public double ProbeLossPercent { get; set; }
    public double DnsMs { get; set; }
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double LoadedLatencyMs { get; set; }
    public double BufferbloatMs { get; set; }
}
