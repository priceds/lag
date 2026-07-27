//go:build windows

package main

import (
	"strconv"
	"strings"
	"time"
)

func platformConnection() Connection {
	connection := Connection{Type: "network"}
	output, err := commandOutput(1500*time.Millisecond, "netsh.exe", "wlan", "show", "interfaces")
	if err != nil {
		return connection
	}
	for _, line := range strings.Split(output, "\n") {
		key, value, found := strings.Cut(line, ":")
		if !found {
			continue
		}
		switch strings.ToLower(strings.TrimSpace(key)) {
		case "name":
			connection.Interface = strings.TrimSpace(value)
		case "state":
			if strings.EqualFold(strings.TrimSpace(value), "connected") {
				connection.Type = "Wi-Fi"
			}
		case "signal":
			percent, _ := strconv.Atoi(strings.TrimSuffix(strings.TrimSpace(value), "%"))
			connection.SignalText = percentLabel(percent)
		}
	}
	return connection
}
