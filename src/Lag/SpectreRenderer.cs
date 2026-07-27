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
            SingleWriter = true
        });
        var latest = new TestProgress(0, "Powering the instruments", "preparing network probes");
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

        await AnsiConsole.Live(BuildFlightDeck(latest, frame, started.Elapsed, includeBandwidth))
            .AutoClear(true)
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async context =>
            {
                while (!testTask.IsCompleted)
                {
                    while (updates.Reader.TryRead(out var update))
                        latest = update;
                    context.UpdateTarget(BuildFlightDeck(latest, frame++, started.Elapsed, includeBandwidth));
                    await Task.Delay(80, cancellationToken);
                }
            });
        return await testTask;
    }

    private static IRenderable BuildFlightDeck(TestProgress progress, int frame, TimeSpan elapsed, bool includeBandwidth)
    {
        var routeWidth = 35;
        var cycle = (routeWidth - 1) * 2;
        var rawPosition = frame % cycle;
        var packet = rawPosition < routeWidth ? rawPosition : cycle - rawPosition;
        var route = new char[routeWidth];
        Array.Fill(route, '━');
        route[packet] = '●';
        var routeText = new string(route);
        var packetColor = rawPosition < routeWidth ? "deepskyblue1" : "springgreen2";

        var waveform = BuildWaveform(frame, 49);
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
            $"[bold white]DEVICE[/] [deepskyblue1]◉[/] [{packetColor}]{routeText}[/] " +
            "[mediumpurple2]◆[/] [bold white]ROUTER[/] [grey35]━━━━━━[/] " +
            "[springgreen2]⬡[/] [bold white]EDGE[/]";
        var phase = $"[black on deepskyblue1] {Markup.Escape(progress.Phase.ToUpperInvariant())} [/]  " +
                    $"[grey]{Markup.Escape(progress.Detail)}[/]";
        var timer = $"[grey53]T+ {elapsed:mm\\:ss\\.f}[/]";

        var rows = new Rows(
            new Markup("[bold deepskyblue1]◢ NETWORK FLIGHT DECK ◣[/]"),
            new Text(""),
            new Markup(topology),
            new Markup($"[grey35]            {waveform}[/]"),
            new Markup("[grey35]            LIVE PROBE PULSE[/]"),
            new Text(""),
            new Markup(stageLine),
            new Text(""),
            new Markup($"{phase}    {timer}"),
            new Markup("[grey35]Local analysis · generated test traffic · no report upload[/]")
        );
        return new Panel(rows)
            .Header("[bold deepskyblue1] lag telemetry [/]")
            .Border(BoxBorder.Double)
            .BorderColor(Color.DeepSkyBlue1)
            .Padding(2, 1)
            .Expand();
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

    public static void Render(NetworkReport report)
    {
        AnsiConsole.Write(new FigletText("lag").Color(Color.DeepSkyBlue1));
        AnsiConsole.MarkupLine("[grey]Your internet is fast. So why does it feel slow?[/]\n");

        var metrics = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey35)
            .AddColumn(new TableColumn("[bold]Signal[/]").Centered())
            .AddColumn(new TableColumn("[bold]Measurement[/]"))
            .AddColumn(new TableColumn("[bold]Result[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]What it means[/]"));

        AddMetric(metrics, report.Metrics.Reachable, "Connected",
            $"{report.Connection.Type} · {report.Connection.Interface ?? "active interface"}", "Public endpoint reachable");
        if (report.Metrics.Reachable)
        {
            AddMetric(metrics, report.Metrics.LatencyMs <= 80, "Latency", $"{report.Metrics.LatencyMs:0} ms", "Interaction delay");
            AddMetric(metrics, report.Metrics.JitterMs <= 25, "Jitter", $"{report.Metrics.JitterMs:0} ms", "Moment-to-moment variation");
            AddMetric(metrics, report.Metrics.ProbeLossPercent < 1, "Stability", $"{report.Metrics.ProbeLossPercent:0.0}% loss", "Failed HTTPS probes");
            AddMetric(metrics, report.Metrics.DnsMs <= 180, "DNS", $"{report.Metrics.DnsMs:0} ms", "Name lookup delay");
            if (report.Metrics.DownloadMbps > 0)
                AddMetric(metrics, report.Metrics.DownloadMbps >= 25, "Download", $"{report.Metrics.DownloadMbps:0.0} Mbps", "Receiving capacity");
            if (report.Metrics.UploadMbps > 0)
                AddMetric(metrics, report.Metrics.UploadMbps >= 8, "Upload", $"{report.Metrics.UploadMbps:0.0} Mbps", "Sending capacity");
            if (report.Metrics.LoadedLatencyMs > 0)
                AddMetric(metrics, report.Metrics.BufferbloatMs <= 80, "Under load",
                    $"{report.Metrics.LoadedLatencyMs:0} ms  ([grey]+{report.Metrics.BufferbloatMs:0}[/])", "Responsiveness while busy");
        }
        AnsiConsole.Write(metrics);

        var tone = report.Verdict switch
        {
            Verdict.Excellent => "springgreen2",
            Verdict.Good => "green",
            Verdict.Unstable => "yellow",
            _ => "red"
        };
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new BreakdownChart()
            .Width(54)
            .AddItem("Quality", report.Score, Color.DeepSkyBlue1)
            .AddItem("Headroom", 100 - report.Score, Color.Grey23));
        AnsiConsole.MarkupLine($"\n  [bold {tone}]{report.Score}/100 · {report.Verdict}[/]");

        var diagnosis = new Panel(Markup.Escape(report.Diagnosis))
            .Header("[bold] Diagnosis [/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DeepSkyBlue1)
            .Padding(1, 0);
        AnsiConsole.Write(diagnosis);

        if (report.LikelySymptoms.Count > 0)
        {
            AnsiConsole.Write(new Rule("[yellow]Likely symptoms[/]").LeftJustified());
            foreach (var symptom in report.LikelySymptoms)
                AnsiConsole.MarkupLine($"  [yellow]•[/] {Markup.Escape(symptom)}");
        }

        AnsiConsole.Write(new Rule("[deepskyblue1]Try first[/]").LeftJustified());
        foreach (var action in report.TryFirst)
            AnsiConsole.MarkupLine($"  [deepskyblue1]→[/] {Markup.Escape(action)}");
        foreach (var note in report.Notes)
            AnsiConsole.MarkupLine($"\n  [grey]Note: {Markup.Escape(note)}[/]");
        AnsiConsole.WriteLine();
    }

    private static void AddMetric(Table table, bool pass, string name, string value, string meaning)
    {
        var indicator = pass ? "[green]✓[/]" : "[yellow]![/]";
        table.AddRow(indicator, Markup.Escape(name), value, $"[grey]{Markup.Escape(meaning)}[/]");
    }
}
