#!/usr/bin/env bash
set -euo pipefail
umask 077

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <nv-index-offset> <platform-auth-file> <nv-index-auth-file>" >&2
  exit 2
fi

offset="$1"
platform_auth_file="$2"
nv_auth_file="$3"

[[ "$offset" =~ ^(0x[0-9a-fA-F]+|[0-9]+)$ ]] || { echo "Invalid NV index offset." >&2; exit 2; }
if [[ "$offset" == 0x* ]]; then
  numeric_offset=$((offset))
else
  numeric_offset=$((10#$offset))
fi
(( numeric_offset >= 0x1000 && numeric_offset <= 0x00FFFFFF )) || {
  echo "NV index offset must be between 0x1000 and 0x00FFFFFF." >&2
  exit 2
}
[[ -r "$platform_auth_file" ]] || { echo "Platform authorization file is not readable." >&2; exit 2; }
[[ -r "$nv_auth_file" ]] || { echo "NV index authorization file is not readable." >&2; exit 2; }
[[ -c /dev/tpmrm0 ]] || { echo "Local TPM resource-manager device /dev/tpmrm0 is unavailable." >&2; exit 1; }

command -v tpm2_nvdefine >/dev/null
command -v tpm2_nvread >/dev/null
command -v tpm2_nvreadpublic >/dev/null

# Explicit local TCTI: never use a TCP simulator or automatic device fallback.
export TPM2TOOLS_TCTI="device:/dev/tpmrm0"

tmpdir="$(mktemp -d /dev/shm/faranou-tpm-nv.XXXXXX)"
trap 'rm -rf "$tmpdir"' EXIT
readback="$tmpdir/readback.bin"
zeros="$tmpdir/zeros.bin"
dd if=/dev/zero of="$zeros" bs=32 count=1 status=none

# Create-only: an existing index causes TPM_RC_NV_DEFINED and is never removed or reset.
# Platform hierarchy creation sets TPMA_NV_PLATFORMCREATE. The runtime provider verifies it.
tpm2_nvdefine \
  -C p \
  -P "file:$platform_auth_file" \
  -p "file:$nv_auth_file" \
  -s 32 \
  -g sha256 \
  -a "nt=extend|authread|authwrite" \
  "$offset"

tpm2_nvreadpublic "$offset" >/dev/null
tpm2_nvread -C "$offset" -P "file:$nv_auth_file" -s 32 "$offset" > "$readback"

if [[ "$(wc -c < "$readback")" -ne 32 ]] || ! cmp -s "$zeros" "$readback"; then
  echo "Provisioned TPM NV Extend index did not read back as 32 zero bytes." >&2
  exit 1
fi

echo "Provisioned and verified TPM 2.0 NV Extend index offset $offset."
echo "Verified initial value: 32 zero bytes. Runtime will verify SHA-256, Extend, AuthRead, AuthWrite and PlatformCreate."
echo "Record TPM identity and index offset only; never copy authorization values into logs or inventory."
