//go:build windows

package main

import (
	"os"
	"strings"
	"syscall"
	"unsafe"
)

const enableVirtualTerminalProcessing = 0x0004

func prepareAnimationTerminal() animationTerminal {
	kernel32 := syscall.NewLazyDLL("kernel32.dll")
	getConsoleMode := kernel32.NewProc("GetConsoleMode")
	setConsoleMode := kernel32.NewProc("SetConsoleMode")
	handle := syscall.Handle(os.Stdout.Fd())
	var mode uint32
	ok, _, _ := getConsoleMode.Call(uintptr(handle), uintptr(unsafe.Pointer(&mode)))
	if ok != 0 {
		enabled, _, _ := setConsoleMode.Call(uintptr(handle), uintptr(mode|enableVirtualTerminalProcessing))
		if enabled != 0 {
			return animationTerminal{
				Begin: "\x1b[?25l",
				Clear: "\r\x1b[2K",
				End:   "\x1b[?25h",
				ANSI:  true,
			}
		}
	}

	// Legacy Command Prompt fallback: redraw a padded carriage-return line.
	// It animates without cursor-control sequences or ANSI color.
	clear := "\r" + strings.Repeat(" ", 140) + "\r"
	return animationTerminal{Clear: clear, ANSI: false}
}
