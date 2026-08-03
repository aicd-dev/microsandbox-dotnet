#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 || $# -gt 2 ]]; then
  echo "usage: $0 PACKAGE managed|release" >&2
  exit 2
fi

package=$1
mode=$2
[[ -f "$package" ]] || { echo "package not found: $package" >&2; exit 1; }
command -v unzip >/dev/null 2>&1 || { echo "unzip is required" >&2; exit 1; }
entries=$(unzip -Z1 "$package")
repo_root=$(cd "$(dirname "$0")/.." && pwd)
expected_version=$(sed -n 's#.*<Version>\([^<]*\)</Version>.*#\1#p' "$repo_root/src/Microsandbox/Microsandbox.csproj")

require_entry() {
  grep -Fxq "$1" <<< "$entries" || { echo "package is missing $1" >&2; exit 1; }
}

require_entry README.md
require_entry LICENSE
require_entry Withakay.Microsandbox.nuspec
require_entry lib/net8.0/Withakay.Microsandbox.dll
require_entry lib/net8.0/Withakay.Microsandbox.xml

nuspec=$(unzip -p "$package" Withakay.Microsandbox.nuspec)
grep -Fq '<id>Withakay.Microsandbox</id>' <<< "$nuspec" || {
  echo "package nuspec has an unexpected package id" >&2
  exit 1
}
grep -Fq "<version>$expected_version</version>" <<< "$nuspec" || {
  echo "package nuspec version does not match project version $expected_version" >&2
  exit 1
}

case "$mode" in
  managed)
    if grep -q '^runtimes/' <<< "$entries"; then
      echo "managed-only package unexpectedly contains native runtime assets" >&2
      exit 1
    fi
    ;;
  release)
    require_entry runtimes/linux-x64/native/libmicrosandbox_go_ffi.so
    require_entry runtimes/linux-arm64/native/libmicrosandbox_go_ffi.so
    require_entry runtimes/osx-arm64/native/libmicrosandbox_go_ffi.dylib
    require_entry runtimes/win-x64/native/microsandbox_go_ffi.dll
    require_entry runtimes/win-arm64/native/microsandbox_go_ffi.dll
    runtime_count=$(grep -c '^runtimes/' <<< "$entries" || true)
    if [[ "$runtime_count" -ne 5 ]]; then
      echo "release package must contain exactly the five expected native runtime files" >&2
      exit 1
    fi
    ;;
  *)
    echo "mode must be managed or release" >&2
    exit 2
    ;;
esac

echo "verified $mode package: $package"
