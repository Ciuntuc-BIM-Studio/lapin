#!/usr/bin/env bash
set -euo pipefail

# The Linux desktop app (.deb and .AppImage) of one product, built on this
# machine in an Ubuntu 22.04 x64 container: the same steps as the Build Linux
# workflow, for when Actions is not available. Needs Docker (OrbStack, Docker
# Desktop or Colima); on Apple Silicon the container runs x64 under emulation,
# so it is slower than Actions.
#   scripts/build-linux-docker.sh [product] [ref]     (default: lapin, origin/main)
# The packages land in build/<product>-linux/. The toolchains, the packages'
# caches, Rust's target and manifold's library are kept in Docker volumes, so
# a second build is quicker. The build is of a clean clone of <ref>: what is
# not committed is not in it.

product="${1:-lapin}"
ref="${2:-origin/main}"
root="$(cd "$(dirname "$0")/.." && pwd)"
image="lapin-linux-build:22.04"
out="${root}/build/${product}-linux"

command -v docker >/dev/null || { echo "no docker: install OrbStack (brew install --cask orbstack), Docker Desktop or Colima" >&2; exit 1; }
git -C "${root}" fetch -q origin || true
commit="$(git -C "${root}" rev-parse "${ref}")"

docker build --platform linux/amd64 -t "${image}" - <<'DOCKERFILE'
FROM ubuntu:22.04
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
      ca-certificates curl git build-essential pkg-config file xz-utils \
      libwebkit2gtk-4.1-dev libgtk-3-dev libayatana-appindicator3-dev librsvg2-dev patchelf cmake libssl-dev \
      libicu70 \
 && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
 && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && apt-get install -y nodejs && rm -rf /var/lib/apt/lists/* \
 && npm install -g corepack@latest && corepack enable
RUN curl -fsSL https://sh.rustup.rs | sh -s -- -y --profile minimal --default-toolchain stable
ENV PATH=/root/.cargo/bin:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 COREPACK_ENABLE_DOWNLOAD_PROMPT=0
DOCKERFILE

mkdir -p "${out}"
docker run --rm --platform linux/amd64 \
  -v "${root}/.git:/repo.git:ro" \
  -v "${out}:/out" \
  -v lapin-linux-cargo:/root/.cargo/registry \
  -v lapin-linux-target:/target \
  -v lapin-linux-pnpm:/root/.local/share/pnpm \
  -v lapin-linux-nuget:/root/.nuget/packages \
  -v lapin-linux-native:/native \
  -e PRODUCT="${product}" -e COMMIT="${commit}" -e CI=true -e APPIMAGE_EXTRACT_AND_RUN=1 \
  "${image}" bash -euo pipefail -c '
    git config --global --add safe.directory "*"
    git clone -q /repo.git /work && cd /work && git checkout -q --detach "$COMMIT"
    echo "▸ $(git log --oneline -1)"
    ln -s /target desktop/src-tauri/target
    # The booleans: manifold built once, kept in a volume.
    if [ ! -f /native/libmanifoldc.so ]; then node scripts/build-manifold.mjs && cp engine/native/linux-x64/libmanifoldc.so /native/; fi
    mkdir -p engine/native/linux-x64 && cp /native/libmanifoldc.so engine/native/linux-x64/
    pnpm install --frozen-lockfile
    dotnet build engine/Lapin.slnx -v q -nologo
    dotnet test engine/tests/Lapin.Engine.Tests -v q -nologo
    pnpm build:product "$PRODUCT"
    rm -f /out/*.deb /out/*.AppImage
    cp build/"$PRODUCT"/bundle/deb/*.deb build/"$PRODUCT"/bundle/appimage/*.AppImage /out/
    ls -la /out
  '
echo "✓ ${out}"
