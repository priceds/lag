namespace Lag;

internal enum Verdict
{
    Excellent,
    Good,
    Unstable,
    Poor,
    Offline
}

internal sealed record TestProgress(int Stage, string Phase, string Detail);

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
