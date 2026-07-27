namespace Lag;

internal sealed class NetworkQualityTester : IDisposable
{
    private const string DownloadEndpoint = "https://speed.cloudflare.com/__down";
    private const string UploadEndpoint = "https://speed.cloudflare.com/__up";
    private readonly HttpClient _client;

    public NetworkQualityTester()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 16
        };
        _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("lag-dotnet/1.0");
    }

    public async Task<NetworkReport> RunAsync(
        bool includeBandwidth,
        Action<TestProgress>? progress,
        CancellationToken cancellationToken,
        bool includeNetOps = false)
    {
        progress?.Invoke(new(0, "Inspecting connection", "interface · VPN · proxy"));
        var connection = InspectConnection();
        var metrics = new NetworkMetrics();

        progress?.Invoke(new(1, "Opening a clean route", "DNS · TCP · TLS"));
        await MeasureLatencyAsync(1, TimeSpan.Zero, cancellationToken);

        progress?.Invoke(new(2, "Testing responsiveness", "latency · jitter · stability"));
        var latencyTask = MeasureLatencyAsync(
            12,
            TimeSpan.FromMilliseconds(90),
            cancellationToken,
            snapshot => progress?.Invoke(new(
                2,
                "Testing responsiveness",
                $"probe {snapshot.Completed}/12",
                new(
                    LatencyMs: Statistics.Median(snapshot.Samples),
                    JitterMs: Statistics.Jitter(snapshot.Samples),
                    ProbeLossPercent: snapshot.Failures / (double)snapshot.Completed * 100,
                    SamplesCompleted: snapshot.Completed))));
        var dnsTask = MeasureDnsAsync(cancellationToken);
        var latency = await latencyTask;
        metrics.DnsMs = await dnsTask;
        metrics.Reachable = latency.Samples.Count > 0;
        metrics.LatencyMs = Statistics.Median(latency.Samples);
        metrics.JitterMs = Statistics.Jitter(latency.Samples);
        metrics.ProbeLossPercent = latency.Failures / 12d * 100d;

        if (includeBandwidth && metrics.Reachable)
        {
            progress?.Invoke(new(3, "Putting the link under load", "download · loaded latency"));
            var downloadTask = MeasureDownloadAsync(
                cancellationToken,
                (rate, bytes) => progress?.Invoke(new(
                    3,
                    "Putting the link under load",
                    "download · loaded responsiveness",
                    new(DownloadMbps: rate, DownloadBytes: bytes))));
            var loadedTask = MeasureLatencyAsync(
                10,
                TimeSpan.FromMilliseconds(250),
                cancellationToken,
                snapshot => progress?.Invoke(new(
                    3,
                    "Putting the link under load",
                    $"loaded probe {snapshot.Completed}/10",
                    new(
                        LoadedLatencyMs: Statistics.Median(snapshot.Samples),
                        SamplesCompleted: snapshot.Completed))));
            await Task.WhenAll(downloadTask, loadedTask);
            metrics.DownloadMbps = await downloadTask;
            var loaded = await loadedTask;
            metrics.LoadedLatencyMs = Statistics.Median(loaded.Samples);
            metrics.BufferbloatMs = Math.Max(0, metrics.LoadedLatencyMs - metrics.LatencyMs);

            progress?.Invoke(new(4, "Testing the return path", "upload throughput"));
            metrics.UploadMbps = await MeasureUploadAsync(
                2_000_000,
                cancellationToken,
                (rate, bytes) => progress?.Invoke(new(
                    4,
                    "Testing the return path",
                    "upload throughput",
                    new(UploadMbps: rate, UploadBytes: bytes))));
        }

        progress?.Invoke(new(5, "Reading the connection", "quality · symptoms · fixes"));
        var report = new NetworkReport { Connection = connection, Metrics = metrics };
        Diagnosis.Apply(report);
        if (includeNetOps)
        {
            progress?.Invoke(new(6, "Tracing fault domains", "gateway · stacks · handshakes · route"));
            report.NetOps = await NetOpsCollector.CollectAsync(connection, cancellationToken);
        }
        return report;
    }

    private async Task<(List<double> Samples, int Failures)> MeasureLatencyAsync(
        int count,
        TimeSpan spacing,
        CancellationToken cancellationToken,
        Action<LatencySnapshot>? progress = null)
    {
        var samples = new List<double>(count);
        var failures = 0;
        for (var i = 0; i < count; i++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{DownloadEndpoint}?bytes=0");
                var watch = Stopwatch.StartNew();
                using var response = await _client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                watch.Stop();
                if (response.IsSuccessStatusCode)
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                else
                    failures++;
            }
            catch (HttpRequestException)
            {
                failures++;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }
            progress?.Invoke(new(samples.ToArray(), failures, i + 1));

            if (i + 1 < count && spacing > TimeSpan.Zero)
                await Task.Delay(spacing, cancellationToken);
        }
        return (samples, failures);
    }

    private static async Task<double> MeasureDnsAsync(CancellationToken cancellationToken)
    {
        var samples = new List<double>(3);
        foreach (var host in new[] { "cloudflare.com", "example.com", "github.com" })
        {
            try
            {
                var watch = Stopwatch.StartNew();
                await Dns.GetHostAddressesAsync(host, cancellationToken);
                watch.Stop();
                samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            catch (SocketException) { }
        }
        return Statistics.Median(samples);
    }

    private async Task<double> MeasureDownloadAsync(
        CancellationToken cancellationToken,
        Action<double, long>? progress)
    {
        var first = await MeasureDownloadOnceAsync(1_000_000, cancellationToken, progress);
        if (first is <= 0 or < 10)
            return first;
        var second = await MeasureDownloadOnceAsync(8_000_000, cancellationToken, progress);
        return second > 0 ? second : first;
    }

    private async Task<double> MeasureDownloadOnceAsync(
        int bytes,
        CancellationToken cancellationToken,
        Action<double, long>? progress)
    {
        try
        {
            var watch = Stopwatch.StartNew();
            using var response = await _client.GetAsync(
                $"{DownloadEndpoint}?bytes={bytes}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return 0;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[64 * 1024];
            long received = 0;
            var lastUpdate = TimeSpan.Zero;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                received += read;
                if (watch.Elapsed - lastUpdate >= TimeSpan.FromMilliseconds(80))
                {
                    progress?.Invoke(
                        received * 8d / watch.Elapsed.TotalSeconds / 1_000_000d,
                        received);
                    lastUpdate = watch.Elapsed;
                }
            }
            watch.Stop();
            var rate = received * 8d / watch.Elapsed.TotalSeconds / 1_000_000d;
            progress?.Invoke(rate, received);
            return rate;
        }
        catch (HttpRequestException) { return 0; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return 0; }
    }

    private async Task<double> MeasureUploadAsync(
        int bytes,
        CancellationToken cancellationToken,
        Action<double, long>? progress)
    {
        try
        {
            var payload = new byte[bytes];
            Random.Shared.NextBytes(payload.AsSpan(0, 32));
            var watch = Stopwatch.StartNew();
            var lastUpdate = TimeSpan.Zero;
            using var content = new ProgressUploadContent(payload, sent =>
            {
                if (watch.Elapsed - lastUpdate < TimeSpan.FromMilliseconds(80) && sent < bytes)
                    return;
                progress?.Invoke(sent * 8d / watch.Elapsed.TotalSeconds / 1_000_000d, sent);
                lastUpdate = watch.Elapsed;
            });
            using var response = await _client.PostAsync(UploadEndpoint, content, cancellationToken);
            watch.Stop();
            var rate = response.IsSuccessStatusCode
                ? bytes * 8d / watch.Elapsed.TotalSeconds / 1_000_000d
                : 0;
            progress?.Invoke(rate, bytes);
            return rate;
        }
        catch (HttpRequestException) { return 0; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return 0; }
    }

    private static ConnectionInfo InspectConnection()
    {
        var result = new ConnectionInfo();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up ||
                network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            result.Interface ??= network.Name;
            var interfaceName = network.Name.ToLowerInvariant();
            var looksWireless = network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                interfaceName.StartsWith("wl", StringComparison.Ordinal) ||
                                interfaceName.StartsWith("wlan", StringComparison.Ordinal) ||
                                interfaceName.StartsWith("wi-fi", StringComparison.Ordinal) ||
                                interfaceName.StartsWith("airport", StringComparison.Ordinal);
            if (looksWireless)
            {
                result.Type = "Wi-Fi";
                result.Interface = network.Name;
            }
            else if (result.Type == "Network" && network.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                result.Type = "Ethernet";
            }

            var lowered = $"{network.Name} {network.Description}".ToLowerInvariant();
            if (new[] { "tun", "tap", "vpn", "wireguard", "tailscale", "zerotier", "utun" }
                .Any(lowered.Contains))
            {
                result.VpnDetected = true;
                result.VpnInterface = network.Name;
            }
        }

        foreach (var name in new[] { "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "https_proxy", "http_proxy", "all_proxy" })
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            {
                result.ProxyDetected = true;
                result.ProxySource = name;
                break;
            }
        }
        return result;
    }

    public void Dispose() => _client.Dispose();
}

internal sealed record LatencySnapshot(
    IReadOnlyList<double> Samples,
    int Failures,
    int Completed);

internal sealed class ProgressUploadContent(
    byte[] payload,
    Action<long> progress) : HttpContent
{
    protected override bool TryComputeLength(out long length)
    {
        length = payload.LongLength;
        return true;
    }

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context)
    {
        const int chunkSize = 64 * 1024;
        long sent = 0;
        while (sent < payload.LongLength)
        {
            var count = (int)Math.Min(chunkSize, payload.LongLength - sent);
            await stream.WriteAsync(payload.AsMemory((int)sent, count));
            sent += count;
            progress(sent);
        }
    }
}

internal static class Statistics
{
    public static double Median(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0)
            return 0;
        var middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }

    public static double Jitter(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return 0;
        var total = 0d;
        for (var i = 1; i < values.Count; i++)
            total += Math.Abs(values[i] - values[i - 1]);
        return total / (values.Count - 1);
    }
}
