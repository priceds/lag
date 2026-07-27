package main

import (
	"fmt"
	"strings"
)

const (
	reset  = "\x1b[0m"
	bold   = "\x1b[1m"
	dim    = "\x1b[2m"
	green  = "\x1b[32m"
	red    = "\x1b[31m"
	yellow = "\x1b[33m"
	cyan   = "\x1b[36m"
)

func render(report Report, color bool) string {
	var out strings.Builder
	style := func(code, text string) string {
		if !color {
			return text
		}
		return code + text + reset
	}
	fmt.Fprintf(&out, "\n%s  %s\n", style(bold+cyan, "lag"), style(dim, report.Platform))
	fmt.Fprintf(&out, "%s\n\n", style(dim, "Private network-quality test — no account and no result upload"))

	connection := report.Connection.Type
	if report.Connection.Interface != "" {
		connection += " · " + report.Connection.Interface
	}
	metricLine(&out, report.Metrics.Reachable, "Connected", connection, style)
	if report.Metrics.Reachable {
		metricLine(&out, report.Metrics.LatencyMS <= 80, "Latency", formatMS(report.Metrics.LatencyMS), style)
		metricLine(&out, report.Metrics.JitterMS <= 25, "Jitter", formatMS(report.Metrics.JitterMS), style)
		metricLine(&out, report.Metrics.ProbeLossPct < 1, "Stability", fmt.Sprintf("%.1f%% probe loss", report.Metrics.ProbeLossPct), style)
		metricLine(&out, report.Metrics.DNSMS <= 180, "DNS", formatMS(report.Metrics.DNSMS), style)
		if report.Metrics.DownloadMbps > 0 {
			metricLine(&out, report.Metrics.DownloadMbps >= 25, "Download", fmt.Sprintf("%.1f Mbps", report.Metrics.DownloadMbps), style)
		}
		if report.Metrics.UploadMbps > 0 {
			metricLine(&out, report.Metrics.UploadMbps >= 8, "Upload", fmt.Sprintf("%.1f Mbps", report.Metrics.UploadMbps), style)
		}
		if report.Metrics.LoadedLatencyMS > 0 {
			metricLine(&out, report.Metrics.BufferbloatMS <= 80, "Under load", fmt.Sprintf("%s · +%.0f ms", formatMS(report.Metrics.LoadedLatencyMS), report.Metrics.BufferbloatMS), style)
		}
	}

	tone := green
	if report.Score < 75 {
		tone = yellow
	}
	if report.Score < 50 {
		tone = red
	}
	fmt.Fprintf(&out, "\n  %s %s  %s\n", style(bold, fmt.Sprintf("%3d/100", report.Score)), style(cyan, scoreBar(report.Score, 20)), style(bold+tone, string(report.Verdict)))
	fmt.Fprintf(&out, "\n  %s\n  %s\n", style(bold, "Diagnosis"), report.Diagnosis)
	if len(report.Symptoms) > 0 {
		fmt.Fprintf(&out, "\n  %s\n", style(bold, "Likely symptoms"))
		for _, symptom := range report.Symptoms {
			fmt.Fprintf(&out, "  %s %s\n", style(dim, "•"), symptom)
		}
	}
	if len(report.Actions) > 0 {
		fmt.Fprintf(&out, "\n  %s\n", style(bold, "Try first"))
		for _, action := range report.Actions {
			fmt.Fprintf(&out, "  %s %s\n", style(cyan, "→"), action)
		}
	}
	for _, note := range report.Notes {
		fmt.Fprintf(&out, "\n  %s %s\n", style(dim, "Note:"), style(dim, note))
	}
	fmt.Fprintln(&out)
	return out.String()
}

func metricLine(out *strings.Builder, pass bool, name, value string, style func(string, string) string) {
	icon, tone := "✓", green
	if !pass {
		icon, tone = "!", yellow
	}
	fmt.Fprintf(out, "  %s  %-12s %s\n", style(tone, icon), style(bold, name), value)
}

func formatMS(value float64) string {
	return fmt.Sprintf("%.0f ms", value)
}

func scoreBar(score, width int) string {
	filled := score * width / 100
	if filled < 0 {
		filled = 0
	}
	if filled > width {
		filled = width
	}
	return strings.Repeat("█", filled) + strings.Repeat("░", width-filled)
}
