#!/usr/bin/env python3
"""Fail when application-owned markup/styles/scripts load assets from remote CDNs."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SEARCH_ROOTS = [
    ROOT / "Server" / "Pages", ROOT / "Server" / "wwwroot" / "css", ROOT / "Server" / "wwwroot" / "js",
    # React client sources (the build output is checked by ClientApp/scripts/check-offline-build.mjs).
    ROOT / "ClientApp" / "src", ROOT / "ClientApp" / "index.html",
]
CDN_HOSTS = (
    "cdn.jsdelivr.net", "cdnjs.cloudflare.com", "unpkg.com", "fonts.googleapis.com",
    "fonts.gstatic.com", "ajax.googleapis.com", "code.jquery.com", "stackpath.bootstrapcdn.com",
)
RESOURCE_ATTR = re.compile(r"(?:src|href)\s*=\s*['\"](https?://[^'\"]+)['\"]", re.IGNORECASE)
CSS_URL = re.compile(r"url\(\s*['\"]?(https?://[^)\"']+)['\"]?\s*\)", re.IGNORECASE)

violations = []
for search_root in SEARCH_ROOTS:
    if not search_root.exists():
        continue
    paths = [search_root] if search_root.is_file() else search_root.rglob("*")
    for path in paths:
        if not path.is_file() or path.suffix.lower() not in {".cshtml", ".html", ".css", ".js", ".ts", ".tsx"}:
            continue
        try:
            content = path.read_text(encoding="utf-8", errors="ignore")
        except OSError as exc:
            print(f"WARN: cannot read {path}: {exc}", file=sys.stderr)
            continue
        candidates = [(m.group(1), m.start()) for m in RESOURCE_ATTR.finditer(content)]
        if path.suffix.lower() in {".css", ".ts", ".tsx"}:
            candidates += [(m.group(1), m.start()) for m in CSS_URL.finditer(content)]
        for url, offset in candidates:
            host = re.sub(r"^https?://", "", url, flags=re.IGNORECASE).split("/", 1)[0].split(":", 1)[0].lower()
            if any(host == cdn or host.endswith("." + cdn) for cdn in CDN_HOSTS):
                line = content.count("\n", 0, offset) + 1
                violations.append(f"{path.relative_to(ROOT)}:{line}: remote asset reference: {url}")

if violations:
    print("ERROR: remote CDN assets are prohibited. Vendor files under wwwroot/lib are intentionally excluded.")
    print("\n".join(violations))
    sys.exit(1)

print("PASS: no remote CDN asset references found in application-owned pages, CSS, or JS.")
