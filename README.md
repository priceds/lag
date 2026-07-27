<div align="center">

# ⚡ lag

### Your internet is fast. So why does it feel slow?

[![CI](https://github.com/priceds/lag/actions/workflows/ci.yml/badge.svg)](https://github.com/priceds/lag/actions/workflows/ci.yml)
[![Go Report Card](https://goreportcard.com/badge/github.com/priceds/lag)](https://goreportcard.com/report/github.com/priceds/lag)
[![Go Version](https://img.shields.io/github/go-mod/go-version/priceds/lag?logo=go)](go.mod)
[![License: MIT](https://img.shields.io/badge/license-MIT-22c55e.svg)](LICENSE)
[![Platforms](https://img.shields.io/badge/platform-Linux%20%7C%20macOS%20%7C%20Windows-06b6d4)](#-platforms)

**One command. Real network quality. Plain-English answers.**

</div>

`lag` is a private, cross-platform internet-quality test that explains what the
numbers mean. It measures responsiveness, stability, DNS, throughput, and
latency under load, then translates them into likely symptoms and useful next
steps.

Unlike a traditional speed test, `lag` looks at the things people actually
feel: delay, instability, DNS pauses, request loss, and what happens to
responsiveness when the connection becomes busy.

## ✨ Why lag?

| A speed test tells you | `lag` tells you |
| --- | --- |
| “84 Mbps” | Whether calls, games, and streams should feel good |
| One bandwidth number | Latency, jitter, stability, DNS, and bufferbloat |
| That something is slow | What is probably wrong and what to try first |
| A web page | A private terminal report with no account |

While the test runs, an animated network pulse shows the current measurement
phase and elapsed time. It works in macOS and Linux terminals, Windows Terminal,
PowerShell, and Command Prompt. Older Windows consoles receive a no-ANSI
carriage-return fallback.

```console
$ lag

lag  linux/amd64
Private network-quality test — no account and no result upload

  ✓  Connected    Wi-Fi · wlan0
  ✓  Latency      21 ms
  ✓  Jitter       3 ms
  !  Stability    4.2% probe loss
  ✓  DNS          18 ms
  ✓  Download     146.7 Mbps
  ✓  Upload       31.4 Mbps
  !  Under load   189 ms · +168 ms

   58/100 ███████████░░░░░░░░░  Unstable

  Diagnosis
  The connection is losing requests, even if its headline speed looks fast.
```

## 🔬 What it measures

- Idle HTTPS round-trip latency
- Jitter between consecutive probes
- Failed HTTPS probes, clearly labelled as probe loss rather than ICMP loss
- DNS lookup time
- Download and upload throughput
- Latency while a download is active
- Bufferbloat: the increase from idle to loaded latency
- Network interface, Wi-Fi signal where available, proxy and VPN clues

## 🖥️ Platforms

| OS | Terminals |
| --- | --- |
| Linux | GNOME Terminal, Konsole, Alacritty, Kitty, and other ANSI terminals |
| macOS | Terminal, iTerm2, Warp, and other ANSI terminals |
| Windows | Windows Terminal, PowerShell, Command Prompt, with a legacy fallback |

The download test starts at 1 MB and ramps to 8 MB only on faster links. With
the 2 MB upload probe, the default test transfers approximately 3–11 MB plus
protocol overhead.

## 🚀 Install from source

Requires Go 1.23 or newer:

```bash
go install github.com/priceds/lag@latest
```

For local development:

```bash
git clone https://github.com/priceds/lag.git
cd lag
make check
```

## 🎛️ Usage

```text
lag [--quick] [--json] [--no-color] [--timeout 25s]
```

Use `--quick` to skip download, upload, and loaded-latency testing:

```bash
lag --quick
lag --json
lag --timeout 40s
```

Animation is enabled only when standard output is an interactive terminal.
JSON and redirected or piped output contain no cursor-control sequences.

## 🔒 Privacy

Measurements use Cloudflare's public speed-test endpoints. No account is
required, and `lag` does not upload its report, device name, file contents,
location, or browsing history. Like any network request, the measurement
provider can observe the connecting IP address.

## 🧭 Measurement notes

An HTTPS request measures the complete application-visible path, including DNS
where relevant, TCP, TLS, proxies, and VPN routing. It is more representative
of browsing and calling applications than privileged raw ICMP alone, but failed
HTTPS probes are not identical to network-layer packet loss. The output keeps
that distinction explicit.

One short test is evidence, not certainty. Run it several times when diagnosing
an intermittent connection.

## 🛠️ Development

```bash
make fmt
make vet
make test
make build
```

GoReleaser is configured for Linux, macOS, and Windows on amd64 and arm64.

## License

MIT
