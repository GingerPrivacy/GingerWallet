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

  "$helper" "$target"
  echo "    Wrote $target"
}

main() {
  local root
  root="$(find_repo_root)"
  cd "$root"

  update_deps ".#packages.x86_64-linux.default" "deps.nix"

  update_deps ".#packages.x86_64-linux.all" "deps-all.nix"

  echo "==> Done. Commit deps.nix and deps-all.nix"
}

main "$@"
