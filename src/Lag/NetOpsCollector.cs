namespace Lag;

internal static class NetOpsCollector
{
    private const string ProbeHost = "speed.cloudflare.com";

    public static async Task<NetOpsDiagnostics> CollectAsync(
        ConnectionInfo connection,
        CancellationToken cancellationToken)
    {
        var result = new NetOpsDiagnostics();
        var network = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(item => item.Name == connection.Interface);
        if (network is not null)
            CollectInterface(network, result);

        if (IPAddress.TryParse(result.Gateway, out var gateway))
            result.GatewayLatencyMs = await PingAsync(gateway, cancellationToken);

        var timing = await MeasureTimingAsync(cancellationToken);
        result.Timing = timing.Timing;
        result.CertificateIssuer = timing.CertificateIssuer;
        result.IPv4 = await MeasureFamilyAsync(AddressFamily.InterNetwork, cancellationToken);
        result.IPv6 = await MeasureFamilyAsync(AddressFamily.InterNetworkV6, cancellationToken);

        foreach (var host in new[] { ProbeHost, "one.one.one.one", "github.com" })
            result.Endpoints.Add(await MeasureEndpointAsync(host, cancellationToken));

        result.Wifi = await ReadWifiDetailsAsync(connection, cancellationToken);
        result.RouteSummary = await ReadRouteAsync(cancellationToken);
        result.MtuStatus = await ReadPathMtuAsync(cancellationToken);
        result.TcpStatus = await ReadTcpStatusAsync(cancellationToken);
        return result;
    }

    private static void CollectInterface(NetworkInterface network, NetOpsDiagnostics result)
    {
        try
        {
            var properties = network.GetIPProperties();
            result.Gateway = properties.GatewayAddresses
                .Select(item => item.Address)
                .FirstOrDefault(address => !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any))
                ?.ToString();
            result.LocalAddresses.AddRange(properties.UnicastAddresses
                .Select(item => item.Address)
                .Where(address => !IPAddress.IsLoopback(address))
                .Select(address => address.ToString()));
            result.DnsServers.AddRange(properties.DnsAddresses.Select(address => address.ToString()));
            result.Mtu = properties.GetIPv4Properties()?.Mtu ?? properties.GetIPv6Properties()?.Mtu;
        }
        catch (NetworkInformationException) { }

