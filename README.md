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

- A packet follows the active test path from **Device → Internet Edge**
- A continuously evolving oscilloscope waveform visualizes probe timing
- Live counters stream real latency, jitter, loss, download, upload, and
  loaded-latency measurements
- Sample counts and direction-specific transferred bytes update during testing
- Purposeful 24-bit colors identify test state and signal quality
- Telemetry stages transition from pending to active to complete
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

Operations diagnostics:

```bash
lag --netops
lag --quick --netops
lag --netops --json > lag-netops.json
```

Machine-readable output:

```bash
dotnet run --project src/Lag -- --json
```

### CLI options

| Option | Purpose |
| --- | --- |
| `--quick` | Skip throughput and loaded-latency transfer tests |
| `--netops` | Add gateway, interface, IPv4/IPv6, connection-timing, endpoint, Wi-Fi, MTU, TCP, and route diagnostics |
| `--json` | Emit stable machine-readable JSON without animation |
| `--no-color` | Disable color while retaining structured presentation |
| `--force-color` | Force true color in capable terminals that cannot advertise it |
| `--version` | Print application and framework version |

## NetOps mode

`lag --netops` turns the end-user quality test into a bounded first-response
snapshot for help desks, network engineers, SREs, and operations teams. It
keeps the normal experience score and adds enough context to separate a local
link problem from DNS, dual-stack, path, TLS, or upstream service trouble.

```bash
# Full quality test plus the operations report
lag --netops

# Low-impact triage: no download/upload or loaded-latency transfer
lag --quick --netops

# Capture structured evidence for a ticket or repeated comparison
lag --netops --json > lag-netops.json
```

In PowerShell, the same capture is:

```powershell
lag --netops --json | Set-Content -Encoding utf8 lag-netops.json
```

Quick mode is useful on a production host or constrained link, but its
throughput fields are intentionally shown as unavailable. Run the full mode
when bandwidth and responsiveness under load matter.

### How to read the report

| Finding | What it usually narrows down |
| --- | --- |
| Gateway RTT is already high or unstable | Local Wi-Fi, Ethernet, adapter, switch, or router |
| Gateway is clean but public latency is high | ISP access link, upstream routing, or congestion |
| IPv4 works while IPv6 fails (or the reverse) | Dual-stack routing, firewall, or provider configuration |
| DNS is slow while TCP is fast | Resolver, VPN DNS, filtering, or split-DNS behavior |
| TCP is slow before TLS begins | Route, firewall, proxy, or connection-establishment delay |
| TLS is disproportionately slow | Inspection proxy, certificate path, or remote TLS edge |
| TTFB is slow after a clean TLS setup | Remote service or application response time |
| Endpoint timings diverge sharply | Destination-specific routing or peering |
| Weak Wi-Fi signal or low negotiated rate | RF coverage, interference, band, channel, or distance |
| Interface errors or drops increase | Driver, cable, adapter, congestion, or queue pressure |
| Path-MTU probe fails | VPN/overlay encapsulation, PMTUD, or filtering |

The **Wi-Fi radio** block is structured into SSID, BSSID, band/channel, signal,
and negotiated rates when the operating system exposes them. The **connection
timing** block breaks one request into DNS, TCP, TLS, and time-to-first-byte.
The **endpoint matrix** helps distinguish a machine-wide problem from a
destination-specific one.

Useful operator workflows:

1. Run `lag --quick --netops` for low-impact initial triage.
2. Compare Wi-Fi with Ethernet, or VPN on with VPN off.
3. Run the full `lag --netops` once transfer testing is acceptable.
4. Save JSON before and after a change and attach only reviewed output to the
   incident.
5. Repeat from another host or network segment to identify the fault boundary.

Hop tracing is best-effort and time-bounded. Windows supplies `tracert`;
Linux systems need the optional `traceroute` command for hop details, and
macOS normally includes it. Missing platform tools are reported as unavailable
instead of failing the quality test.

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

`--netops` additionally inspects local interface configuration and may run
bounded gateway ping, path-MTU, TCP summary, Wi-Fi radio, and hop-trace probes.
Its report can contain local IP addresses, gateway and DNS addresses, Wi-Fi
SSID/BSSID, public endpoint addresses, route hops, and TLS certificate issuer.
Review JSON output before sharing it outside your organization.

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
