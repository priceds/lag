#!/usr/bin/env sh
set -eu

repo="priceds/lag"
install_dir="${LAG_INSTALL_DIR:-$HOME/.local/bin}"

case "$(uname -s)" in
  Linux) os="linux" ;;
  Darwin) os="osx" ;;
  *) echo "lag: unsupported operating system: $(uname -s)" >&2; exit 1 ;;
esac

case "$(uname -m)" in
  x86_64|amd64) arch="x64" ;;
  arm64|aarch64) arch="arm64" ;;
  *) echo "lag: unsupported architecture: $(uname -m)" >&2; exit 1 ;;
esac

asset="lag-${os}-${arch}.tar.gz"
url="https://github.com/${repo}/releases/latest/download/${asset}"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT HUP INT TERM

echo "Installing lag for ${os}-${arch}…"
if command -v curl >/dev/null 2>&1; then
  curl --fail --location --progress-bar "$url" --output "$tmp_dir/$asset"
elif command -v wget >/dev/null 2>&1; then
  wget --output-document="$tmp_dir/$asset" "$url"
else
  echo "lag: curl or wget is required" >&2
  exit 1
fi

mkdir -p "$install_dir"
tar -xzf "$tmp_dir/$asset" -C "$tmp_dir"
install -m 0755 "$tmp_dir/lag" "$install_dir/lag"

echo "Installed lag to $install_dir/lag"
case ":$PATH:" in
  *":$install_dir:"*) ;;
  *) echo "Add $install_dir to PATH, then run: lag" ;;
esac
