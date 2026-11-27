#!/usr/bin/env bash
# Builds the release zip: dist/ModernUI.zip, holding one top-level ModernUI/ folder (the
# mod, its fonts, LICENSE and THIRD_PARTY_NOTICES.md), as the content index expects.
# DevBridge (a development tool with a local control server) is never in it.
#
#   scripts/release.sh            build and zip
#   scripts/release.sh --check    also print the zip's contents and the version
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" PATH="$HOME/.dotnet:$PATH"
cd "$REPO"

version=$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' ModernUI/ModernUI.csproj)
echo "ModernUI $version"

# A clean Release build of the mod alone (KSAModTools packages it into Release/ModernUI/).
rm -rf Release/ModernUI ModernUI/bin ModernUI/obj
dotnet build ModernUI/ModernUI.csproj -c Release -nologo -v quiet

stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
cp -R Release/ModernUI "$stage/ModernUI"
cp LICENSE THIRD_PARTY_NOTICES.md "$stage/ModernUI/"

# The zip must hold exactly the mod: refuse anything from DevBridge.
if find "$stage" -iname '*DevBridge*' | grep -q .; then
  echo "release.sh: DevBridge found in the release folder; refusing" >&2
  exit 1
fi
grep -q "^version = \"v$version\"" "$stage/ModernUI/mod.toml" || { echo "release.sh: mod.toml version isn't v$version" >&2; exit 1; }

mkdir -p dist
rm -f dist/ModernUI.zip
(cd "$stage" && find ModernUI -name .DS_Store -delete && zip -qrX "$REPO/dist/ModernUI.zip" ModernUI)
echo "$REPO/dist/ModernUI.zip"

if [[ ${1:-} == --check ]]; then
  unzip -l dist/ModernUI.zip
  shasum -a 256 dist/ModernUI.zip
fi
