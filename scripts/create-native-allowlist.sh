#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: $0 UPSTREAM_CHECKSUM_FILE RELEASE_VERSION OUTPUT_FILE" >&2
  exit 2
fi

checksum_file=$1
version=$2
output_file=$3
assets=(
  libmicrosandbox_go_ffi-darwin-arm64.dylib
  libmicrosandbox_go_ffi-linux-amd64.so
  libmicrosandbox_go_ffi-linux-arm64.so
  libmicrosandbox_go_ffi-windows-amd64.dll
  libmicrosandbox_go_ffi-windows-arm64.dll
)

[[ -f "$checksum_file" ]] || { echo "checksum manifest not found: $checksum_file" >&2; exit 1; }
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]] || {
  echo "invalid release version: $version" >&2
  exit 2
}

extract_hash() {
  local wanted=$1
  local line hash filename found=""
  while IFS= read -r line || [[ -n "$line" ]]; do
    line=${line%$'\r'}
    if [[ "$line" =~ ^([[:xdigit:]]{64})[[:space:]]+\*?(.+)$ ]]; then
      hash=${BASH_REMATCH[1]}
      filename=${BASH_REMATCH[2]}
      if [[ "$filename" == "$wanted" ]]; then
        [[ -z "$found" ]] || { echo "duplicate checksum entry for $wanted" >&2; return 1; }
        found=$(printf '%s' "$hash" | tr '[:upper:]' '[:lower:]')
      fi
    fi
  done < "$checksum_file"
  [[ -n "$found" ]] || { echo "missing checksum entry for $wanted" >&2; return 1; }
  printf '%s\n' "$found"
}

install -d "$(dirname "$output_file")"
temp_file="$output_file.tmp.$$"
cleanup() { rm -f "$temp_file"; }
trap cleanup EXIT HUP INT TERM
printf 'version %s\n' "$version" > "$temp_file"
for asset in "${assets[@]}"; do
  printf '%s  %s\n' "$(extract_hash "$asset")" "$asset" >> "$temp_file"
done
mv "$temp_file" "$output_file"
trap - EXIT HUP INT TERM
echo "created native digest allowlist for release $version: $output_file"
