#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
stage="$root/artifacts/WendysDash-1.1.0-linux-x64"
dotnet run --project "$root/linux/tests/WendysDash.Linux.Tests.csproj" -c Release
dotnet publish "$root/linux/WendysDash.Linux.csproj" -c Release -r linux-x64 --self-contained true -o "$stage" -p:DebugType=None -p:DebugSymbols=false
cp "$root/LICENSE" "$root/docs/STEAM_DECK.md" "$stage/"
cp "$root/linux/THIRD_PARTY_NOTICES.md" "$stage/"
cp "$root/linux/Launch-WendysDash.sh" "$stage/"
chmod +x "$stage/WendysDash" "$stage/Launch-WendysDash.sh"
if command -v xvfb-run >/dev/null 2>&1; then
    xvfb-run -a "$stage/WendysDash" --smoke-test "$root/artifacts/linux-gui.png"
fi
tar -C "$root/artifacts" -czf "$root/artifacts/WendysDash-1.1.0-linux-x64.tar.gz" "WendysDash-1.1.0-linux-x64"
(cd "$root/artifacts" && sha256sum WendysDash-1.1.0-linux-x64.tar.gz > SHA256SUMS-linux.txt)
echo "Packaged $root/artifacts/WendysDash-1.1.0-linux-x64.tar.gz"
