//go:build !windows

package main

func prepareAnimationTerminal() animationTerminal {
	return animationTerminal{
		Begin: "\x1b[?25l",
		Clear: "\r\x1b[2K",
		End:   "\x1b[?25h",
		ANSI:  true,
	}
}
