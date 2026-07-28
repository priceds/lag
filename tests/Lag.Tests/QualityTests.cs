using System.Text;
using System.Text.Json;
using Spectre.Console;
using Xunit;

namespace Lag.Tests;

public sealed class QualityTests
{
    [Fact]
    public void WindowsConsoleUsesUtf8BeforeRendering()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var originalEncoding = Console.OutputEncoding;
        try
        {
            Console.OutputEncoding = Encoding.Latin1;
            Program.ConfigureConsoleEncoding();

            Assert.Equal(Encoding.UTF8.CodePage, Console.OutputEncoding.CodePage);
            Assert.True(AnsiConsole.Profile.Capabilities.Unicode);
        }
        finally
        {
            Console.OutputEncoding = originalEncoding;
        }
    }

    [Fact]
    public void MedianHandlesEvenSamples()
    {
        Assert.Equal(4, Statistics.Median([9, 1, 5, 3]));
    }

    [Fact]
    public void JitterUsesConsecutiveDifferences()
    {
        Assert.Equal(7.5, Statistics.Jitter([10, 20, 15]), precision: 3);
    }

    [Fact]
    public void PacketLossProducesUsefulDiagnosis()
    {
        var report = ReportWith(new NetworkMetrics
        {
            Reachable = true,
            LatencyMs = 20,
            JitterMs = 3,
            ProbeLossPercent = 8,
            DnsMs = 12,
            DownloadMbps = 100,
            UploadMbps = 20
        });

        Diagnosis.Apply(report);

        Assert.Contains("losing requests", report.Diagnosis);
        Assert.NotEqual(Verdict.Excellent, report.Verdict);
        Assert.NotEmpty(report.TryFirst);
    }

    [Fact]
    public void BufferbloatIsDistinguishedFromSlowBandwidth()
    {
        var report = ReportWith(new NetworkMetrics
        {
            Reachable = true,
            LatencyMs = 20,
            JitterMs = 2,
            DnsMs = 10,
            DownloadMbps = 100,
            UploadMbps = 20,
            LoadedLatencyMs = 200,
            BufferbloatMs = 180
        });

        Diagnosis.Apply(report);

        Assert.Contains("bufferbloat", report.Diagnosis);
    }

    [Fact]
    public void UnreachableEndpointIsOffline()
    {
        var report = ReportWith(new NetworkMetrics());

        Diagnosis.Apply(report);

        Assert.Equal(Verdict.Offline, report.Verdict);
        Assert.Equal(0, report.Score);
    }

    [Fact]
    public void LiveTelemetryKeepsTransferDirectionsIndependent()
    {
        var telemetry = new LiveTelemetry(DownloadMbps: 80, DownloadBytes: 8_000_000)
            .Merge(new LiveTelemetry(UploadMbps: 20, UploadBytes: 500_000));

        Assert.Equal(8_000_000, telemetry.DownloadBytes);
        Assert.Equal(500_000, telemetry.UploadBytes);
        Assert.Equal(80, telemetry.DownloadMbps);
        Assert.Equal(20, telemetry.UploadMbps);
    }

    [Fact]
    public void NetOpsJsonUsesConventionalIpFamilyNames()
    {
        var report = ReportWith(new NetworkMetrics());
        report.NetOps = new NetOpsDiagnostics();

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("\"ipv4\"", json);
        Assert.Contains("\"ipv6\"", json);
        Assert.DoesNotContain("\"i_pv4\"", json);
    }

    [Fact]
    public void LinuxWifiOutputBecomesStructuredFields()
    {
        var wifi = NetOpsCollector.ParseLinuxWifi(
            "Connected to 1c:3b:f3:de:de:c0 (on wlp3s0) · " +
            "SSID: Office_5G · freq: 5745.0 · signal: -48 dBm · " +
            "rx bitrate: 433.3 MBit/s 80MHz · tx bitrate: 390.0 MBit/s 80MHz");

        Assert.True(wifi.Available);
        Assert.Equal("Office_5G", wifi.Ssid);
        Assert.Equal("1c:3b:f3:de:de:c0", wifi.Bssid);
        Assert.Equal("5 GHz", wifi.Band);
        Assert.Equal("149", wifi.Channel);
        Assert.Equal("-48 dBm", wifi.Signal);
    }

    private static NetworkReport ReportWith(NetworkMetrics metrics) =>
        new() { Connection = new ConnectionInfo(), Metrics = metrics };
}
