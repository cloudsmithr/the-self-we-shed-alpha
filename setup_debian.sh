#!/usr/bin/env bash
# =============================================================================
# setup-dev.sh — Dev environment for fortress game (Debian)
#
# Installs:
#   - .NET 10 SDK          (API server)
#   - Go 1.26.1              (Worker)
#   - nvm + Node.js 24    (React frontend)
#   - Docker + Compose     (local dev containers)
#
# Usage:
#   chmod +x setup-dev.sh && ./setup-dev.sh
#
# Safe to re-run. Each section checks before installing.
# =============================================================================

set -euo pipefail

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------
info()    { echo -e "\n\033[1;34m==>\033[0m $*"; }
success() { echo -e "\033[1;32m✓\033[0m $*"; }

# -----------------------------------------------------------------------------
# 1. .NET 10 SDK
# -----------------------------------------------------------------------------
info ".NET 10 SDK"

if dotnet --version 2>/dev/null | grep -q "^10\."; then
  success ".NET 10 already installed ($(dotnet --version))"
else
  DEBIAN_VERSION="$(. /etc/os-release && echo "$VERSION_ID")"
  wget "https://packages.microsoft.com/config/debian/${DEBIAN_VERSION}/packages-microsoft-prod.deb" -O packages-microsoft-prod.deb
  sudo dpkg -i packages-microsoft-prod.deb
  rm packages-microsoft-prod.deb
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-10.0

  success ".NET $(dotnet --version) installed"
fi

# -----------------------------------------------------------------------------
# 2. Go 1.26.1
# -----------------------------------------------------------------------------
info "Go 1.26.1"

GO_VERSION="1.26.1"
GO_TARBALL="go${GO_VERSION}.linux-amd64.tar.gz"
GO_URL="https://go.dev/dl/${GO_TARBALL}"
GO_INSTALL_DIR="/usr/local"
GO_BIN="${GO_INSTALL_DIR}/go/bin/go"
PROFILE_LINE='export PATH="$PATH:/usr/local/go/bin"'

for rc in "$HOME/.bashrc" "$HOME/.profile"; do
  grep -qxF "${PROFILE_LINE}" "${rc}" 2>/dev/null || echo "${PROFILE_LINE}" >> "${rc}"
done

export PATH="$PATH:/usr/local/go/bin"

if [ -x "${GO_BIN}" ] && "${GO_BIN}" version | grep -q "go${GO_VERSION}"; then
  success "Go ${GO_VERSION} already installed"
else
  wget -q --show-progress "${GO_URL}" -O "/tmp/${GO_TARBALL}"
  sudo rm -rf "${GO_INSTALL_DIR}/go"
  sudo tar -C "${GO_INSTALL_DIR}" -xzf "/tmp/${GO_TARBALL}"
  rm "/tmp/${GO_TARBALL}"

  success "Go $(go version) installed"
fi

# -----------------------------------------------------------------------------
# 3. nvm + Node.js LTS
# -----------------------------------------------------------------------------
info "nvm + Node.js 24"

export NVM_DIR="$HOME/.nvm"

if [ -s "$NVM_DIR/nvm.sh" ]; then
  success "nvm already installed"
else
  curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.4/install.sh | bash
  success "nvm installed"
fi

# Load nvm in current shell
\. "$HOME/.nvm/nvm.sh"

if nvm version 24 >/dev/null 2>&1; then
  nvm alias default 24 >/dev/null
  nvm use 24 >/dev/null
  success "Node $(node --version) already installed and set as default"
else
  nvm install 24
  nvm alias default 24 >/dev/null
  nvm use 24 >/dev/null
  success "Node $(node --version) installed and set as default"
fi

# -----------------------------------------------------------------------------
# 4. Docker + Compose plugin
# -----------------------------------------------------------------------------
info "Docker"

if docker version &>/dev/null; then
  success "Docker already installed ($(docker --version))"
else
  # Official Docker apt repo
  sudo apt update
  sudo apt install -y ca-certificates curl
  sudo install -m 0755 -d /etc/apt/keyrings
  sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  sudo chmod a+r /etc/apt/keyrings/docker.asc

sudo tee /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: $(. /etc/os-release && echo "$VERSION_CODENAME")
Components: stable
Signed-By: /etc/apt/keyrings/docker.asc
EOF

  sudo apt update
  sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  sudo systemctl start docker

  # Allow running docker without sudo
  sudo usermod -aG docker "$USER"

  success "Docker $(docker --version) installed"
  echo "  NOTE: Log out and back in (or run 'newgrp docker') for group changes to take effect"
fi

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
echo ""
echo -e "\033[1;32m=== All done ===\033[0m"
echo "  .NET:   $(dotnet --version 2>/dev/null || echo 'restart shell to verify')"
echo "  Go:     $(go version 2>/dev/null | awk '{print $3}' || echo 'restart shell to verify')"
echo "  Node:   $(node --version 2>/dev/null || echo 'restart shell to verify')"
echo "  Docker: $(docker --version 2>/dev/null | awk '{print $3}' | tr -d ',' || echo 'restart shell to verify')"
echo ""
echo "If this is a fresh terminal, run: source ~/.bashrc"
