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
    public NetOpsDiagnostics? NetOps { get; set; }
    public int Score { get; set; }
    public Verdict Verdict { get; set; }
    public string Diagnosis { get; set; } = "";
    public List<string> LikelySymptoms { get; } = [];
    public List<string> TryFirst { get; } = [];
    public List<string> Notes { get; } = [];
}

internal sealed class NetOpsDiagnostics
{
    public string? Gateway { get; set; }
    public double? GatewayLatencyMs { get; set; }
    public List<string> LocalAddresses { get; } = [];
    public List<string> DnsServers { get; } = [];
    public int? Mtu { get; set; }
    public long? LinkSpeedMbps { get; set; }
    public long? ReceivedErrors { get; set; }
    public long? SentErrors { get; set; }
    public long? ReceivedDrops { get; set; }
    public long? SentDrops { get; set; }
    [JsonPropertyName("ipv4")]
    public AddressFamilyDiagnostic IPv4 { get; set; } = new();
    [JsonPropertyName("ipv6")]
    public AddressFamilyDiagnostic IPv6 { get; set; } = new();
    public TimingDiagnostic Timing { get; set; } = new();
    public List<EndpointDiagnostic> Endpoints { get; } = [];
    public WifiRadioDiagnostic Wifi { get; set; } = new();
    public string RouteSummary { get; set; } = "Not collected";
    public string MtuStatus { get; set; } = "Interface MTU only; path MTU was not actively probed";
    public string TcpStatus { get; set; } = "Per-flow retransmission counters unavailable through the portable socket API";
    public string CaptivePortalStatus { get; set; } = "No unexpected HTTPS behavior detected";
    public string? CertificateIssuer { get; set; }
}

internal sealed class WifiRadioDiagnostic
{
    public bool Available { get; set; }
    public string Status { get; set; } = "Unavailable from this OS without additional privileges or tooling";
    public string? Ssid { get; set; }
    public string? Bssid { get; set; }
    public string? Signal { get; set; }
    public string? Frequency { get; set; }
    public string? Band { get; set; }
    public string? Channel { get; set; }
    public string? ReceiveRate { get; set; }
    public string? TransmitRate { get; set; }
    public string? ReceivedTraffic { get; set; }
    public string? TransmittedTraffic { get; set; }
}

internal sealed class AddressFamilyDiagnostic
{
    public bool Available { get; set; }
    public bool Reachable { get; set; }
    public string? Address { get; set; }
    public double? ConnectMs { get; set; }
    public string Status { get; set; } = "Not available";
}

internal sealed class TimingDiagnostic
{
    public double? DnsMs { get; set; }
    public double? TcpMs { get; set; }
    public double? TlsMs { get; set; }
    public double? FirstByteMs { get; set; }
}

internal sealed record EndpointDiagnostic(
    string Host,
    bool Reachable,
    double? ConnectMs,
    string? Address);

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
