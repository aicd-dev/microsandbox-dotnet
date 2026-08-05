#!/usr/bin/env bash
set -euo pipefail

for file in "$@"; do
  case "$file" in
    *.cs|*.csproj|*.props|*.targets|*.slnx)
      [[ -f "$file" ]] || continue
      dotnet csharpier format "$file"
      git add -- "$file"
      ;;
  esac
done
