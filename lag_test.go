package main

import (
	"math"
	"testing"
)

func TestMedian(t *testing.T) {
	if got := median([]float64{9, 1, 5, 3}); got != 4 {
		t.Fatalf("median = %v", got)
	}
}

func TestJitter(t *testing.T) {
	if got := jitter([]float64{10, 20, 15}); math.Abs(got-7.5) > 0.001 {
		t.Fatalf("jitter = %v", got)
	}
}

func TestDiagnosePacketLoss(t *testing.T) {
	report := Report{Metrics: Metrics{Reachable: true, LatencyMS: 20, JitterMS: 3, ProbeLossPct: 8, DNSMS: 12, DownloadMbps: 100, UploadMbps: 20}}
	diagnose(&report)
	if report.Verdict == VerdictExcellent || !stringsContain(report.Diagnosis, "losing") {
		t.Fatalf("unexpected diagnosis: %#v", report)
	}
}

func TestDiagnoseBufferbloat(t *testing.T) {
	report := Report{Metrics: Metrics{Reachable: true, LatencyMS: 20, JitterMS: 2, DNSMS: 10, DownloadMbps: 100, UploadMbps: 20, BufferbloatMS: 180}}
	diagnose(&report)
	if !stringsContain(report.Diagnosis, "bufferbloat") {
		t.Fatalf("unexpected diagnosis: %q", report.Diagnosis)
	}
}

func TestOffline(t *testing.T) {
	report := Report{}
	diagnose(&report)
	if report.Verdict != VerdictOffline || report.Score != 0 {
		t.Fatalf("unexpected report: %#v", report)
	}
}

func TestProgressIsNonBlocking(t *testing.T) {
	progress := make(chan Progress, 1)
	emitProgress(progress, "one", "first")
	emitProgress(progress, "two", "channel is deliberately full")
	got := <-progress
	if got.Phase != "one" {
		t.Fatalf("unexpected progress: %#v", got)
	}
}

func TestAnimationFramesRemainReadableWithoutColor(t *testing.T) {
	if got := stripANSIForTest("\x1b[36m●━━━━○\x1b[0m"); got != "●━━━━○" {
		t.Fatalf("got %q", got)
	}
}

func stringsContain(value, fragment string) bool {
	for index := 0; index+len(fragment) <= len(value); index++ {
		if value[index:index+len(fragment)] == fragment {
			return true
		}
	}
	return false
}
