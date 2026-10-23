#!/usr/bin/env bash
# Installs the .NET SDK that global.json asks for, from Microsoft's official release metadata.
#
# Why this script exists: the Claude Code cloud image does not ship the .NET SDK (see
# docs/DEVELOPMENT.md, section "Claude Code Cloud"), so `dotnet` is missing until something installs it.
# The version is NOT hard-coded here: it is derived from global.json (sdk.version + rollForward),
# so the script, CI (actions/setup-dotnet with 10.0.x) and the repository stay in step.
#
# What it does (idempotent; a second run only prints the installed version):
#   1. reads sdk.version/rollForward from global.json;
#   2. picks the newest stable SDK of the same major.minor that global.json accepts
#      (rollForward latestFeature/latestMinor/latestMajor) or exactly the pinned one
#      (every other policy);
#   3. downloads the linux-x64 tarball from the URL in the official releases.json and checks
#      its SHA-512 against the hash published there -- a mismatch aborts without installing;
#   4. extracts it to $DOTNET_INSTALL_DIR (default /usr/share/dotnet as root, else ~/.dotnet)
#      and links `dotnet` into /usr/local/bin when that is writable.
#
# An install is only counted as complete when the marker file sdk/<version>/.faranou-complete
# exists; it is written after extraction, so a killed run is repaired by the next one.
# Trust model: the SHA-512 and the URL both come from releases.json on builds.dotnet.microsoft.com
# over HTTPS (https enforced, other hosts refused). It is an integrity check against the published
# metadata, NOT a signature check: trust rests on TLS to that host (in Claude Code cloud sessions
# also on the agent proxy's CA).
# Needs: bash, curl, python3, tar, sha512sum (--check needs only python3) and outbound HTTPS to builds.dotnet.microsoft.com
# (the Claude Code "Trusted" network level allows it). Nothing is written inside the repository.
# Usage: scripts/dev/install-dotnet.sh [--check]    (--check: report only, install nothing)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
global_json="$repo_root/global.json"
check_only=0
die() { echo "install-dotnet: $*" >&2; exit 1; }
case "${1:-}" in
  "") ;;
  --check) check_only=1 ;;
  *) die "unknown argument '$1' (usage: install-dotnet.sh [--check])" ;;
esac
[ -f "$global_json" ] || die "global.json not found at $global_json"
required_tools="python3"
[ "$check_only" = "1" ] || required_tools="python3 curl tar sha512sum"
for tool in $required_tools; do
  command -v "$tool" >/dev/null 2>&1 || die "required tool missing: $tool"
done

case "$(uname -m)" in
  x86_64) rid="linux-x64" ;;
  aarch64|arm64) rid="linux-arm64" ;;
  *) die "unsupported CPU architecture: $(uname -m)" ;;
esac

if [ -n "${DOTNET_INSTALL_DIR:-}" ]; then
  install_dir="$DOTNET_INSTALL_DIR"
elif [ "$(id -u)" = "0" ]; then
  install_dir="/usr/share/dotnet"
else
  install_dir="$HOME/.dotnet"
fi

