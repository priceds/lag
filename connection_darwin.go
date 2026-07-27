//go:build darwin

package main

func platformConnection() Connection {
	return Connection{Type: "network"}
}
