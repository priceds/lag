using Spectre.Console;
using Spectre.Console.Rendering;

namespace Lag;

internal static class SpectreRenderer
{
    public static async Task<NetworkReport> RunAnimatedAsync(
        NetworkQualityTester tester,
        bool includeBandwidth,
        CancellationToken cancellationToken)
    {
        var updates = Channel.CreateUnbounded<TestProgress>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var latest = new TestProgress(0, "Powering the instruments", "preparing network probes");
        var telemetry = new LiveTelemetry();
        var started = Stopwatch.StartNew();
        var frame = 0;
        var testTask = Task.Run(async () =>
        {
            try
            {
                return await tester.RunAsync(
                    includeBandwidth,
                    update => updates.Writer.TryWrite(update),
                    cancellationToken);
            }
            finally
            {
                updates.Writer.TryComplete();
            }
        }, cancellationToken);

        await AnsiConsole.Live(BuildFlightDeck(latest, telemetry, frame, started.Elapsed, includeBandwidth))
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
                    }
                    context.UpdateTarget(BuildFlightDeck(latest, telemetry, frame++, started.Elapsed, includeBandwidth));
                    await Task.Delay(80, cancellationToken);
                }
            });
        return await testTask;
    }

    private static IRenderable BuildFlightDeck(
        TestProgress progress,
        LiveTelemetry telemetry,
        int frame,
        TimeSpan elapsed,
        bool includeBandwidth)
    {
        var routeWidth = 35;
        var cycle = (routeWidth - 1) * 2;
        var rawPosition = frame % cycle;
        var packet = rawPosition < routeWidth ? rawPosition : cycle - rawPosition;
        var route = new char[routeWidth];
        Array.Fill(route, '━');
        route[packet] = '●';
        var routeText = GradientText(new string(route), frame * 5);
        var packetColor = rawPosition < routeWidth ? "#22d3ee" : "#4ade80";

        var waveform = GradientText(BuildWaveform(frame, 49), frame * 9 + 80);
        var stages = includeBandwidth
            ? new[] { "LINK", "ROUTE", "RESPONSE", "LOAD", "RETURN", "ANALYZE" }
            : new[] { "LINK", "ROUTE", "RESPONSE", "ANALYZE" };
        var mappedStage = includeBandwidth ? progress.Stage : progress.Stage >= 5 ? 3 : Math.Min(progress.Stage, 2);
        var stageLine = string.Join("  ", stages.Select((stage, index) => index switch
        {
            var current when current < mappedStage => $"[springgreen2]◆ {stage}[/]",
            var current when current == mappedStage => $"[bold deepskyblue1]◈ {stage}[/]",
            _ => $"[grey35]◇ {stage}[/]"
        }));

        var topology =
            $"[bold #f8fafc]DEVICE[/] [#22d3ee]◉[/] {routeText} " +
            $"[{packetColor}]◆[/] [bold #f8fafc]ROUTER[/] [#475569]━━━━━━[/] " +
            "[#4ade80]⬡[/] [bold #f8fafc]EDGE[/]";
        var phase = $"[black on deepskyblue1] {Markup.Escape(progress.Phase.ToUpperInvariant())} [/]  " +
                    $"[grey]{Markup.Escape(progress.Detail)}[/]";
        var timer = $"[grey53]T+ {elapsed:mm\\:ss\\.f}[/]";

        var counters = BuildLiveCounters(telemetry, frame);
        var rows = new Rows(
            new Markup(GradientText("◢ NETWORK FLIGHT DECK ◣", frame * 7)),
            new Text(""),
            new Markup(topology),
            new Markup($"            {waveform}"),
            new Markup("[#64748b]            LIVE PROBE PULSE · TRUE-COLOR TELEMETRY[/]"),
            new Text(""),
            new Markup(stageLine),
            new Text(""),
            counters,
            new Text(""),
            new Markup($"{phase}    {timer}"),
            new Markup("[#64748b]Local analysis · generated test traffic · no report upload[/]")
        );
        return new Panel(rows)
            .Header("[bold deepskyblue1] lag telemetry [/]")
            .Border(BoxBorder.Double)
            .BorderColor(Color.DeepSkyBlue1)
            .Padding(2, 1)
            .Expand();
    }

    private static Grid BuildLiveCounters(LiveTelemetry telemetry, int frame)
    {
        var grid = new Grid().Expand();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow(
            Counter("LATENCY", FormatMs(telemetry.LatencyMs), "#38bdf8", $"probe #{telemetry.SamplesCompleted ?? 0}"),
            Counter("JITTER", FormatMs(telemetry.JitterMs), "#a78bfa", "variation"),
            Counter("PROBE LOSS", FormatPercent(telemetry.ProbeLossPercent), "#facc15", "HTTPS stability"));
        grid.AddRow(
            Counter("DOWNLOAD", FormatRate(telemetry.DownloadMbps), "#22d3ee", FormatBytes(telemetry.DownloadBytes)),
            Counter("UPLOAD", FormatRate(telemetry.UploadMbps), "#4ade80", FormatBytes(telemetry.UploadBytes)),
            Counter("UNDER LOAD", FormatMs(telemetry.LoadedLatencyMs), PulseColor(frame), "responsiveness"));
        return grid;
    }

    private static Panel Counter(string name, string value, string color, string footer)
    {
        var content = new Rows(
            new Align(new Markup($"[bold {color}]{Markup.Escape(value)}[/]"), HorizontalAlignment.Center),
            new Align(new Markup($"[#64748b]{Markup.Escape(footer)}[/]"), HorizontalAlignment.Center));
        return new Panel(content)
            .Header($"[bold #cbd5e1] {name} [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.FromHex(color))
            .Padding(1, 0);
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

    private static string PulseColor(int frame)
    {
        var strength = (Math.Sin(frame * .18) + 1) / 2;
        var red = (byte)(56 + strength * 80);
        var green = (byte)(189 + strength * 40);
        return $"#{red:X2}{green:X2}F8";
    }

    private static string FormatMs(double? value) => value is null ? "—" : $"{value:0} ms";
    private static string FormatRate(double? value) => value is null ? "—" : $"{value:0.0} Mbps";
    private static string FormatPercent(double? value) => value is null ? "—" : $"{value:0.0}%";
    private static string FormatBytes(long? value) => value switch
    {
        null => "waiting",
        < 1_000_000 => $"{value / 1_000d:0} KB transferred",
        _ => $"{value / 1_000_000d:0.0} MB transferred"
    };

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

    public static void Render(NetworkReport report)
    {
        AnsiConsole.Write(new FigletText("lag").Color(Color.DeepSkyBlue1));
        AnsiConsole.MarkupLine("[grey]Your internet is fast. So why does it feel slow?[/]\n");

        var cards = new List<IRenderable>
        {
            ResultCard(report.Metrics.Reachable, "CONNECTION",
                $"{report.Connection.Type}\n[#94a3b8]{Markup.Escape(report.Connection.Interface ?? "active interface")}[/]",
                "Public edge reachable", "#22d3ee")
        };
        if (report.Metrics.Reachable)
        {
            cards.Add(ResultCard(report.Metrics.LatencyMs <= 80, "LATENCY",
                $"{report.Metrics.LatencyMs:0} ms", "Interaction delay", "#38bdf8"));
            cards.Add(ResultCard(report.Metrics.JitterMs <= 25, "JITTER",
                $"{report.Metrics.JitterMs:0} ms", "Timing variation", "#a78bfa"));
            cards.Add(ResultCard(report.Metrics.ProbeLossPercent < 1, "STABILITY",
                $"{report.Metrics.ProbeLossPercent:0.0}% loss", "Failed HTTPS probes", "#facc15"));
            cards.Add(ResultCard(report.Metrics.DnsMs <= 180, "DNS",
                $"{report.Metrics.DnsMs:0} ms", "Name lookup delay", "#fb7185"));
            if (report.Metrics.DownloadMbps > 0)
                cards.Add(ResultCard(report.Metrics.DownloadMbps >= 25, "DOWNLOAD",
                    $"{report.Metrics.DownloadMbps:0.0} Mbps", "Receiving capacity", "#22d3ee"));
            if (report.Metrics.UploadMbps > 0)
                cards.Add(ResultCard(report.Metrics.UploadMbps >= 8, "UPLOAD",
                    $"{report.Metrics.UploadMbps:0.0} Mbps", "Sending capacity", "#4ade80"));
            if (report.Metrics.LoadedLatencyMs > 0)
                cards.Add(ResultCard(report.Metrics.BufferbloatMs <= 80, "UNDER LOAD",
                    $"{report.Metrics.LoadedLatencyMs:0} ms\n[#94a3b8]+{report.Metrics.BufferbloatMs:0} ms[/]",
                    "Responsiveness while busy", "#2dd4bf"));
        }

        var resultGrid = new Grid().Expand();
        for (var index = 0; index < 4; index++)
            resultGrid.AddColumn();
        for (var index = 0; index < cards.Count; index += 4)
        {
            var row = new IRenderable[4];
            for (var column = 0; column < 4; column++)
                row[column] = index + column < cards.Count ? cards[index + column] : new Text("");
            resultGrid.AddRow(row);
        }
        AnsiConsole.Write(resultGrid);

        var tone = report.Verdict switch
        {
            Verdict.Excellent => "springgreen2",
            Verdict.Good => "green",
            Verdict.Unstable => "yellow",
            _ => "red"
        };
        var chart = new BreakdownChart()
            .Width(54)
            .AddItem("Quality", report.Score, Color.DeepSkyBlue1)
            .AddItem("Headroom", 100 - report.Score, Color.Grey23);
        var scorePanel = new Panel(new Rows(
                chart,
                new Align(new Markup($"[bold {tone}]{report.Score}/100 · {report.Verdict}[/]"), HorizontalAlignment.Center)))
            .Header("[bold #38bdf8] QUALITY [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DeepSkyBlue1)
            .Padding(1, 0);

        var diagnosis = new Panel(Markup.Escape(report.Diagnosis))
            .Header("[bold #a78bfa] DIAGNOSIS [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.MediumPurple2)
            .Padding(1, 0);
        var summary = new Grid().Expand().AddColumn().AddColumn();
        summary.AddRow(scorePanel, diagnosis);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(summary);

        var symptomText = report.LikelySymptoms.Count == 0
            ? "[#64748b]No obvious user-facing symptoms detected.[/]"
            : string.Join("\n", report.LikelySymptoms.Select(item => $"[#facc15]•[/] {Markup.Escape(item)}"));
        var actionText = string.Join("\n", report.TryFirst.Select(item => $"[#22d3ee]→[/] {Markup.Escape(item)}"));
        var guidance = new Grid().Expand().AddColumn().AddColumn();
        guidance.AddRow(
            new Panel(new Markup(symptomText))
                .Header("[bold #facc15] LIKELY SYMPTOMS [/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow),
            new Panel(new Markup(actionText))
                .Header("[bold #22d3ee] TRY FIRST [/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1));
        AnsiConsole.Write(guidance);

        foreach (var note in report.Notes)
            AnsiConsole.MarkupLine($"\n  [#64748b]Note: {Markup.Escape(note)}[/]");
        AnsiConsole.WriteLine();
    }

    private static Panel ResultCard(
        bool pass,
        string name,
        string value,
        string meaning,
        string accent)
    {
        var state = pass ? "[#4ade80]● GOOD[/]" : "[#facc15]▲ CHECK[/]";
        var content = new Rows(
            new Align(new Markup($"[bold {accent}]{value}[/]"), HorizontalAlignment.Center),
            new Align(new Markup($"[#94a3b8]{Markup.Escape(meaning)}[/]"), HorizontalAlignment.Center),
            new Align(new Markup(state), HorizontalAlignment.Center));
        return new Panel(content)
            .Header($"[bold #e2e8f0] {name} [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.FromHex(accent))
            .Padding(1, 0);
    }
}
