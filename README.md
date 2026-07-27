<div align="center">

# ⚡ lag

### Your internet is fast. So why does it feel slow?

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Spectre.Console](https://img.shields.io/badge/Spectre.Console-0.57.2-8A2BE2)](https://spectreconsole.net/)
[![CI](https://github.com/priceds/lag/actions/workflows/ci.yml/badge.svg)](https://github.com/priceds/lag/actions/workflows/ci.yml)
[![Platforms](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-06b6d4)](#platforms)
[![License](https://img.shields.io/badge/license-MIT-22c55e)](LICENSE)

**A private network-quality test that explains what speed tests miss.**

</div>

<div align="center">

![lag network flight deck and results](docs/lag-demo.gif)

</div>

Most speed tests answer one question: **how much data can this connection
move?** `lag` answers the question people actually ask: **why does it feel
slow?**

It measures latency, jitter, failed HTTPS probes, DNS delay, download and
upload capacity, and responsiveness while the connection is busy. The result
is a plain-language diagnosis with likely symptoms and practical next steps.

| Signal | What it reveals |
| --- | --- |
| Latency | The delay you feel in calls, games, and interactive apps |
| Jitter | Why audio or video can become uneven even on a fast connection |
| HTTPS probe loss | Failed real-world requests, without relying on ICMP |
| DNS | Delay before a site or service can begin connecting |
| Download / upload | Available transfer capacity in both directions |
| Loaded latency | Whether the connection stays responsive while busy |

## Live test

The dashboard makes the test observable while it runs:

- A bidirectional packet travels from **Device → Router → Internet Edge**
- A continuously evolving probe waveform visualizes test activity
- Six live counters stream real latency, jitter, loss, download, upload, and
  loaded-latency measurements
- Sample counts and direction-specific transferred bytes update during testing
- Full 24-bit color gradients animate across the topology and waveform
- Six telemetry stages transition from pending to active to complete
- The active measurement and protocol layer are always visible
- A mission timer shows elapsed test time
- The dashboard clears before a compact, glanceable results report

Packet motion and the waveform are decorative but stage-accurate. Every number
in the counter grid comes from an actual completed probe or transferred byte;
the final report uses the stabilized measurements.

## What it answers

> Why do calls break up, games lag, or websites pause when my speed looks fine?

`lag` translates measurements into symptoms and suggests what to try first. It
does not change network settings, claim to replace laboratory-grade network
analysis, or reduce connection quality to bandwidth alone.

## Why another speed test?

Two connections can report the same Mbps and feel completely different.
Bandwidth alone does not expose unstable delay, slow name resolution, failed
requests, or a router that becomes unresponsive whenever somebody starts an
upload. `lag` tests those conditions together and explains the result without
requiring networking expertise.

## Install

Prebuilt, self-contained releases do not require the .NET SDK.

### Linux

```bash
curl -fsSL https://raw.githubusercontent.com/priceds/lag/main/install.sh | sh
```

### macOS

```bash
curl -fsSL https://raw.githubusercontent.com/priceds/lag/main/install.sh | sh
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/priceds/lag/main/install.ps1 | iex
```

Installers select the correct x64 or Arm64 build and install for the current
user. Set `LAG_INSTALL_DIR` to choose another location. You can also download
an archive directly from [GitHub Releases](https://github.com/priceds/lag/releases).

## Run

After installation:

```bash
lag
```

Run from source with the .NET 10 SDK:

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

## Methodology and limitations

- Results describe the connection from this device at this moment; Wi-Fi,
  VPNs, background traffic, power saving, and endpoint distance can all affect
  them.
- Loaded latency is measured while `lag` generates transfer traffic. It is a
  practical bufferbloat signal, not a controlled laboratory benchmark.
- HTTPS probe failures are reported as probe loss and must not be interpreted
  as router-level ICMP packet loss.
- A single run is a snapshot. Compare repeated tests—idle and under normal
  household load—before drawing conclusions.
- Cloudflare provides the measurement endpoints but does not sponsor or
  endorse this project.

## Build

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

The repository targets `net10.0`, treats compiler warnings as errors, and tests
on Linux, macOS, and Windows in GitHub Actions.

## 💜 Special thanks

The rich terminal presentation is powered by
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
