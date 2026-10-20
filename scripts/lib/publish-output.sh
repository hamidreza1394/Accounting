#!/usr/bin/env bash
# Validation and preparation of the publish output directory (sourced by scripts/publish.sh).
#
# The directory is deleted and re-created by the publish, so it is validated first and
# nothing is deleted unless every check passes:
#   * the path is made absolute and canonical (".." removed, symbolic links resolved);
#   * neither the path nor any existing ancestor may be a symbolic link (pass the real path);
#   * it must not be the file-system root, the repository root, the home directory, the
#     current directory, or an ancestor of any of them;
#   * inside the repository only artifacts/<something> is allowed (sources are never targets);
#   * an existing directory is only replaced when it is empty or carries the marker file
#     written by an earlier publish (.faranou-publish-output); an existing file is refused.
# The same rules are implemented in scripts/publish.ps1 (Resolve-FaranouPublishOutput).

FARANOU_PUBLISH_MARKER=".faranou-publish-output"

faranou_fail() {
  echo "publish: refusing output directory: $1" >&2
  return 1
}

# faranou_is_same_or_ancestor <candidate> <path>: true when candidate == path or contains it.
faranou_is_same_or_ancestor() {
  local candidate="$1" path="$2"
  [ "$candidate" = "/" ] && return 0
  [ "$path" = "$candidate" ] && return 0
  case "$path/" in "$candidate"/*) return 0 ;; esac
  return 1
}

# faranou_resolve_output <requested> <repo-root> -> prints the canonical, validated path.
faranou_resolve_output() {
  local requested="$1" repo_root home current canonical
  repo_root="$(cd "$2" && pwd -P)"

  if [ -z "${requested// /}" ]; then
    faranou_fail "the path is empty"; return 1
  fi

  case "$requested" in /*) ;; *) requested="$(pwd -P)/$requested" ;; esac

  local probe="${requested%/}"
  while [ -n "$probe" ] && [ "$probe" != "/" ]; do
    if [ -L "$probe" ]; then
      faranou_fail "'$probe' is a symbolic link"; return 1
    fi
    probe="$(dirname -- "$probe")"
  done

  canonical="$(realpath -m -- "$requested")" || { faranou_fail "'$requested' can not be resolved"; return 1; }
  home="$(realpath -m -- "${HOME:-/}")"
  current="$(pwd -P)"

  if [ "$canonical" = "/" ]; then
    faranou_fail "'$canonical' is the file-system root"; return 1
  fi
  if faranou_is_same_or_ancestor "$canonical" "$repo_root"; then
    faranou_fail "'$canonical' is the repository root or contains it"; return 1
  fi
  if faranou_is_same_or_ancestor "$canonical" "$home"; then
    faranou_fail "'$canonical' is the home directory or contains it"; return 1
  fi
  if faranou_is_same_or_ancestor "$canonical" "$current"; then
    faranou_fail "'$canonical' is the current directory or contains it"; return 1
  fi
  case "$canonical/" in
    "$repo_root"/artifacts/?*) ;;
    "$repo_root"/*) faranou_fail "'$canonical' is inside the repository but not under artifacts/"; return 1 ;;
  esac

  if [ -e "$canonical" ] && [ ! -d "$canonical" ]; then
    faranou_fail "'$canonical' exists and is not a directory"; return 1
  fi
  if [ -d "$canonical" ] && [ -n "$(ls -A -- "$canonical")" ] && [ ! -f "$canonical/$FARANOU_PUBLISH_MARKER" ]; then
    faranou_fail "'$canonical' is not empty and was not created by a publish (no $FARANOU_PUBLISH_MARKER)"; return 1
  fi

  printf '%s\n' "$canonical"
}

# faranou_prepare_output <validated-canonical-path>: empties/creates it and writes the marker.
faranou_prepare_output() {
  local output="$1"
  rm -rf -- "$output"
  mkdir -p -- "$output"
  printf 'Created by scripts/publish.sh; the whole directory is replaced by the next publish.\n' > "$output/$FARANOU_PUBLISH_MARKER"
}
