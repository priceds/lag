package main

import "time"

type Verdict string

const (
	VerdictExcellent Verdict = "Excellent"
	VerdictGood      Verdict = "Good"
	VerdictUnstable  Verdict = "Unstable"
	VerdictPoor      Verdict = "Poor"
	VerdictOffline   Verdict = "Offline"
)

type Report struct {
	GeneratedAt time.Time  `json:"generated_at"`
	Platform    string     `json:"platform"`
	Version     string     `json:"version"`
	Connection  Connection `json:"connection"`
	Metrics     Metrics    `json:"metrics"`
	Score       int        `json:"score"`
	Verdict     Verdict    `json:"verdict"`
	Diagnosis   string     `json:"diagnosis"`
	Symptoms    []string   `json:"likely_symptoms,omitempty"`
	Actions     []string   `json:"try_first,omitempty"`
	Notes       []string   `json:"notes,omitempty"`
}

type Connection struct {
	Type       string `json:"type"`
	Interface  string `json:"interface,omitempty"`
	SignalDBM  int    `json:"signal_dbm,omitempty"`
	SignalText string `json:"signal,omitempty"`
	Proxy      bool   `json:"proxy_detected"`
	ProxyName  string `json:"proxy_source,omitempty"`
	VPN        bool   `json:"vpn_detected"`
	VPNName    string `json:"vpn_interface,omitempty"`
}

type Metrics struct {
	Reachable       bool    `json:"reachable"`
	LatencyMS       float64 `json:"latency_ms,omitempty"`
	JitterMS        float64 `json:"jitter_ms,omitempty"`
	ProbeLossPct    float64 `json:"probe_loss_percent,omitempty"`
	DNSMS           float64 `json:"dns_ms,omitempty"`
	DownloadMbps    float64 `json:"download_mbps,omitempty"`
	UploadMbps      float64 `json:"upload_mbps,omitempty"`
	LoadedLatencyMS float64 `json:"loaded_latency_ms,omitempty"`
	BufferbloatMS   float64 `json:"bufferbloat_ms,omitempty"`
}

type Progress struct {
	Phase  string
	Detail string
}
