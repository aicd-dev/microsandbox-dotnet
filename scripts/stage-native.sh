#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo "usage: $0 RELEASE_ASSET_DIR RELEASE_VERSION [RUNTIMES_DIR]" >&2
  exit 2
fi

source_dir=$1
release_version=$2
target_dir=${3:-"$(cd "$(dirname "$0")/.." && pwd)/src/Microsandbox/runtimes"}
checksum_file="$source_dir/checksums.sha256"

assets=(
  "libmicrosandbox_go_ffi-linux-amd64.so|linux-x64|libmicrosandbox_go_ffi.so|elf"
  "libmicrosandbox_go_ffi-linux-arm64.so|linux-arm64|libmicrosandbox_go_ffi.so|elf"
  "libmicrosandbox_go_ffi-darwin-arm64.dylib|osx-arm64|libmicrosandbox_go_ffi.dylib|macho"
  "libmicrosandbox_go_ffi-windows-amd64.dll|win-x64|microsandbox_go_ffi.dll|mz"
  "libmicrosandbox_go_ffi-windows-arm64.dll|win-arm64|microsandbox_go_ffi.dll|mz"
)

if [[ ! -f "$checksum_file" ]]; then
  echo "missing checksum manifest: $checksum_file" >&2
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

expected_hash() {
  local wanted=$1
  local line hash filename found=""
  while IFS= read -r line || [[ -n "$line" ]]; do
    line=${line%$'\r'}
    if [[ "$line" =~ ^([[:xdigit:]]{64})[[:space:]]+\*?(.+)$ ]]; then
      hash=${BASH_REMATCH[1]}
      filename=${BASH_REMATCH[2]}
      if [[ "$filename" == "$wanted" ]]; then
        if [[ -n "$found" ]]; then
          echo "duplicate checksum entry for $wanted" >&2
          return 1
        fi
        found=$(printf '%s' "$hash" | tr '[:upper:]' '[:lower:]')
      fi
    fi
  done < "$checksum_file"

  if [[ -z "$found" ]]; then
    echo "missing checksum entry for $wanted" >&2
    return 1
  fi
  printf '%s\n' "$found"
}

validate_magic() {
  local path=$1
  local format=$2
  local magic
  magic=$(od -An -tx1 -N4 "$path" | tr -d '[:space:]')
  case "$format:$magic" in
    elf:7f454c46) ;;
    macho:feedface|macho:feedfacf|macho:cefaedfe|macho:cffaedfe|macho:cafebabe|macho:bebafeca|macho:cafebabf|macho:bfbafeca) ;;
    mz:4d5a*) ;;
    *)
      echo "invalid $format binary magic: $path" >&2
      return 1
      ;;
  esac
}

for mapping in "${assets[@]}"; do
  IFS='|' read -r asset rid canonical format <<< "$mapping"
  source_path="$source_dir/$asset"
  if [[ ! -f "$source_path" ]]; then
    echo "missing release asset: $source_path" >&2
    exit 1
  fi

  expected=$(expected_hash "$asset")
  actual=$(hash_file "$source_path")
  if [[ "$actual" != "$expected" ]]; then
    echo "checksum mismatch for $source_path" >&2
    exit 1
  fi
  validate_magic "$source_path" "$format"
done

target_parent=$(dirname "$target_dir")
target_name=$(basename "$target_dir")
install -d "$target_parent"
temp_dir=$(mktemp -d "$target_parent/.${target_name}.stage.XXXXXX")
backup_dir=""

cleanup() {
  local status=$?
  [[ -z "$temp_dir" || ! -e "$temp_dir" ]] || rm -rf "$temp_dir"
  if [[ -n "$backup_dir" && -e "$backup_dir" && ! -e "$target_dir" ]]; then
    mv "$backup_dir" "$target_dir"
  fi
  exit "$status"
}
trap cleanup EXIT HUP INT TERM

for mapping in "${assets[@]}"; do
  IFS='|' read -r asset rid canonical format <<< "$mapping"
  install -d "$temp_dir/$rid/native"
  install -m 0644 "$source_dir/$asset" "$temp_dir/$rid/native/$canonical"
done
printf '%s\n' "$release_version" > "$temp_dir/release-version.txt"

if [[ -e "$target_dir" ]]; then
  backup_dir=$(mktemp -d "$target_parent/.${target_name}.backup.XXXXXX")
  rmdir "$backup_dir"
  mv "$target_dir" "$backup_dir"
fi
mv "$temp_dir" "$target_dir"
temp_dir=""
if [[ -n "$backup_dir" ]]; then
  rm -rf "$backup_dir"
  backup_dir=""
fi
trap - EXIT HUP INT TERM

echo "staged five native runtime assets for release $release_version under $target_dir"
