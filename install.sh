#!/bin/sh
set -e

REPO="SimplicatedGamesStudio/unity-cli"

OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
case "$OS" in
  linux)  ;;
  darwin) ;;
  *)      echo "Unsupported OS: $OS (use Windows instructions in README)"; exit 1 ;;
esac

ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64)  ARCH="amd64" ;;
  aarch64|arm64)  ARCH="arm64" ;;
  *)              echo "Unsupported architecture: $ARCH"; exit 1 ;;
esac

INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

URL="https://github.com/${REPO}/releases/latest/download/unity-cli-${OS}-${ARCH}"

echo "Downloading unity-cli for ${OS}/${ARCH}..."
if curl -fsSL "$URL" -o "$INSTALL_DIR/unity-cli"; then
  chmod +x "$INSTALL_DIR/unity-cli"
else
  echo "Release binary not available for ${REPO}. Falling back to 'go install'."
  if ! command -v go >/dev/null 2>&1; then
    echo "Go is required for the fallback install path. Install Go or publish a release asset first."
    exit 1
  fi

  GOBIN="$INSTALL_DIR" go install "github.com/${REPO}@latest"
fi

case ":$PATH:" in
  *":$INSTALL_DIR:"*) ;;
  *)
    export PATH="$INSTALL_DIR:$PATH"
    LINE="export PATH=\"$INSTALL_DIR:\$PATH\""
    SHELL_NAME="$(basename "$SHELL")"
    case "$SHELL_NAME" in
      zsh)  RC_FILE="$HOME/.zshrc" ;;
      bash) RC_FILE="$HOME/.bashrc" ;;
      *)    RC_FILE="$HOME/.profile" ;;
    esac
    touch "$RC_FILE"
    echo "$LINE" >> "$RC_FILE"
    echo "Added $INSTALL_DIR to PATH (restart shell to apply)" ;;
esac

echo "Installed unity-cli to $INSTALL_DIR/unity-cli"
"$INSTALL_DIR/unity-cli" version