# Resolve the wanted version from global.json (python: the only JSON parser we can rely on).
resolve() {  # no arguments; reads global.json and the official releases.json
  python3 - "$global_json" <<'PY'
import json, re, sys, urllib.request

global_json = sys.argv[1]
sdk = json.load(open(global_json, encoding="utf-8")).get("sdk", {})
pinned = sdk.get("version")
if not pinned:
    sys.exit("global.json has no sdk.version")
roll = sdk.get("rollForward", "latestPatch")
allow_pre = bool(sdk.get("allowPrerelease", False))
m = re.match(r"^(\d+)\.(\d+)\.(\d+)(-.+)?$", pinned)
if not m:
    sys.exit(f"cannot parse sdk.version '{pinned}'")
major, minor, patch = int(m[1]), int(m[2]), int(m[3])
band = patch // 100

def key(v):
    p = re.match(r"^(\d+)\.(\d+)\.(\d+)", v)
    return tuple(int(x) for x in p.groups())

url = f"https://builds.dotnet.microsoft.com/dotnet/release-metadata/{major}.{minor}/releases.json"
req = urllib.request.Request(url, headers={"User-Agent": "faranou-install-dotnet"})
import time
data = None
for attempt in range(3):
    try:
        data = json.load(urllib.request.urlopen(req, timeout=60))
        break
    except Exception as exc:  # network flake: retry, then fail with the last error
        if attempt == 2:
            sys.exit(f"cannot read {url}: {exc}")
        time.sleep(2 * (attempt + 1))
sdks = {}
for rel in data.get("releases", []):
    for s in ([rel.get("sdk")] if rel.get("sdk") else []) + (rel.get("sdks") or []):
        if s and s.get("version"):
            sdks[s["version"]] = s

def stable(v):
    return allow_pre or "-" not in v

if roll in ("latestFeature", "latestMinor", "latestMajor"):
    cands = [v for v in sdks if stable(v) and key(v)[:2] == (major, minor) and key(v)[2] // 100 >= band and key(v) >= key(pinned)]
elif roll == "latestPatch":
    cands = [v for v in sdks if stable(v) and key(v)[:2] == (major, minor) and key(v)[2] // 100 == band and key(v) >= key(pinned)]
else:  # patch, feature, minor, major, disable: take exactly what is pinned
    cands = [pinned] if pinned in sdks else []
if not cands:
    sys.exit(f"no published SDK satisfies global.json ({pinned}, rollForward={roll})")
best = max(cands, key=key)
print(best)
PY
}

version="$(resolve)" || die "could not resolve the SDK version from global.json"

link_dotnet() {  # make `dotnet` reachable; also repairs a cache that kept the SDK but not the link
  [ -d /usr/local/bin ] && [ -w /usr/local/bin ] && [ "$install_dir/dotnet" != "/usr/local/bin/dotnet" ] || return 0
  if [ -e /usr/local/bin/dotnet ] && [ ! -L /usr/local/bin/dotnet ]; then
    echo "install-dotnet: /usr/local/bin/dotnet exists and is not a link; left untouched" >&2
    return 0
  fi
  ln -sf "$install_dir/dotnet" /usr/local/bin/dotnet
}

asked="$(python3 -c 'import json,sys;s=json.load(open(sys.argv[1])).get("sdk",{});print(s.get("version"),"rollForward",s.get("rollForward","latestPatch"))' "$global_json")"
installed="no"
[ -f "$install_dir/sdk/$version/.faranou-complete" ] && [ -x "$install_dir/dotnet" ] && installed="yes"
echo "install-dotnet: global.json asks for $asked; newest accepted SDK is $version; complete install in $install_dir: $installed"
if [ "$installed" = "yes" ]; then
  [ "$check_only" = "1" ] || link_dotnet
  exit 0
fi
[ "$check_only" = "1" ] && { echo "install-dotnet: --check given, nothing installed"; exit 3; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
major_minor="$(echo "$version" | cut -d. -f1,2)"
meta_url="https://builds.dotnet.microsoft.com/dotnet/release-metadata/$major_minor/releases.json"
curl -fsSL --proto '=https' --proto-redir '=https' --retry 3 --max-time 120 "$meta_url" -o "$work/releases.json" || die "cannot download $meta_url"

read -r url expected < <(python3 - "$work/releases.json" "$version" "$rid" <<'PY'
import json, re, sys
path, version, rid = sys.argv[1:4]
data = json.load(open(path, encoding="utf-8"))
for rel in data["releases"]:
    for s in ([rel.get("sdk")] if rel.get("sdk") else []) + (rel.get("sdks") or []):
        if s and s.get("version") == version:
            for f in s.get("files", []):
                if f.get("rid") == rid and f.get("name", "").endswith(".tar.gz"):
                    url, digest = f["url"], f["hash"].lower()
                    if not url.startswith("https://builds.dotnet.microsoft.com/") or not re.fullmatch(r"[0-9a-f]{128}", digest):
                        sys.exit(f"refusing unexpected download entry for SDK {version}")
                    print(url, digest)
                    sys.exit(0)
sys.exit(f"no {rid} tarball for SDK {version} in releases.json")
PY
) || die "release metadata has no download for SDK $version ($rid)"

echo "install-dotnet: downloading $url"
curl -fsSL --proto '=https' --proto-redir '=https' --retry 3 --max-time 240 "$url" -o "$work/sdk.tar.gz" || die "download failed"
actual="$(sha512sum "$work/sdk.tar.gz" | cut -d' ' -f1)"
[ "$actual" = "$expected" ] || die "SHA-512 mismatch (expected $expected, got $actual); nothing installed"
echo "install-dotnet: SHA-512 matches the value published in releases.json"

mkdir -p "$install_dir"
# One installer at a time (the SessionStart hook and the environment's Setup script can overlap).
if command -v flock >/dev/null 2>&1; then
  exec 9>"$install_dir/.faranou-install.lock"
  flock 9
  # Another run may have finished while we waited.
  if [ -f "$install_dir/sdk/$version/.faranou-complete" ] && [ -x "$install_dir/dotnet" ]; then
    link_dotnet
    echo "install-dotnet: SDK $version was installed by a concurrent run"
    exit 0
  fi
fi
tar --no-same-owner --no-same-permissions -xzf "$work/sdk.tar.gz" -C "$install_dir"
[ -f "$install_dir/sdk/$version/dotnet.dll" ] || die "extraction finished but $install_dir/sdk/$version is incomplete"
: >"$install_dir/sdk/$version/.faranou-complete"
link_dotnet
echo "install-dotnet: installed SDK $version in $install_dir"
"$install_dir/dotnet" --list-sdks
