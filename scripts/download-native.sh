#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "$0")/.." && pwd)
project="$repo_root/src/Microsandbox/Microsandbox.csproj"
default_version=$(sed -n 's#.*<Version>\([^<]*\)</Version>.*#\1#p' "$project")
version=${1:-$default_version}
download_dir=${2:-"$repo_root/artifacts/upstream/v$version"}
runtimes_dir=${3:-"$repo_root/src/Microsandbox/runtimes"}
tag="v$version"
upstream="superradcompany/microsandbox"
allowlist=${MICROSANDBOX_NATIVE_ALLOWLIST:-"$repo_root/native-assets.sha256"}
assets=(
  checksums.sha256
  libmicrosandbox_go_ffi-linux-amd64.so
  libmicrosandbox_go_ffi-linux-arm64.so
  libmicrosandbox_go_ffi-darwin-arm64.dylib
  libmicrosandbox_go_ffi-windows-amd64.dll
  libmicrosandbox_go_ffi-windows-arm64.dll
)

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "invalid upstream release version: $version" >&2
  exit 2
fi

[[ -f "$allowlist" ]] || { echo "native digest allowlist not found: $allowlist" >&2; exit 1; }
allowlist_version=$(sed -n 's/^version \([^[:space:]]*\)$/\1/p' "$allowlist")
if [[ "$allowlist_version" != "$version" ]]; then
  echo "native digest allowlist version $allowlist_version does not match requested release $version" >&2
  exit 1
fi

if command -v sha256sum >/dev/null 2>&1; then
  hash_file() { sha256sum "$1" | tr '[:upper:]' '[:lower:]' | cut -d ' ' -f 1; }
elif command -v shasum >/dev/null 2>&1; then
  hash_file() { shasum -a 256 "$1" | tr '[:upper:]' '[:lower:]' | cut -d ' ' -f 1; }
else
  echo "sha256sum or shasum is required" >&2
  exit 1
fi

allowlisted_hash() {
  local wanted=$1
  local line hash filename found=""
  while IFS= read -r line || [[ -n "$line" ]]; do
    line=${line%$'\r'}
    if [[ "$line" =~ ^([[:xdigit:]]{64})[[:space:]]+\*?(.+)$ ]]; then
      hash=${BASH_REMATCH[1]}
      filename=${BASH_REMATCH[2]}
      if [[ "$filename" == "$wanted" ]]; then
        [[ -z "$found" ]] || { echo "duplicate allowlist entry for $wanted" >&2; return 1; }
        found=$(printf '%s' "$hash" | tr '[:upper:]' '[:lower:]')
      fi
    fi
  done < "$allowlist"
  [[ -n "$found" ]] || { echo "missing allowlist entry for $wanted" >&2; return 1; }
  printf '%s\n' "$found"
}

install -d "$(dirname "$download_dir")"
temp_dir=$(mktemp -d "$(dirname "$download_dir")/.native-download.XXXXXX")
cleanup() { [[ -z "$temp_dir" ]] || rm -rf "$temp_dir"; }
trap cleanup EXIT HUP INT TERM

if [[ "${GITHUB_ACTIONS:-}" == "true" ]] && command -v gh >/dev/null 2>&1; then
  args=(release download "$tag" --repo "$upstream" --dir "$temp_dir")
  for asset in "${assets[@]}"; do
    args+=(--pattern "$asset")
  done
  gh "${args[@]}"
else
  command -v curl >/dev/null 2>&1 || { echo "curl is required outside GitHub Actions" >&2; exit 1; }
  base="https://github.com/$upstream/releases/download/$tag"
  for asset in "${assets[@]}"; do
    curl --fail --location --retry 3 --retry-all-errors --output "$temp_dir/$asset" "$base/$asset"
  done
fi

for asset in "${assets[@]}"; do
  [[ -s "$temp_dir/$asset" ]] || { echo "upstream release $tag is missing $asset" >&2; exit 1; }
done

allowlist_entries=$(grep -Ec '^[[:xdigit:]]{64}[[:space:]]+' "$allowlist" || true)
[[ "$allowlist_entries" -eq 5 ]] || { echo "native digest allowlist must contain exactly five assets" >&2; exit 1; }
for asset in "${assets[@]:1}"; do
  expected=$(allowlisted_hash "$asset")
  actual=$(hash_file "$temp_dir/$asset")
  [[ "$actual" == "$expected" ]] || { echo "committed digest mismatch for $asset" >&2; exit 1; }
done

install -d "$download_dir"
for asset in "${assets[@]}"; do
  install -m 0644 "$temp_dir/$asset" "$download_dir/$asset"
done
"$repo_root/scripts/stage-native.sh" "$download_dir" "$version" "$runtimes_dir"
echo "downloaded and staged native assets from $upstream $tag"
