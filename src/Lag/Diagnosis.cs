namespace Lag;

internal static class Diagnosis
{
    public static void Apply(NetworkReport report)
    {
        var m = report.Metrics;
        if (!m.Reachable)
        {
            report.Score = 0;
            report.Verdict = Verdict.Offline;
            report.Diagnosis = "The public test endpoint could not be reached.";
            report.LikelySymptoms.Add("Websites and online applications will not connect.");
            report.TryFirst.Add("Check Wi-Fi or Ethernet, then open a browser to rule out a captive portal.");
            report.TryFirst.Add("Temporarily disconnect VPN or proxy software and test again.");
            return;
        }

        var score = 100;
        score -= Penalty(m.LatencyMs, 40, 80, 150, 5, 15, 30);
        score -= Penalty(m.JitterMs, 10, 25, 50, 5, 15, 30);
        score -= Penalty(m.ProbeLossPercent, .1, 1, 5, 10, 25, 45);
        score -= Penalty(m.DnsMs, 80, 180, 400, 3, 10, 20);
        if (m.DownloadMbps > 0)
            score -= PenaltyLow(m.DownloadMbps, 100, 25, 5, 2, 2, 10, 25);
        if (m.UploadMbps > 0)
            score -= PenaltyLow(m.UploadMbps, 20, 8, 2, .5, 2, 8, 20);
        if (m.BufferbloatMs > 0)
            score -= Penalty(m.BufferbloatMs, 30, 80, 180, 5, 15, 30);
        report.Score = Math.Clamp(score, 0, 100);
        report.Verdict = score switch
        {
            >= 90 => Verdict.Excellent,
            >= 75 => Verdict.Good,
            >= 50 => Verdict.Unstable,
            _ => Verdict.Poor
        };

        if (m.ProbeLossPercent >= 3)
        {
            report.Diagnosis = "The connection is losing requests, even if its headline speed looks fast.";
            report.LikelySymptoms.AddRange(["Robotic or missing call audio", "Gaming lag and rubber-banding", "Streams dropping quality"]);
            report.TryFirst.AddRange(["Move closer to the router or test over Ethernet.", "Restart the router; persistent loss may be upstream at the ISP."]);
        }
        else if (m.BufferbloatMs >= 100)
        {
            report.Diagnosis = "Latency rises sharply while the connection is busy (bufferbloat).";
            report.LikelySymptoms.AddRange(["Calls or games lag while another device downloads", "Fast speed tests but poor responsiveness"]);
            report.TryFirst.AddRange(["Enable SQM/QoS on the router if available.", "Pause large transfers during calls or games."]);
        }
        else if (m.JitterMs >= 25)
        {
            report.Diagnosis = "Response times vary substantially from moment to moment.";
            report.LikelySymptoms.AddRange(["Uneven call audio", "Intermittent gaming lag"]);
            report.TryFirst.AddRange(["Use Ethernet or a less congested Wi-Fi channel.", "Move closer to the access point and retest."]);
        }
        else if (m.DnsMs >= 180)
        {
            report.Diagnosis = "The connection responds after connecting, but name lookups are slow.";
            report.LikelySymptoms.Add("Websites pause before beginning to load.");
            report.TryFirst.Add("Try a reputable DNS resolver or check VPN DNS settings.");
        }
        else if (m.LatencyMs >= 100)
        {
            report.Diagnosis = "Round-trip latency is high; bandwidth alone cannot make interactions responsive.";
            report.LikelySymptoms.AddRange(["Noticeable call delay", "Slow gaming response"]);
            report.TryFirst.AddRange(["Disconnect unnecessary VPNs.", "Compare Wi-Fi with Ethernet to isolate the local link."]);
        }
        else
        {
            report.Diagnosis = "The measured connection is responsive and stable.";
            report.TryFirst.Add("If one application still feels slow, the issue is likely specific to that service, route, VPN, or device.");
        }

        if (report.Connection.VpnDetected)
            report.Notes.Add($"VPN-like interface active: {report.Connection.VpnInterface}");
        if (report.Connection.ProxyDetected)
            report.Notes.Add($"Proxy environment variable active: {report.Connection.ProxySource}");
        report.Notes.Add("Probe loss means failed HTTPS probes, not raw ICMP packet loss.");
    }

    private static int Penalty(double value, double good, double fair, double poor, int fairPenalty, int poorPenalty, int badPenalty) =>
        value <= good ? 0 : value <= fair ? fairPenalty : value <= poor ? poorPenalty : badPenalty;

    private static int PenaltyLow(double value, double excellent, double good, double fair, double poor, int goodPenalty, int fairPenalty, int poorPenalty) =>
        value >= excellent ? 0 : value >= good ? goodPenalty : value >= fair ? fairPenalty : value >= poor ? poorPenalty : poorPenalty + 10;
}
