package main

import (
	"bytes"
	"context"
	"crypto/rand"
	"io"
	"math"
	"net"
	"net/http"
	"os"
	"runtime"
	"sort"
	"strings"
	"sync"
	"time"
)

const (
	downloadURL = "https://speed.cloudflare.com/__down"
	uploadURL   = "https://speed.cloudflare.com/__up"
)

var client = &http.Client{
	Timeout: 20 * time.Second,
	Transport: &http.Transport{
		Proxy:                 http.ProxyFromEnvironment,
		ForceAttemptHTTP2:     true,
		MaxIdleConns:          16,
		MaxIdleConnsPerHost:   16,
		ResponseHeaderTimeout: 5 * time.Second,
	},
}

func measure(ctx context.Context, bandwidth bool, progress chan<- Progress) Report {
	emitProgress(progress, "Inspecting connection", "interface · VPN · proxy")
	report := Report{
		GeneratedAt: time.Now(),
		Platform:    runtime.GOOS + "/" + runtime.GOARCH,
		Connection:  inspectConnection(),
	}
	// Establish DNS, TCP, TLS, and HTTP/2 state before collecting the sample
	// set. Otherwise the first handshake is incorrectly counted as jitter.
	emitProgress(progress, "Opening a clean route", "DNS · TCP · TLS")
	_, _ = latencyProbe(ctx, 1)
	emitProgress(progress, "Testing responsiveness", "latency · jitter · stability")
	var latencySamples []float64
	var failures int
	var dnsMS float64
	var wg sync.WaitGroup
	wg.Add(2)
	go func() {
		defer wg.Done()
		latencySamples, failures = latencyProbe(ctx, 12)
	}()
	go func() {
		defer wg.Done()
		dnsMS = dnsProbe(ctx)
	}()
	wg.Wait()

	report.Metrics.DNSMS = dnsMS
	report.Metrics.Reachable = len(latencySamples) > 0
	if len(latencySamples) > 0 {
		report.Metrics.LatencyMS = median(latencySamples)
		report.Metrics.JitterMS = jitter(latencySamples)
	}
	report.Metrics.ProbeLossPct = float64(failures) / 12 * 100

	if bandwidth && report.Metrics.Reachable && ctx.Err() == nil {
		emitProgress(progress, "Putting the link under load", "download · loaded latency")
		var loaded []float64
		wg.Add(2)
		go func() {
			defer wg.Done()
			report.Metrics.DownloadMbps = downloadProbe(ctx)
		}()
		go func() {
			defer wg.Done()
			loaded, _ = latencyProbeWithSpacing(ctx, 10, 250*time.Millisecond)
		}()
		wg.Wait()
		if len(loaded) > 0 {
			report.Metrics.LoadedLatencyMS = median(loaded)
			report.Metrics.BufferbloatMS = math.Max(0, report.Metrics.LoadedLatencyMS-report.Metrics.LatencyMS)
		}
		if ctx.Err() == nil {
			emitProgress(progress, "Testing the return path", "upload throughput")
			report.Metrics.UploadMbps = uploadProbe(ctx, 2_000_000)
		}
	}

	emitProgress(progress, "Reading the connection", "quality · symptoms · fixes")
	diagnose(&report)
	return report
}

func emitProgress(progress chan<- Progress, phase, detail string) {
	if progress == nil {
		return
	}
	select {
	case progress <- Progress{Phase: phase, Detail: detail}:
	default:
	}
}

func latencyProbe(ctx context.Context, count int) ([]float64, int) {
	return latencyProbeWithSpacing(ctx, count, 90*time.Millisecond)
}

func latencyProbeWithSpacing(ctx context.Context, count int, spacing time.Duration) ([]float64, int) {
	samples := make([]float64, 0, count)
	failures := 0
	for index := 0; index < count; index++ {
		request, _ := http.NewRequestWithContext(ctx, http.MethodGet, downloadURL+"?bytes=0", nil)
		request.Header.Set("Cache-Control", "no-cache")
		start := time.Now()
		response, err := client.Do(request)
		elapsed := time.Since(start)
		if err != nil {
			failures++
		} else {
			_, _ = io.Copy(io.Discard, response.Body)
			_ = response.Body.Close()
			if response.StatusCode >= 200 && response.StatusCode < 400 {
				samples = append(samples, float64(elapsed.Microseconds())/1000)
			} else {
				failures++
			}
		}
		if index+1 < count {
			select {
			case <-ctx.Done():
				failures += count - index - 1
				return samples, failures
			case <-time.After(spacing):
			}
		}
	}
	return samples, failures
}

