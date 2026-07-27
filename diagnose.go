package main

import "math"

func diagnose(report *Report) {
	m := report.Metrics
	if !m.Reachable {
		report.Score = 0
		report.Verdict = VerdictOffline
		report.Diagnosis = "The public test endpoint could not be reached."
		report.Symptoms = []string{"Websites and online applications will not connect"}
		report.Actions = []string{"Check Wi-Fi or Ethernet, then open a browser to rule out a captive portal.", "Temporarily disconnect VPN or proxy software and test again."}
		return
	}

	score := 100
	score -= penalty(m.LatencyMS, 40, 80, 150, 5, 15, 30)
	score -= penalty(m.JitterMS, 10, 25, 50, 5, 15, 30)
	score -= penalty(m.ProbeLossPct, 0.1, 1, 5, 10, 25, 45)
	score -= penalty(m.DNSMS, 80, 180, 400, 3, 10, 20)
	if m.DownloadMbps > 0 {
		score -= penaltyLow(m.DownloadMbps, 100, 25, 5, 2, 2, 10, 25)
	}
	if m.UploadMbps > 0 {
		score -= penaltyLow(m.UploadMbps, 20, 8, 2, 0.5, 2, 8, 20)
	}
	if m.BufferbloatMS > 0 {
		score -= penalty(m.BufferbloatMS, 30, 80, 180, 5, 15, 30)
	}
	score = int(math.Max(0, math.Min(100, float64(score))))
	report.Score = score

	switch {
	case score >= 90:
		report.Verdict = VerdictExcellent
	case score >= 75:
		report.Verdict = VerdictGood
	case score >= 50:
		report.Verdict = VerdictUnstable
	default:
		report.Verdict = VerdictPoor
	}

	switch {
	case m.ProbeLossPct >= 3:
		report.Diagnosis = "The connection is losing requests, even if its headline speed looks fast."
		report.Symptoms = []string{"Robotic or missing audio during calls", "Gaming lag and rubber-banding", "Streams dropping quality unexpectedly"}
		report.Actions = []string{"Move closer to the router or test over Ethernet.", "Restart the router, then compare again; persistent loss may be upstream at the ISP."}
	case m.BufferbloatMS >= 100:
		report.Diagnosis = "Latency rises sharply while the connection is busy (bufferbloat)."
		report.Symptoms = []string{"Calls or games lag while another device downloads", "Fast speed tests but poor responsiveness"}
		report.Actions = []string{"Enable SQM/QoS on the router if available.", "Pause large uploads and downloads during calls or games."}
	case m.JitterMS >= 25:
		report.Diagnosis = "Response times vary substantially from moment to moment."
		report.Symptoms = []string{"Uneven call audio", "Intermittent gaming lag"}
		report.Actions = []string{"Use Ethernet or a less congested Wi-Fi channel.", "Move closer to the access point and retest."}
	case m.DNSMS >= 180:
		report.Diagnosis = "The connection is responsive after connecting, but name lookups are slow."
		report.Symptoms = []string{"Websites pause before beginning to load"}
		report.Actions = []string{"Try a reputable DNS resolver or check VPN DNS settings."}
	case m.LatencyMS >= 100:
		report.Diagnosis = "Round-trip latency is high; bandwidth alone cannot make interactions responsive."
		report.Symptoms = []string{"Noticeable call delay", "Slow gaming response"}
		report.Actions = []string{"Prefer a nearby service region and disconnect unnecessary VPNs.", "Compare Wi-Fi with Ethernet to isolate the local link."}
	case m.DownloadMbps > 0 && m.DownloadMbps < 5:
		report.Diagnosis = "Available download bandwidth is limited."
		report.Symptoms = []string{"Video buffering", "Slow downloads"}
		report.Actions = []string{"Stop competing downloads and check who else is using the connection.", "Compare Wi-Fi with Ethernet."}
	default:
		report.Diagnosis = "The measured connection is responsive and stable."
		report.Actions = []string{"If one application still feels slow, the problem is likely specific to that service, route, VPN, or device."}
	}

	if report.Connection.VPN {
		report.Notes = append(report.Notes, "A VPN-like interface is active: "+report.Connection.VPNName)
	}
	if report.Connection.Proxy {
		report.Notes = append(report.Notes, "A proxy environment variable is active: "+report.Connection.ProxyName)
	}
	report.Notes = append(report.Notes, "Probe loss is failed HTTPS test requests, not raw ICMP packet loss.")
}

func penalty(value, good, fair, poor float64, fairPenalty, poorPenalty, badPenalty int) int {
	switch {
	case value <= good:
		return 0
	case value <= fair:
		return fairPenalty
	case value <= poor:
		return poorPenalty
	default:
		return badPenalty
	}
}

func penaltyLow(value, excellent, good, fair, poor float64, goodPenalty, fairPenalty, poorPenalty int) int {
	switch {
	case value >= excellent:
		return 0
	case value >= good:
		return goodPenalty
	case value >= fair:
		return fairPenalty
	case value >= poor:
		return poorPenalty
	default:
		return poorPenalty + 10
	}
}
