//go:build linux

package main

import (
	"os/exec"
	"strconv"
	"strings"
	"time"
)

func platformConnection() Connection {
	connection := Connection{}
	if _, err := exec.LookPath("iw"); err == nil {
		if output, err := commandOutput(1200*time.Millisecond, "iw", "dev"); err == nil {
			for _, line := range strings.Split(output, "\n") {
				line = strings.TrimSpace(line)
				if strings.HasPrefix(line, "Interface ") {
					connection.Type = "Wi-Fi"
					connection.Interface = strings.TrimSpace(strings.TrimPrefix(line, "Interface "))
					break
				}
			}
			if connection.Interface != "" {
				if output, err := commandOutput(1200*time.Millisecond, "iw", "dev", connection.Interface, "link"); err == nil {
					for _, line := range strings.Split(output, "\n") {
						fields := strings.Fields(strings.TrimSpace(line))
						if len(fields) >= 2 && fields[0] == "signal:" {
							connection.SignalDBM, _ = strconv.Atoi(fields[1])
							connection.SignalText = signalLabel(connection.SignalDBM)
						}
					}
				}
			}
		}
	}
	if connection.Type == "" {
		connection.Type = "Ethernet or network"
	}
	return connection
}
