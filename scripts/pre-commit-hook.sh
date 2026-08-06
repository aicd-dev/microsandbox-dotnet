#!/usr/bin/env bash
set -euo pipefail

mapfile -t staged_files < <(git diff --cached --name-only --diff-filter=ACMR)
if ((${#staged_files[@]} > 0)); then
  bash scripts/pre-commit-format.sh "${staged_files[@]}"
fi

exec prek run --hook-stage pre-commit