func dnsProbe(ctx context.Context) float64 {
	resolver := &net.Resolver{}
	var samples []float64
	for _, host := range []string{"cloudflare.com", "example.com", "github.com"} {
		start := time.Now()
		if _, err := resolver.LookupHost(ctx, host); err == nil {
			samples = append(samples, float64(time.Since(start).Microseconds())/1000)
		}
	}
	return median(samples)
}

func downloadProbe(ctx context.Context) float64 {
	// Ramp the payload like Cloudflare's reference test: slow connections do
	// not need to pull a large object to produce a useful estimate.
	first := downloadOnce(ctx, 1_000_000)
	if first <= 0 || first < 10 || ctx.Err() != nil {
		return first
	}
	second := downloadOnce(ctx, 8_000_000)
	if second > 0 {
		return second
	}
	return first
}

func downloadOnce(ctx context.Context, bytesWanted int64) float64 {
	request, _ := http.NewRequestWithContext(ctx, http.MethodGet, downloadURL+"?bytes="+itoa(bytesWanted), nil)
	request.Header.Set("Cache-Control", "no-cache")
	start := time.Now()
	response, err := client.Do(request)
	if err != nil {
		return 0
	}
	defer response.Body.Close()
	if response.StatusCode < 200 || response.StatusCode >= 400 {
		_, _ = io.Copy(io.Discard, response.Body)
		return 0
	}
	count, err := io.Copy(io.Discard, response.Body)
	if err != nil || count == 0 {
		return 0
	}
	return float64(count*8) / time.Since(start).Seconds() / 1_000_000
}

func uploadProbe(ctx context.Context, size int) float64 {
	payload := make([]byte, size)
	_, _ = rand.Read(payload[:32])
	request, _ := http.NewRequestWithContext(ctx, http.MethodPost, uploadURL, bytes.NewReader(payload))
	request.Header.Set("Content-Type", "application/octet-stream")
	start := time.Now()
	response, err := client.Do(request)
	if err != nil {
		return 0
	}
	defer response.Body.Close()
	_, _ = io.Copy(io.Discard, response.Body)
	if response.StatusCode < 200 || response.StatusCode >= 400 {
		return 0
	}
	return float64(size*8) / time.Since(start).Seconds() / 1_000_000
}

func median(values []float64) float64 {
	if len(values) == 0 {
		return 0
	}
	sorted := append([]float64(nil), values...)
	sort.Float64s(sorted)
	middle := len(sorted) / 2
	if len(sorted)%2 == 0 {
		return (sorted[middle-1] + sorted[middle]) / 2
	}
	return sorted[middle]
}

func jitter(values []float64) float64 {
	if len(values) < 2 {
		return 0
	}
	var total float64
	for index := 1; index < len(values); index++ {
		total += math.Abs(values[index] - values[index-1])
	}
	return total / float64(len(values)-1)
}

func inspectConnection() Connection {
	connection := platformConnection()
	for _, key := range []string{"HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "https_proxy", "http_proxy", "all_proxy"} {
		if os.Getenv(key) != "" {
			connection.Proxy = true
			connection.ProxyName = key
			break
		}
	}
	interfaces, _ := net.Interfaces()
	for _, networkInterface := range interfaces {
		if networkInterface.Flags&net.FlagUp == 0 || networkInterface.Flags&net.FlagLoopback != 0 {
			continue
		}
		name := strings.ToLower(networkInterface.Name)
		if containsAny(name, "tun", "tap", "wg", "vpn", "utun", "tailscale", "zerotier") {
			connection.VPN = true
			connection.VPNName = networkInterface.Name
		}
		if connection.Interface == "" {
			connection.Interface = networkInterface.Name
		}
	}
	if connection.Type == "" {
		connection.Type = "network"
	}
	return connection
}

func containsAny(value string, fragments ...string) bool {
	for _, fragment := range fragments {
		if strings.Contains(value, fragment) {
			return true
		}
	}
	return false
}

func itoa(value int64) string {
	if value == 0 {
		return "0"
	}
	var buffer [20]byte
	index := len(buffer)
	for value > 0 {
		index--
		buffer[index] = byte('0' + value%10)
		value /= 10
	}
	return string(buffer[index:])
}
