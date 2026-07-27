using Spectre.Console;
using Spectre.Console.Rendering;

namespace Lag;

internal static class SpectreRenderer
{
    private const string Cyan = "#00E5FF";
    private const string CyanDim = "#176B87";
    private const string Magenta = "#FF2E88";
    private const string MagentaDim = "#7A1F5C";
    private const string Amber = "#FFD166";
    private const string Mint = "#35F2A1";
    private const string Ink = "#D8F7FF";

    public static async Task<NetworkReport> RunAnimatedAsync(
        NetworkQualityTester tester,
        bool includeBandwidth,
        bool includeNetOps,
        CancellationToken cancellationToken)
    {
        var updates = Channel.CreateUnbounded<TestProgress>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var latest = new TestProgress(0, "Igniting the signal reactor", "preparing network probes");
        var telemetry = new LiveTelemetry();
        var latencyHistory = new List<double>();
        double? lastLatency = null;
        var started = Stopwatch.StartNew();
        var frame = 0;
        var testTask = Task.Run(async () =>
        {
            try
            {
                return await tester.RunAsync(
                    includeBandwidth,
                    update => updates.Writer.TryWrite(update),
                    cancellationToken,
                    includeNetOps);
            }
            finally
            {
                updates.Writer.TryComplete();
            }
        }, cancellationToken);

        await AnsiConsole.Live(BuildReactor(latest, telemetry, latencyHistory, frame, started.Elapsed, includeBandwidth, includeNetOps))
            .AutoClear(true)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async context =>
            {
                while (!testTask.IsCompleted)
                {
                    while (updates.Reader.TryRead(out var update))
                    {
                        latest = update;
                        telemetry = telemetry.Merge(update.Telemetry);
                        if (telemetry.LatencyMs is { } latency && latency != lastLatency)
                        {
                            latencyHistory.Add(latency);
                            if (latencyHistory.Count > 48)
                                latencyHistory.RemoveAt(0);
                            lastLatency = latency;
                        }
                    }

                    context.UpdateTarget(BuildReactor(latest, telemetry, latencyHistory, frame++, started.Elapsed, includeBandwidth, includeNetOps));
                    await Task.Delay(67, cancellationToken);
                }
            });
        return await testTask;
    }

    private static IRenderable BuildReactor(
        TestProgress progress,
        LiveTelemetry telemetry,
        IReadOnlyList<double> latencyHistory,
        int frame,
        TimeSpan elapsed,
        bool includeBandwidth,
        bool includeNetOps)
    {
        var scope = BuildNetworkScope(progress.Stage, telemetry, latencyHistory, frame);
        var stageNames = includeBandwidth
            ? includeNetOps
                ? new[] { "WAKE", "PING", "PULSE", "PULL", "PUSH", "DECODE", "OPS" }
                : new[] { "WAKE", "PING", "PULSE", "PULL", "PUSH", "DECODE" }
            : includeNetOps
                ? new[] { "WAKE", "PING", "PULSE", "DECODE", "OPS" }
                : new[] { "WAKE", "PING", "PULSE", "DECODE" };
        var mappedStage = includeBandwidth
            ? progress.Stage
            : includeNetOps && progress.Stage >= 6
                ? 4
                : progress.Stage >= 5 ? 3 : Math.Min(progress.Stage, 2);
        var stages = BuildStageTrack(stageNames, mappedStage, frame);
        var liveStrip = BuildTelemetryStrip(telemetry, frame);
        var phaseColor = progress.Stage is 3 or 4 ? Magenta : Cyan;

        var rows = new Rows(
            new Markup(
                $"[bold {Cyan}]LAG[/]  [bold white]// PACKET INTERCEPT ENGINE[/]                " +
                $"[grey58]T+{elapsed:mm\\:ss\\.f}[/]"),
            scope,
            new Text(""),
            new Markup(stages),
            new Text(""),
            liveStrip,
            new Text(""),
            new Markup(
                $"[black on {phaseColor}] {Markup.Escape(progress.Phase.ToUpperInvariant())} [/] " +
                $"[white]{Markup.Escape(progress.Detail)}[/]"),
            new Markup("[grey46]LOCAL ONLY  ◈  GENERATED TRAFFIC  ◈  NO RECORDING  ◈  NO REPORT UPLOAD[/]")
        );

        return new Panel(rows)
            .Header($"[bold {phaseColor}] ◢ LAG // INTERCEPT ACTIVE ◣ [/]")
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.FromHex(CyanDim))
            .Padding(2, 1)
            .Expand();
    }

    private static IRenderable BuildNetworkScope(
        int stage,
        LiveTelemetry telemetry,
        IReadOnlyList<double> latencyHistory,
        int frame)
    {
        var pathWidth = 43;
        var outward = stage != 4;
        var position = frame % pathWidth;
        if (!outward)
            position = pathWidth - 1 - position;
        var outbound = Enumerable.Repeat('━', pathWidth).ToArray();
        outbound[position] = '◆';
        var direction = outward ? "TX →" : "← RX";
        var protocol = stage switch
        {
            0 => "DISCOVERY",
            1 => "DNS / TCP / TLS",
            2 => "HTTPS PROBES",
            3 => "DOWNLOAD + LOADED PROBES",
            4 => "UPLOAD STREAM",
            _ => "LOCAL ANALYSIS"
        };

        var route = new Markup(
            $"[bold {Ink}]DEVICE[/] [{Cyan}]◉[/] " +
            $"[{CyanDim}]{new string(outbound)}[/] [{Amber}]⬡[/] [bold {Ink}]EDGE[/]\n" +
            $"[grey46]                         {direction}  {protocol}[/]");

        var trace = BuildLatencyTrace(latencyHistory, 49, frame);
        var carrier = GradientText(BuildWaveform(frame, 49), frame * 9 + 80);
        var latest = telemetry.LatencyMs is null ? "waiting for first probe" : $"{telemetry.LatencyMs:0} ms latest";
        var scope = new Panel(new Rows(
                new Markup(carrier),
                new Markup($"[grey35]{new string('─', 49)}[/]"),
                new Markup(trace),
                new Markup(
                    $"[grey46]PROBE CARRIER ↑   MEASURED LATENCY ↓   " +
                    $"{Markup.Escape(latest)} · {latencyHistory.Count} sample(s)[/]")))
            .Header($"[bold {Magenta}] LIVE LATENCY OSCILLOSCOPE [/]")
            .Border(BoxBorder.Square)
            .BorderColor(Color.FromHex(MagentaDim))
            .Padding(1, 0)
            .Expand();

        var transfer = new Grid().Expand().AddColumn().AddColumn().AddColumn();
        transfer.AddRow(
            Meter("DOWNLOAD", telemetry.DownloadMbps, 100, Cyan),
            Meter("UPLOAD", telemetry.UploadMbps, 50, Magenta),
            Meter("LOAD LATENCY", telemetry.LoadedLatencyMs, 250, Amber, "ms"));

        return new Rows(route, new Text(""), scope, transfer);
    }

    private static string BuildWaveform(int frame, int width)
    {
        const string levels = "▁▂▃▄▅▆▇█";
        var output = new char[width];
        for (var index = 0; index < width; index++)
        {
            var first = Math.Sin((index + frame * .8) * .43);
            var second = Math.Sin((index - frame * .35) * .17) * .35;
            var pulse = Math.Exp(-Math.Pow((index - (frame * 2 % width)) / 3d, 2)) * .8;
            var value = Math.Clamp((first + second + pulse + 1.35) / 3.1, 0, .999);
            output[index] = levels[(int)(value * levels.Length)];
        }
        return new string(output);
    }

    private static string GradientText(string text, int phase)
    {
        var output = new System.Text.StringBuilder(text.Length * 22);
        for (var index = 0; index < text.Length; index++)
        {
            var hue = (phase + index * 7) % 360;
            var color = HsvToRgb(hue, .78, .96);
            output.Append($"[#{color.R:X2}{color.G:X2}{color.B:X2}]{Markup.Escape(text[index].ToString())}[/]");
        }
        return output.ToString();
    }

    private static (byte R, byte G, byte B) HsvToRgb(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var match = value - chroma;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return ((byte)((red + match) * 255), (byte)((green + match) * 255), (byte)((blue + match) * 255));
    }

    private static string BuildLatencyTrace(IReadOnlyList<double> samples, int width, int frame)
    {
        const string levels = "▁▂▃▄▅▆▇█";
        if (samples.Count == 0)
        {
            var scan = Enumerable.Repeat('─', width).ToArray();
            scan[frame % width] = '╋';
            return $"[{CyanDim}]{new string(scan)}[/]";
        }

        var visible = samples.TakeLast(width).ToArray();
        var ceiling = Math.Max(100, visible.Max());
        var trace = new System.Text.StringBuilder();
        trace.Append($"[{CyanDim}]{new string('·', width - visible.Length)}[/]");
        foreach (var sample in visible)
        {
            var index = (int)Math.Clamp(sample / ceiling * (levels.Length - 1), 0, levels.Length - 1);
            var color = sample <= 80 ? Cyan : sample <= 150 ? Amber : Magenta;
            trace.Append($"[{color}]{levels[index]}[/]");
        }
        return trace.ToString();
    }

    private static Panel Meter(string label, double? value, double ceiling, string color, string unit = "Mbps")
    {
        const int width = 14;
        var amount = value is null ? 0 : Math.Clamp(value.Value / ceiling, 0, 1);
        var filled = (int)Math.Round(amount * width);
        var bar = $"[{color}]{new string('━', filled)}[/][grey23]{new string('━', width - filled)}[/]";
        var reading = value is null ? "—" : $"{value:0.0} {unit}";
        return new Panel(new Markup($"{bar}\n[bold {color}]{reading}[/]"))
            .Header($"[grey58] {label} [/]")
            .Border(BoxBorder.Square)
            .BorderColor(Color.FromHex(CyanDim))
            .Padding(1, 0);
    }

    private static string BuildStageTrack(string[] names, int active, int frame)
    {
        var parts = new List<string>();
        for (var index = 0; index < names.Length; index++)
        {
            var color = index == active ? Magenta : Cyan;
            var node = index < active ? "●" : index == active ? (frame % 8 < 4 ? "◉" : "◎") : "○";
            var tone = index <= active ? color : "grey30";
            parts.Add($"[bold {tone}]{node} {names[index]}[/]");
            if (index < names.Length - 1)
            {
                var link = index < active ? "━━━" : index == active ? MovingLink(frame) : "╌╌╌";
                parts.Add($"[{tone}]{link}[/]");
            }
        }
        return string.Join(" ", parts);
    }

    private static string MovingLink(int frame)
    {
        var position = frame % 3;
        var chars = "━━━".ToCharArray();
        chars[position] = '◆';
        return new string(chars);
    }

    private static Grid BuildTelemetryStrip(LiveTelemetry telemetry, int frame)
    {
        var grid = new Grid().Expand();
        for (var index = 0; index < 6; index++)
            grid.AddColumn();
        grid.AddRow(
            Telemetry("PING", FormatMs(telemetry.LatencyMs), 0, frame),
            Telemetry("JITTER", FormatMs(telemetry.JitterMs), 1, frame),
            Telemetry("LOSS", FormatPercent(telemetry.ProbeLossPercent), 2, frame),
            Telemetry("DOWN", FormatRate(telemetry.DownloadMbps), 3, frame),
            Telemetry("UP", FormatRate(telemetry.UploadMbps), 4, frame),
            Telemetry("LOADED", FormatMs(telemetry.LoadedLatencyMs), 5, frame));
        grid.AddRow(
            new Markup($"[grey46]#{telemetry.SamplesCompleted ?? 0} samples[/]"),
            new Markup("[grey46]variation[/]"),
            new Markup("[grey46]HTTPS[/]"),
            new Markup($"[grey46]{FormatBytes(telemetry.DownloadBytes)}[/]"),
            new Markup($"[grey46]{FormatBytes(telemetry.UploadBytes)}[/]"),
            new Markup("[grey46]response[/]"));
        return grid;
    }

    private static Markup Telemetry(string label, string value, int index, int frame)
    {
        var colors = new[] { Cyan, Magenta, Amber, Cyan, Mint, Magenta };
        var color = colors[index];
        return new Markup($"[grey58]{label}[/]\n[bold {color}]{Markup.Escape(value)}[/]");
    }

    public static void Render(NetworkReport report)
    {
        var verdictColor = report.Verdict switch
        {
            Verdict.Excellent => Mint,
            Verdict.Good => Cyan,
            Verdict.Unstable => Amber,
            _ => Magenta
        };

        RenderResultMasthead(report, verdictColor);
        AnsiConsole.WriteLine();
        RenderConnectionCard(report);
        AnsiConsole.WriteLine();
        RenderMetricSpectrum(report);
        if (report.NetOps is not null)
        {
            AnsiConsole.WriteLine();
            RenderNetOps(report.NetOps);
        }
        AnsiConsole.WriteLine();
        RenderDiagnosis(report, verdictColor);
        AnsiConsole.WriteLine();
        RenderActionDeck(report);

        foreach (var note in report.Notes)
            AnsiConsole.MarkupLine($"\n[grey46]  ◇ {Markup.Escape(note)}[/]");
        AnsiConsole.MarkupLine("\n[grey35]  LAG // LOCAL NETWORK INTELLIGENCE[/]");
    }

    private static void RenderResultMasthead(NetworkReport report, string verdictColor)
    {
        var score = new FigletText(report.Score.ToString("00"))
            .Color(Color.FromHex(verdictColor));
        var scoreBlock = new Rows(
            new Align(score, HorizontalAlignment.Center),
            new Align(new Markup($"[bold {verdictColor}]{report.Verdict.ToString().ToUpperInvariant()}[/] [grey50]/ 100[/]"), HorizontalAlignment.Center));

        var identity = new Rows(
            new Markup($"[bold {Cyan}]LAG[/]  [bold {Ink}]NETWORK LATENCY ANALYZER[/]"),
            new Text(""),
            SpeedReadout("DOWNLOAD", "receiving capacity", report.Metrics.DownloadMbps, 100, Cyan),
            new Text(""),
            SpeedReadout("UPLOAD", "sending capacity", report.Metrics.UploadMbps, 50, Magenta),
            new Text(""),
            new Align(new Markup($"[bold {verdictColor}]{Markup.Escape(report.Diagnosis)}[/]"), HorizontalAlignment.Center));

        var grid = new Grid().Expand()
            .AddColumn(new GridColumn().Width(25))
            .AddColumn();
        grid.AddRow(scoreBlock, identity);
        AnsiConsole.Write(new Panel(grid)
            .Header($"[bold {verdictColor}] ✦ YOUR SIGNAL, DECODED ✦ [/]")
            .Border(BoxBorder.Double)
            .BorderColor(Color.FromHex(verdictColor))
            .Padding(2, 1));
    }

    private static IRenderable SpeedReadout(
        string label,
        string meaning,
        double value,
        double ceiling,
        string color)
    {
        var reading = value > 0 ? $"{value:0.0}" : "—";
        const int width = 28;
        var ratio = value <= 0 ? 0 : Math.Clamp(value / ceiling, 0, 1);
        var filled = (int)Math.Round(ratio * width);
        var marker = Math.Clamp(filled, 0, width - 1);
        var rail =
            $"[{color}]{new string('━', marker)}◆[/]" +
            $"[grey23]{new string('━', width - marker - 1)}[/]";
        return new Rows(
            new Markup(
                $"[bold {color}]{label}[/]  [bold white]{reading}[/] [grey58]Mbps[/]  " +
                $"[grey46]{meaning}[/]"),
            new Markup(rail));
    }

    private static void RenderConnectionCard(NetworkReport report)
    {
        var connection = report.Connection;
        var vpnState = connection.VpnDetected
            ? $"[bold {Amber}]ACTIVE[/] [grey58]· {Markup.Escape(connection.VpnInterface ?? "VPN interface")}[/]"
            : $"[bold {Mint}]NOT DETECTED[/]";
        var proxyState = connection.ProxyDetected
            ? $"[bold {Amber}]ACTIVE[/] [grey58]· {Markup.Escape(connection.ProxySource ?? "environment")}[/]"
            : $"[bold {Mint}]NOT DETECTED[/]";
        var details = new Grid().Expand()
            .AddColumn(new GridColumn().Width(13))
            .AddColumn();
        details.AddRow("[grey58]LINK[/]", $"[bold white]{Markup.Escape(connection.Type)}[/]");
        details.AddRow("[grey58]INTERFACE[/]", $"[bold white]{Markup.Escape(connection.Interface ?? "active")}[/]");
        details.AddRow("[grey58]VPN[/]", vpnState);
        details.AddRow("[grey58]PROXY[/]", proxyState);

        AnsiConsole.Write(new Panel(details)
            .Header($"[bold {Cyan}] CONNECTION PROFILE [/]")
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.FromHex(CyanDim))
            .Padding(2, 0));
    }

    private static void RenderMetricSpectrum(NetworkReport report)
    {
        var metrics = new List<(string Name, string Value, string Meaning, double Quality)>
        {
            ("LATENCY", $"{report.Metrics.LatencyMs:0} ms", "reaction time", 1 - report.Metrics.LatencyMs / 200d),
            ("JITTER", $"{report.Metrics.JitterMs:0} ms", "consistency", 1 - report.Metrics.JitterMs / 80d),
            ("STABILITY", $"{report.Metrics.ProbeLossPercent:0.0}%", "failed probes", 1 - report.Metrics.ProbeLossPercent / 10d),
            ("DNS", $"{report.Metrics.DnsMs:0} ms", "lookup time", 1 - report.Metrics.DnsMs / 300d)
        };
        if (report.Metrics.DownloadMbps > 0)
            metrics.Add(("DOWNLOAD", $"{report.Metrics.DownloadMbps:0.0} Mbps", "receiving", report.Metrics.DownloadMbps / 100d));
        if (report.Metrics.UploadMbps > 0)
            metrics.Add(("UPLOAD", $"{report.Metrics.UploadMbps:0.0} Mbps", "sending", report.Metrics.UploadMbps / 40d));
        if (report.Metrics.LoadedLatencyMs > 0)
            metrics.Add(("UNDER LOAD", $"{report.Metrics.LoadedLatencyMs:0} ms", $"+{report.Metrics.BufferbloatMs:0} ms", 1 - report.Metrics.BufferbloatMs / 250d));

        var table = new Table()
            .Border(TableBorder.None)
            .Expand()
            .HideHeaders()
            .AddColumn(new TableColumn("").Width(14))
            .AddColumn(new TableColumn("").Width(18))
            .AddColumn("")
            .AddColumn(new TableColumn("").RightAligned());

        for (var index = 0; index < metrics.Count; index++)
        {
            var metric = metrics[index];
            var quality = Math.Clamp(metric.Quality, 0, 1);
            var width = 24;
            var lit = (int)Math.Round(quality * width);
            var color = quality >= .7 ? Cyan : quality >= .4 ? Amber : Magenta;
            var spectrum = SignalBar(lit, width, color);
            var status = quality >= .7 ? $"[{Mint}]● CLEAN[/]" : quality >= .4 ? $"[{Amber}]◆ MIXED[/]" : $"[{Magenta}]▲ ROUGH[/]";
            table.AddRow(
                $"[bold {color}]{metric.Name}[/]",
                $"[bold white]{metric.Value}[/]\n[grey46]{metric.Meaning}[/]",
                spectrum,
                status);
        }

        AnsiConsole.Write(new Panel(table)
            .Header($"[bold {Cyan}] SIGNAL INTEGRITY MATRIX [/]")
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.FromHex(CyanDim))
            .Padding(1, 0));
    }

    private static void RenderNetOps(NetOpsDiagnostics ops)
    {
        var interfaceTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").Width(17))
            .AddColumn("");
        interfaceTable.AddRow("[grey58]DEFAULT GATEWAY[/]", Value(ops.Gateway));
        interfaceTable.AddRow("[grey58]GATEWAY RTT[/]", Milliseconds(ops.GatewayLatencyMs));
        interfaceTable.AddRow("[grey58]LOCAL ADDRESS[/]", ValueList(ops.LocalAddresses));
        interfaceTable.AddRow("[grey58]DNS SERVERS[/]", ValueList(ops.DnsServers));
        interfaceTable.AddRow("[grey58]INTERFACE MTU[/]", ops.Mtu?.ToString() ?? "[grey46]unavailable[/]");
        interfaceTable.AddRow("[grey58]LINK RATE[/]", ops.LinkSpeedMbps is { } speed ? $"[white]{speed:N0} Mbps[/]" : "[grey46]unavailable[/]");
        interfaceTable.AddRow(
            "[grey58]ERRORS / DROPS[/]",
            $"[white]RX {ops.ReceivedErrors ?? 0:N0}/{ops.ReceivedDrops ?? 0:N0} · " +
            $"TX {ops.SentErrors ?? 0:N0}/{ops.SentDrops ?? 0:N0}[/]");

        var timingTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").Width(15))
            .AddColumn(new TableColumn("").RightAligned());
        timingTable.AddRow("[grey58]DNS LOOKUP[/]", Milliseconds(ops.Timing.DnsMs));
        timingTable.AddRow("[grey58]TCP CONNECT[/]", Milliseconds(ops.Timing.TcpMs));
        timingTable.AddRow("[grey58]TLS HANDSHAKE[/]", Milliseconds(ops.Timing.TlsMs));
        timingTable.AddRow("[grey58]FIRST BYTE[/]", Milliseconds(ops.Timing.FirstByteMs));
        timingTable.AddRow("[grey58]IPv4[/]", Family(ops.IPv4));
        timingTable.AddRow("[grey58]IPv6[/]", Family(ops.IPv6));

        var top = new Grid().Expand().AddColumn().AddColumn();
        top.AddRow(
            new Panel(interfaceTable)
                .Header($"[bold {Cyan}] LOCAL / LINK DOMAIN [/]")
                .Border(BoxBorder.Heavy)
                .BorderColor(Color.FromHex(CyanDim)),
            new Panel(timingTable)
                .Header($"[bold {Magenta}] STACK / HANDSHAKE [/]")
                .Border(BoxBorder.Heavy)
                .BorderColor(Color.FromHex(MagentaDim)));
        AnsiConsole.Write(new Rule($"[bold {Amber}] NETOPS FAULT-DOMAIN REPORT [/]") { Style = new Style(Color.FromHex(Amber)) });
        AnsiConsole.Write(top);

        var endpoints = new Table()
            .Border(TableBorder.Simple)
            .Expand()
            .AddColumn("Endpoint")
            .AddColumn("Address")
            .AddColumn(new TableColumn("TCP/443").RightAligned())
            .AddColumn("State");
        foreach (var endpoint in ops.Endpoints)
            endpoints.AddRow(
                Markup.Escape(endpoint.Host),
                Value(endpoint.Address),
                Milliseconds(endpoint.ConnectMs),
                endpoint.Reachable ? $"[{Mint}]REACHABLE[/]" : $"[{Magenta}]FAILED / FILTERED[/]");
        AnsiConsole.Write(endpoints);

        RenderWifiRadio(ops.Wifi);

        var environment = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").Width(19))
            .AddColumn("");
        environment.AddRow("[grey58]ROUTE / HOPS[/]", $"[white]{Markup.Escape(ops.RouteSummary)}[/]");
        environment.AddRow("[grey58]PATH MTU[/]", $"[grey70]{Markup.Escape(ops.MtuStatus)}[/]");
        environment.AddRow("[grey58]TCP SOCKET SUMMARY[/]", $"[grey70]{Markup.Escape(ops.TcpStatus)}[/]");
        environment.AddRow("[grey58]HTTPS / PORTAL[/]", $"[white]{Markup.Escape(ops.CaptivePortalStatus)}[/]");
        environment.AddRow("[grey58]TLS ISSUER[/]", Value(ops.CertificateIssuer));
        environment.AddRow("[grey58]BASELINE[/]", "[grey46]not configured · save JSON and compare repeated runs[/]");
        AnsiConsole.Write(new Panel(environment)
            .Header($"[bold {Amber}] PATH / ENVIRONMENT [/]")
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.FromHex(Amber))
            .Padding(1, 0));
    }

    private static void RenderWifiRadio(WifiRadioDiagnostic wifi)
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").Width(12))
            .AddColumn("")
            .AddColumn(new TableColumn("").Width(12))
            .AddColumn("");

        if (!wifi.Available)
        {
            table.AddRow("[grey58]STATUS[/]", Value(wifi.Status), "", "");
        }
        else
        {
            table.AddRow("[grey58]SSID[/]", Value(wifi.Ssid), "[grey58]BSSID[/]", Value(wifi.Bssid));
            table.AddRow("[grey58]BAND[/]", Value(wifi.Band), "[grey58]CHANNEL[/]", Value(wifi.Channel));
            table.AddRow("[grey58]FREQUENCY[/]", Value(wifi.Frequency), "[grey58]SIGNAL[/]", Value(wifi.Signal));
            table.AddRow("[grey58]RX LINK[/]", Value(wifi.ReceiveRate), "[grey58]TX LINK[/]", Value(wifi.TransmitRate));
            table.AddRow("[grey58]RX TRAFFIC[/]", Value(wifi.ReceivedTraffic), "[grey58]TX TRAFFIC[/]", Value(wifi.TransmittedTraffic));
        }

        AnsiConsole.Write(new Panel(table)
            .Header($"[bold {Cyan}] WI-FI RADIO [/]")
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.FromHex(CyanDim))
            .Padding(1, 0));
    }

    private static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "[grey46]unavailable[/]" : $"[white]{Markup.Escape(value)}[/]";

    private static string ValueList(IReadOnlyList<string> values) =>
        values.Count == 0
            ? "[grey46]unavailable[/]"
            : $"[white]{Markup.Escape(string.Join(", ", values))}[/]";

    private static string Milliseconds(double? value) =>
        value is null ? "[grey46]unavailable[/]" : $"[white]{value:0.0} ms[/]";

    private static string Family(AddressFamilyDiagnostic family) =>
        family.Reachable
            ? $"[{Mint}]UP[/] [grey70]{family.ConnectMs:0.0} ms · {Markup.Escape(family.Address ?? "")}[/]"
            : $"[{(family.Available ? Amber : "grey46")}]{Markup.Escape(family.Status)}[/]";

    private static string SignalBar(int lit, int width, string color)
    {
        var output = new System.Text.StringBuilder();
        for (var index = 0; index < width; index++)
        {
            if (index < lit)
                output.Append($"[{color}]▰[/]");
            else
                output.Append("[grey19]▱[/]");
        }
        return output.ToString();
    }

    private static void RenderDiagnosis(NetworkReport report, string verdictColor)
    {
        var symptoms = report.LikelySymptoms.Count == 0
            ? $"[{Mint}]● No obvious user-facing symptoms detected.[/]"
            : string.Join("\n", report.LikelySymptoms.Select((item, index) =>
                $"[{Magenta}]◆[/] [white]{Markup.Escape(item)}[/]"));
        var panel = new Panel(new Markup(symptoms))
            .Header($"[bold {verdictColor}] WHAT YOU MAY FEEL [/]")
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.FromHex(verdictColor))
            .Padding(2, 1);
        AnsiConsole.Write(panel);
    }

    private static void RenderActionDeck(NetworkReport report)
    {
        var grid = new Grid().Expand();
        var count = Math.Max(1, report.TryFirst.Count);
        for (var index = 0; index < count; index++)
            grid.AddColumn();

        var row = report.TryFirst.Select((item, index) =>
        {
            var color = index % 2 == 0 ? Cyan : Magenta;
            return (IRenderable)new Panel(
                    new Align(new Markup($"[bold white]{Markup.Escape(item)}[/]"), HorizontalAlignment.Center))
                .Header($"[bold {color}] 0{index + 1} / TRY THIS [/]")
                .Border(BoxBorder.Double)
                .BorderColor(Color.FromHex(color))
                .Padding(1, 1);
        }).ToArray();

        if (row.Length > 0)
            grid.AddRow(row);
        AnsiConsole.Write(grid);
    }

    private static string FormatMs(double? value) => value is null ? "—" : $"{value:0} ms";
    private static string FormatRate(double? value) => value is null ? "—" : $"{value:0.0} Mbps";
    private static string FormatPercent(double? value) => value is null ? "—" : $"{value:0.0}%";
    private static string FormatBytes(long? value) => value switch
    {
        null => "waiting",
        < 1_000_000 => $"{value / 1_000d:0} KB",
        _ => $"{value / 1_000_000d:0.0} MB"
    };
}
