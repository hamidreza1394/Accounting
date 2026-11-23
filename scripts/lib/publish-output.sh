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
#
# After a real publish, faranou_assert_customer_publish_clean (G2 / D-041) refuses Vendor
# assemblies and private-key material in the customer package.

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

# G2 (D-041): refuse Vendor assemblies and private-key material in the customer publish output.
# Called after a real dotnet publish. Patterns are intentional and narrow:
#   - assembly / native name Vendor.* (case-insensitive on the file name)
#   - sensitive extensions: .pfx .p12 .key (any path)
#   - PEM private-key headers in text files (BEGIN ... PRIVATE KEY)
# Does not block ordinary customer DLLs, appsettings, or public certificates without private key.
# Uses `command grep` so a shell alias (e.g. ugrep) cannot reinterpret the pattern.
#
# The same function also enforces the D-041 G2 allow-list of the customer package
# (scripts/lib/publish-assembly-allowlist.txt, same directory as this lib): every managed or
# native binary (.dll/.exe/.so/.dylib and the extension-less apphost) outside wwwroot/ must
# be explicitly listed; anything unlisted is refused fail-closed. Non-binary root files are
# not assemblies and are skipped here; the PEM scan above still inspects their content.
FARANOU_PUBLISH_ALLOWLIST="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/publish-assembly-allowlist.txt"

faranou_assert_customer_publish_clean() {
  local output="$1"
  local found=0
  local rel f

  if [ ! -d "$output" ]; then
    echo "publish: G2 content guard: output directory does not exist: $output" >&2
    return 1
  fi

  while IFS= read -r f; do
    [ -z "$f" ] && continue
    rel="${f#"$output"/}"
    echo "publish: G2 content guard: forbidden Vendor assembly or library: $rel" >&2
    found=1
  done <<FIND_VENDOR
$(find "$output" -type f \( -iname 'Vendor.*.dll' -o -iname 'Vendor.*.exe' -o -iname 'Vendor.*.so' -o -iname 'Vendor.*.dylib' -o -iname 'Vendor.*.pdb' \) 2>/dev/null)
FIND_VENDOR

  while IFS= read -r f; do
    [ -z "$f" ] && continue
    rel="${f#"$output"/}"
    echo "publish: G2 content guard: forbidden sensitive file: $rel" >&2
    found=1
  done <<FIND_SENSITIVE
$(find "$output" -type f \( -iname '*.pfx' -o -iname '*.p12' -o -iname '*.key' \) 2>/dev/null)
FIND_SENSITIVE

  while IFS= read -r f; do
    [ -z "$f" ] && continue
    case "$f" in
      *.dll|*.exe|*.so|*.dylib|*.pdb|*.png|*.jpg|*.jpeg|*.gif|*.woff|*.woff2|*.ttf|*.eot|*.ico|*.zip|*.nupkg) continue ;;
    esac
    if command grep -qE -- '-----BEGIN ([A-Z0-9 ]+)?PRIVATE KEY-----' "$f" 2>/dev/null; then
      rel="${f#"$output"/}"
      echo "publish: G2 content guard: private key material detected in: $rel" >&2
      found=1
    fi
  done <<FIND_TEXT
$(find "$output" -type f -size -512k 2>/dev/null)
FIND_TEXT

  # --- allow-list of the whole package (fail-closed on anything unlisted) -----------------
  local -A root_ok sat_ok cult_ok run_ok
  local line kind value base ext
  if [ ! -r "$FARANOU_PUBLISH_ALLOWLIST" ]; then
    echo "publish: G2 content guard: allow-list file missing or unreadable: $FARANOU_PUBLISH_ALLOWLIST" >&2
    return 1
  fi
  while IFS= read -r line; do
    line="${line%$'\r'}"
    case "$line" in ''|'#'*) continue ;; esac
    kind="${line%%:*}"
    value="${line#*:}"
    value="${value#"${value%%[! ]*}"}"
    value="${value%"${value##*[! ]}"}"
    case "$kind" in
      root) root_ok["${value,,}"]=1 ;;
      satellite) sat_ok["${value,,}"]=1 ;;
      culture) cult_ok["${value,,}"]=1 ;;
      runtime) run_ok["${value,,}"]=1 ;;
      *) echo "publish: G2 content guard: malformed allow-list entry: $line" >&2; return 1 ;;
    esac
  done < "$FARANOU_PUBLISH_ALLOWLIST"

  while IFS= read -r f; do
    [ -z "$f" ] && continue
    rel="${f#"$output"/}"
    case "$rel" in wwwroot/*) continue ;; esac
    base="${rel##*/}"
    case "$base" in .faranou-publish-output) continue ;; esac
    case "$rel" in
      runtimes/*)
        if [ -z "${run_ok["${rel,,}"]:-}" ]; then
          echo "publish: G2 content guard: file outside the D-041 publish allow-list: $rel" >&2
          found=1
        fi
        continue ;;
    esac
    if [[ "$rel" == */* ]]; then
      # Nested (not runtimes/, not wwwroot/): only an allowed satellite
      # <culture>/<base>.resources.dll may live here.
      local dir="${rel%%/*}"
      if [ -n "${cult_ok["${dir,,}"]:-}" ] && [[ "$base" == *.resources.dll ]]; then
        value="${base%.resources.dll}"
        if [ -n "${sat_ok["${value,,}"]:-}" ]; then continue; fi
      fi
      echo "publish: G2 content guard: file outside the D-041 publish allow-list: $rel" >&2
      found=1
      continue
    fi
    # Root level: binaries and the extension-less apphost must be allow-listed.
    if [[ "$base" == *.* ]]; then
      ext="${base##*.}"
      case "${ext,,}" in
        dll|exe|so|dylib)
          if [ -z "${root_ok["${base,,}"]:-}" ]; then
            echo "publish: G2 content guard: file outside the D-041 publish allow-list: $rel" >&2
            found=1
          fi ;;
      esac
    else
      if [ -z "${root_ok["${base,,}"]:-}" ]; then
        echo "publish: G2 content guard: file outside the D-041 publish allow-list: $rel" >&2
        found=1
      fi
    fi
  done <<FIND_ALL
$(find "$output" -type f 2>/dev/null)
FIND_ALL

  if [ "$found" -ne 0 ]; then
    echo "publish: G2 content guard failed (D-041): customer package must not contain Vendor assemblies or private keys" >&2
    return 1
  fi
  return 0
}
