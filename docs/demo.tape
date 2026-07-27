Output docs/lag-demo.gif

Require ./artifacts/demo/lag

Set Shell "bash"
Set FontSize 18
Set FontFamily "DejaVu Sans Mono"
Set Width 1100
Set Height 820
Set Framerate 12
Set PlaybackSpeed 1.15
Set CursorBlink false
Set WindowBar Colorful
Set BorderRadius 10
Set Margin 18
Set MarginFill "#111827"
Set Theme { "name": "lag", "background": "#070b14", "foreground": "#e5e7eb", "black": "#111827", "brightBlack": "#4b5563", "red": "#ef4444", "brightRed": "#f87171", "green": "#22c55e", "brightGreen": "#4ade80", "yellow": "#eab308", "brightYellow": "#facc15", "blue": "#0ea5e9", "brightBlue": "#38bdf8", "magenta": "#8b5cf6", "brightMagenta": "#a78bfa", "cyan": "#06b6d4", "brightCyan": "#22d3ee", "white": "#d1d5db", "brightWhite": "#f9fafb" }
Env TERM "xterm-256color"
Env COLORTERM "truecolor"

Type@55ms "./artifacts/demo/lag --force-color"
Sleep 350ms
Enter
Sleep 32s
Sleep 4s
