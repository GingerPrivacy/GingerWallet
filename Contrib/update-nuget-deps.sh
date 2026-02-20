#!/usr/bin/env bash
set -euo pipefail

find_repo_root() {
  local d="$PWD"
  while [[ "$d" != "/" ]]; do
    [[ -f "$d/flake.nix" ]] && { echo "$d"; return 0; }
    d="$(dirname "$d")"
  done
  echo "ERROR: Could not find flake.nix in any parent directory." >&2
  exit 1
}

nix_build_flags=(
  --extra-experimental-features nix-command
  --extra-experimental-features flakes
)

update_deps() {
  local attr="$1"        # .#packages.x86_64-linux.default
  local target="$2"      # deps.nix

  echo "==> Updating ${target} from ${attr}.passthru.fetch-deps"

  local helper
  helper="$(nix build "${attr}.passthru.fetch-deps" \
            --no-link --print-out-paths \
            "${nix_build_flags[@]}")"

  if [[ -z "$helper" || ! -e "$helper" ]]; then
    echo "ERROR: nix did not return a valid output path for fetch-deps." >&2
    exit 2
  fi

  local out gen_path
  out="$("$helper" 2>&1 || true)"
  gen_path="$(printf '%s\n' "$out" | sed -nE 's/^Succesfully wrote lockfile to (.*)$/\1/p' | tail -n 1)"

  if [[ -z "$gen_path" ]]; then
    echo "ERROR: Could not find generated lockfile path in fetch-deps output." >&2
    echo "---- fetch-deps output (last 120 lines) ----" >&2
    printf '%s\n' "$out" | tail -n 120 >&2
    exit 3
  fi

  cp -f "$gen_path" "$target"
  echo "    Wrote $target (from $gen_path)"
}

main() {
  local root
  root="$(find_repo_root)"
  cd "$root"

  update_deps ".#packages.x86_64-linux.default" "deps.nix"

  echo "==> Done. Commit deps.nix"
}

main "$@"
