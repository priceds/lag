package main

import (
	"fmt"
	"os"
	"strings"
	"time"
)

func animate(events <-chan Progress, done chan<- struct{}, color bool) {
	defer close(done)
	terminal := prepareAnimationTerminal()
	color = color && terminal.ANSI
	frames := []string{
		"●━━━━○────○",
		"○●━━━○────○",
		"○━●━━○────○",
		"○━━●━○────○",
		"○━━━●○────○",
		"○━━━━●────○",
		"○────○●━━━○",
		"○────○━●━━○",
		"○────○━━●━○",
		"○────○━━━●○",
		"○────○━━━━●",
		"○────○━━━●○",
		"○────○━━●━○",
		"○────○━●━━○",
	}
	started := time.Now()
	current := Progress{Phase: "Starting network test", Detail: "preparing probes"}
	ticker := time.NewTicker(90 * time.Millisecond)
	defer ticker.Stop()
	fmt.Fprint(os.Stdout, terminal.Begin)
	defer fmt.Fprint(os.Stdout, terminal.Clear+terminal.End)

	frame := 0
	for {
		select {
		case update, open := <-events:
			if !open {
				return
			}
			current = update
		case <-ticker.C:
			pulse := frames[frame%len(frames)]
			frame++
			elapsed := time.Since(started).Round(100 * time.Millisecond)
			phase := current.Phase
			detail := current.Detail
			if color {
				pulse = "\x1b[36m" + pulse + "\x1b[0m"
				phase = "\x1b[1m" + phase + "\x1b[0m"
				detail = "\x1b[2m" + detail + "\x1b[0m"
			}
			fmt.Fprintf(os.Stdout, "%s  %s  %-27s  %s  %5s", terminal.Clear, pulse, phase, detail, elapsed)
		}
	}
}

type animationTerminal struct {
	Begin string
	Clear string
	End   string
	ANSI  bool
}

func stripANSIForTest(value string) string {
	// Kept small and dependency-free; useful for snapshot tests of animation
	// fragments without making terminal styling part of the public API.
	for _, code := range []string{"\x1b[36m", "\x1b[1m", "\x1b[2m", "\x1b[0m"} {
		value = strings.ReplaceAll(value, code, "")
	}
	return value
}
