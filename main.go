package main

import (
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"runtime"
	"time"
)

var version = "dev"

func main() {
	os.Exit(run(os.Args[1:]))
}

func run(args []string) int {
	flags := flag.NewFlagSet("lag", flag.ContinueOnError)
	flags.SetOutput(os.Stderr)
	jsonOutput := flags.Bool("json", false, "print machine-readable JSON")
	noColor := flags.Bool("no-color", false, "disable ANSI colors")
	quick := flags.Bool("quick", false, "skip bandwidth and loaded-latency tests")
	showVersion := flags.Bool("version", false, "print version")
	timeout := flags.Duration("timeout", 25*time.Second, "maximum test duration")
	flags.Usage = func() {
		fmt.Fprintln(flags.Output(), "lag — your internet is fast, so why does it feel slow?")
		fmt.Fprintln(flags.Output(), "\nUsage: lag [--quick] [--json] [--no-color] [--timeout 25s]")
		flags.PrintDefaults()
	}
	if err := flags.Parse(args); err != nil {
		return 2
	}
	if *showVersion {
		fmt.Printf("lag %s (%s/%s)\n", version, runtime.GOOS, runtime.GOARCH)
		return 0
	}
	if flags.NArg() != 0 {
		flags.Usage()
		return 2
	}

	ctx, cancel := context.WithTimeout(context.Background(), *timeout)
	defer cancel()
	interactive := !*jsonOutput && isTerminal(os.Stdout)
	var progress chan Progress
	var animationDone chan struct{}
	if interactive {
		progress = make(chan Progress, 8)
		animationDone = make(chan struct{})
		go animate(progress, animationDone, !*noColor && os.Getenv("NO_COLOR") == "")
	}

	report := measure(ctx, !*quick, progress)
	if progress != nil {
		close(progress)
		<-animationDone
	}
	report.Version = version

	if *jsonOutput {
		encoder := json.NewEncoder(os.Stdout)
		encoder.SetIndent("", "  ")
		if err := encoder.Encode(report); err != nil {
			fmt.Fprintln(os.Stderr, "lag:", err)
			return 2
		}
	} else {
		color := !*noColor && os.Getenv("NO_COLOR") == "" && isTerminal(os.Stdout)
		fmt.Print(render(report, color))
	}
	if report.Verdict == VerdictOffline {
		return 1
	}
	return 0
}

func isTerminal(file *os.File) bool {
	info, err := file.Stat()
	return err == nil && info.Mode()&os.ModeCharDevice != 0
}
