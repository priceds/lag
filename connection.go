package main

import (
	"os/exec"
	"strings"
	"time"
)

func commandOutput(timeout time.Duration, name string, args ...string) (string, error) {
	command := exec.Command(name, args...)
	var output strings.Builder
	command.Stdout = &output
	command.Stderr = &output
	if err := command.Start(); err != nil {
		return "", err
	}
	timer := time.AfterFunc(timeout, func() { _ = command.Process.Kill() })
	err := command.Wait()
	timer.Stop()
	return output.String(), err
}

func signalLabel(dbm int) string {
	switch {
	case dbm >= -50:
		return "excellent"
	case dbm >= -60:
		return "good"
	case dbm >= -70:
		return "fair"
	default:
		return "weak"
	}
}

func percentLabel(percent int) string {
	switch {
	case percent >= 80:
		return "excellent"
	case percent >= 60:
		return "good"
	case percent >= 40:
		return "fair"
	default:
		return "weak"
	}
}
