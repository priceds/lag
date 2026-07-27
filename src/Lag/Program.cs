using Spectre.Console;

namespace Lag;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var json = args.Contains("--json");
        var quick = args.Contains("--quick");
        var noColor = args.Contains("--no-color");
        var forceColor = args.Contains("--force-color");
        var version = args.Contains("--version");
        var help = args.Contains("--help") || args.Contains("-h");
        var known = new HashSet<string> { "--json", "--quick", "--no-color", "--force-color", "--version", "--help", "-h" };

        if (args.Any(argument => !known.Contains(argument)))
        {
            Console.Error.WriteLine($"lag: unknown option: {args.First(argument => !known.Contains(argument))}");
            return 2;
        }
        if (help)
        {
            Console.WriteLine("lag — explain why the internet feels slow");
            Console.WriteLine("\nUsage: lag [--quick] [--json] [--no-color] [--force-color] [--version]");
            return 0;
        }
        if (version)
        {
            Console.WriteLine("lag 1.0.0 (.NET 10 · Spectre.Console)");
            return 0;
        }
        if (noColor)
            AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
        else if (forceColor)
            AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.TrueColor;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        using var tester = new NetworkQualityTester();
        try
        {
            NetworkReport report;
            if (!json && !Console.IsOutputRedirected)
                report = await SpectreRenderer.RunAnimatedAsync(tester, !quick, cancellation.Token);
            else
                report = await tester.RunAsync(!quick, null, cancellation.Token);

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                }));
            }
            else
            {
                SpectreRenderer.Render(report);
            }
            return report.Verdict == Verdict.Offline ? 1 : 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("lag: test timed out after 35 seconds");
            return 1;
        }
    }
}
