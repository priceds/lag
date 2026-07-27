<div align="center">

# ⚡ lag

### Your internet is fast. So why does it feel slow?

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Spectre.Console](https://img.shields.io/badge/Spectre.Console-0.57.2-8A2BE2)](https://spectreconsole.net/)
[![CI](https://github.com/priceds/lag/actions/workflows/ci.yml/badge.svg)](https://github.com/priceds/lag/actions/workflows/ci.yml)
[![Platforms](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-06b6d4)](#platforms)
[![License](https://img.shields.io/badge/license-MIT-22c55e)](LICENSE)

**A cinematic, private network-quality test for your terminal.**

</div>

<div align="center">

![lag network flight deck and results](docs/lag-demo.gif)

</div>

This is the .NET 10 edition of `lag`, rendered with
[Spectre.Console](https://spectreconsole.net/). It goes beyond bandwidth to
measure the network qualities people actually feel: latency, jitter, request
loss, DNS delay, upload/download capacity, and responsiveness under load.

While testing, Spectre.Console presents an animated multi-stage status display.
The completed report uses a rich metric table, quality chart, diagnosis panel,
and actionable recommendations.

```text
╔═ lag telemetry ══════════════════════════════════════════════════════════════╗
║  ◢ NETWORK FLIGHT DECK ◣                                                     ║
║                                                                              ║
║  DEVICE ◉ ━━━━━━━━━●━━━━━━━━━━━━━━━━━━━━━━━━━ ◆ ROUTER ━━━━━━ ⬡ EDGE         ║
║            ▁▂▄▆██▇▅▃▂▁▂▄▆▇▆▄▂▁▂▃▅▇█▇▅▃▂▁▂▃▅▆▇▆▄▂▁▂▃▅▇█▇▅▃▂                  ║
║            LIVE PROBE PULSE                                                  ║
║                                                                              ║
║  ◆ LINK  ◆ ROUTE  ◈ RESPONSE  ◇ LOAD  ◇ RETURN  ◇ ANALYZE                    ║
║                                                                              ║
║   TESTING RESPONSIVENESS   latency · jitter · stability    T+ 00:02.4        ║
║  Local analysis · generated test traffic · no report upload                  ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

## Network Flight Deck

The waiting screen is a live terminal instrument rather than a generic spinner:

- A bidirectional packet travels from **Device → Router → Internet Edge**
- A continuously evolving probe waveform visualizes test activity
- Six telemetry stages transition from pending to active to complete
- The active measurement and protocol layer are always visible
- A mission timer shows elapsed test time
- Spectre.Console redraws the dashboard in place and clears it before results

The animation is decorative but stage-accurate: it never invents intermediate
network measurements. Only completed measurements appear in the final report.

## What it answers

> Why do calls break up, games lag, or websites pause when my speed looks fine?

`lag` translates measurements into symptoms and suggests what to try first. It
does not change network settings.

## Run

Requires the .NET 10 SDK:

```bash
dotnet run --project src/Lag
```

Quick test without throughput transfer:

```bash
dotnet run --project src/Lag -- --quick
```

Machine-readable output:

```bash
dotnet run --project src/Lag -- --json
```

### CLI options

| Option | Purpose |
| --- | --- |
| `--quick` | Skip throughput and loaded-latency transfer tests |
| `--json` | Emit stable machine-readable JSON without animation |
| `--no-color` | Disable color while retaining structured presentation |
| `--force-color` | Force true color in capable terminals that cannot advertise it |
| `--version` | Print application and framework version |

## Publish native launchers

Framework-dependent:

```bash
dotnet publish src/Lag -c Release -r linux-x64 --self-contained false
dotnet publish src/Lag -c Release -r osx-arm64 --self-contained false
dotnet publish src/Lag -c Release -r win-x64 --self-contained false
```

Replace the RID with `linux-arm64`, `osx-x64`, or `win-arm64` as needed.

## Platforms

Spectre.Console handles ANSI capability detection, Unicode fallback, color, and
interactive rendering across:

- Linux terminals
- macOS Terminal, iTerm2, and compatible terminals
- Windows Terminal
- PowerShell
- Command Prompt

When output is redirected or `--json` is used, live animation is disabled and
the output remains automation-safe.

## Privacy and data use

Measurements use Cloudflare's public speed-test endpoints. Approximately
3–11 MB is downloaded and 2 MB uploaded as generated test bytes. No account,
report, filename, location, or browsing history is uploaded by this program.
The endpoint provider can observe the connecting IP, as with any network
request.

Probe loss means failed HTTPS probes; it is deliberately not presented as raw
ICMP packet loss.

## Build

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

The repository targets `net10.0`, treats compiler warnings as errors, and tests
on Linux, macOS, and Windows in GitHub Actions.

## 💜 Special thanks

The cinematic terminal experience is powered by
[**Spectre.Console**](https://github.com/spectreconsole/spectre.console), an
exceptional open-source library for beautiful, cross-platform .NET console
applications.

Huge thanks and a special shoutout to the Spectre.Console maintainers and
contributors for the live rendering engine, rich tables, panels, charts,
Figlet text, terminal capability detection, and the care they put into making
CLI applications feel first-class.

If you build console applications in .NET, give their repository a star:
**[github.com/spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console)**.

The measurement design uses Cloudflare's public speed-test endpoints. Cloudflare
does not sponsor or endorse this project.

## License

MIT