        try
        {
            result.LinkSpeedMbps = network.Speed > 0 ? network.Speed / 1_000_000 : null;
            var statistics = network.GetIPStatistics();
            result.ReceivedErrors = statistics.IncomingPacketsWithErrors;
            result.SentErrors = statistics.OutgoingPacketsWithErrors;
            result.ReceivedDrops = statistics.IncomingPacketsDiscarded;
            if (!OperatingSystem.IsMacOS())
                result.SentDrops = statistics.OutgoingPacketsDiscarded;
        }
        catch (NetworkInformationException) { }
        catch (PlatformNotSupportedException) { }
    }

    private static async Task<double?> PingAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, 1200).WaitAsync(cancellationToken);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException) { return null; }
    }

    private static async Task<AddressFamilyDiagnostic> MeasureFamilyAsync(
        AddressFamily family,
        CancellationToken cancellationToken)
    {
        var result = new AddressFamilyDiagnostic();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(ProbeHost, family, cancellationToken);
            var address = addresses.FirstOrDefault();
            result.Available = address is not null;
            result.Address = address?.ToString();
            if (address is null)
            {
                result.Status = $"No {FamilyName(family)} address resolved";
                return result;
            }

            using var client = new TcpClient(family);
            var watch = Stopwatch.StartNew();
            await client.ConnectAsync(address, 443, cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            watch.Stop();
            result.Reachable = true;
            result.ConnectMs = watch.Elapsed.TotalMilliseconds;
            result.Status = "Reachable";
        }
        catch (Exception exception) when (
            exception is SocketException or TimeoutException or OperationCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            result.Status = "Resolved but connection failed";
        }
        return result;
    }

    private static async Task<(TimingDiagnostic Timing, string? CertificateIssuer)> MeasureTimingAsync(
        CancellationToken cancellationToken)
    {
        var timing = new TimingDiagnostic();
        X509Certificate2? certificate = null;
        try
        {
            var dns = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(ProbeHost, cancellationToken);
            dns.Stop();
            timing.DnsMs = dns.Elapsed.TotalMilliseconds;

            var address = addresses.First(item =>
                item.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6);
            using var tcp = new TcpClient(address.AddressFamily);
            var connect = Stopwatch.StartNew();
            await tcp.ConnectAsync(address, 443, cancellationToken);
            connect.Stop();
            timing.TcpMs = connect.Elapsed.TotalMilliseconds;

            using var tls = new SslStream(tcp.GetStream(), false, (_, remote, _, errors) =>
            {
                if (remote is not null)
                    certificate = new X509Certificate2(remote);
                return errors == SslPolicyErrors.None;
            });
            var handshake = Stopwatch.StartNew();
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = ProbeHost,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, cancellationToken);
            handshake.Stop();
            timing.TlsMs = handshake.Elapsed.TotalMilliseconds;

            var firstByte = Stopwatch.StartNew();
            var request = Encoding.ASCII.GetBytes(
                $"GET /__down?bytes=0 HTTP/1.1\r\nHost: {ProbeHost}\r\nConnection: close\r\n\r\n");
            await tls.WriteAsync(request, cancellationToken);
            await tls.FlushAsync(cancellationToken);
            var oneByte = new byte[1];
            var count = await tls.ReadAsync(oneByte, cancellationToken);
            firstByte.Stop();
            if (count > 0)
                timing.FirstByteMs = firstByte.Elapsed.TotalMilliseconds;
        }
        catch (Exception exception) when (
            exception is SocketException or IOException or AuthenticationException or InvalidOperationException)
        {
            // Partial timing data remains useful.
        }
        return (timing, certificate?.Issuer);
    }

    private static async Task<EndpointDiagnostic> MeasureEndpointAsync(
        string host,
        CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            var address = addresses.FirstOrDefault(item =>
                item.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6);
            if (address is null)
                return new(host, false, null, null);
            using var client = new TcpClient(address.AddressFamily);
            var watch = Stopwatch.StartNew();
            await client.ConnectAsync(address, 443, cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            watch.Stop();
            return new(host, true, watch.Elapsed.TotalMilliseconds, address.ToString());
        }
        catch (Exception exception) when (
            exception is SocketException or TimeoutException or OperationCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            return new(host, false, null, null);
        }
    }

    private static async Task<WifiRadioDiagnostic> ReadWifiDetailsAsync(
        ConnectionInfo connection,
        CancellationToken cancellationToken)
    {
        if (connection.Type != "Wi-Fi")
            return new() { Status = "Not a Wi-Fi connection" };

        if (OperatingSystem.IsWindows())
            return ParseWindowsWifi(await RunToolAsync("netsh", "wlan show interfaces", 40, cancellationToken));
        if (OperatingSystem.IsLinux())
            return ParseLinuxWifi(await RunToolAsync("iw", $"dev {connection.Interface} link", 20, cancellationToken));
        if (OperatingSystem.IsMacOS())
            return new() { Status = "macOS restricts portable Wi-Fi radio details; link identified as Wi-Fi" };
        return new();
    }

    internal static WifiRadioDiagnostic ParseLinuxWifi(string output)
    {
        if (output.Contains("not installed or unavailable", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("Not connected", StringComparison.OrdinalIgnoreCase))
            return new() { Status = output };

        var result = new WifiRadioDiagnostic { Available = true, Status = "Connected" };
        foreach (var part in output.Split(" · ", StringSplitOptions.RemoveEmptyEntries))
        {
            var line = part.Trim();
            if (line.StartsWith("Connected to ", StringComparison.OrdinalIgnoreCase))
                result.Bssid = line["Connected to ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            else if (TryValue(line, "SSID:", out var ssid))
                result.Ssid = ssid;
            else if (TryValue(line, "freq:", out var frequency) && double.TryParse(frequency, out var mhz))
            {
                result.Frequency = $"{mhz:0} MHz";
                result.Band = mhz >= 5925 ? "6 GHz" : mhz >= 5000 ? "5 GHz" : "2.4 GHz";
                result.Channel = mhz >= 5000 ? ((int)(mhz - 5000) / 5).ToString() : ((int)(mhz - 2407) / 5).ToString();
            }
            else if (TryValue(line, "signal:", out var signal))
                result.Signal = signal;
            else if (TryValue(line, "rx bitrate:", out var receive))
                result.ReceiveRate = receive;
            else if (TryValue(line, "tx bitrate:", out var transmit))
                result.TransmitRate = transmit;
            else if (TryValue(line, "RX:", out var received))
                result.ReceivedTraffic = received;
            else if (TryValue(line, "TX:", out var transmitted))
                result.TransmittedTraffic = transmitted;
        }
        return result;
    }

    internal static WifiRadioDiagnostic ParseWindowsWifi(string output)
    {
        if (output.Contains("not installed or unavailable", StringComparison.OrdinalIgnoreCase))
            return new() { Status = output };

        var result = new WifiRadioDiagnostic { Available = true, Status = "Connected" };
        foreach (var part in output.Split(" · ", StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf(':');
            if (separator < 0)
                continue;
            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            switch (key.ToLowerInvariant())
            {
                case "ssid": result.Ssid = value; break;
                case "bssid": result.Bssid = value; break;
                case "signal": result.Signal = value; break;
                case "band": result.Band = value; break;
                case "channel": result.Channel = value; break;
                case "receive rate (mbps)": result.ReceiveRate = $"{value} Mbps"; break;
                case "transmit rate (mbps)": result.TransmitRate = $"{value} Mbps"; break;
            }
        }
        return result;
    }

    private static bool TryValue(string line, string prefix, out string value)
    {
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }
        value = "";
        return false;
    }

    private static async Task<string> ReadRouteAsync(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
            return await RunToolAsync("tracert", $"-d -h 8 -w 700 {ProbeHost}", 10, cancellationToken);
        return await RunToolAsync("traceroute", $"-n -m 8 -w 1 {ProbeHost}", 10, cancellationToken);
    }

    private static async Task<string> ReadPathMtuAsync(CancellationToken cancellationToken)
    {
        string output;
        if (OperatingSystem.IsWindows())
            output = await RunToolAsync("ping", "-n 1 -f -l 1472 1.1.1.1", 5, cancellationToken);
        else if (OperatingSystem.IsMacOS())
            output = await RunToolAsync("ping", "-c 1 -D -s 1472 1.1.1.1", 5, cancellationToken);
        else
            output = await RunToolAsync("ping", "-c 1 -M do -s 1472 1.1.1.1", 5, cancellationToken);

        var succeeded = output.Contains("1 received", StringComparison.OrdinalIgnoreCase) ||
                        output.Contains("Received = 1", StringComparison.OrdinalIgnoreCase);
        return succeeded
            ? "1500-byte IPv4 path probe succeeded without fragmentation"
            : output;
    }

    private static Task<string> ReadTcpStatusAsync(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsLinux())
            return RunToolAsync("ss", "-s", 8, cancellationToken);
        return RunToolAsync("netstat", "-s", 8, cancellationToken);
    }

    private static async Task<string> RunToolAsync(
        string fileName,
        string arguments,
        int maximumLines,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var lines = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeToolLine)
                .Where(line => line.Length > 0)
                .ToArray();
            return lines.Length == 0
                ? $"{fileName} returned no usable data"
                : string.Join(" · ", lines.Take(maximumLines));
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return $"{fileName} is not installed or unavailable";
        }
    }

    private static string FamilyName(AddressFamily family) =>
        family == AddressFamily.InterNetwork ? "IPv4" : "IPv6";

    private static string NormalizeToolLine(string line) =>
        string.Join(" ", line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
