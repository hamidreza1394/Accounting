# Validation and preparation of the publish output directory (dot-sourced by scripts/publish.ps1).
# Same rules as scripts/lib/publish-output.sh:
#   * the path is made absolute and canonical (".." removed); neither the path nor any
#     existing ancestor may be a symbolic link or junction (pass the real path);
#   * not a file-system/drive root, the repository root, the home directory, the current
#     directory, or an ancestor of any of them;
#   * inside the repository only artifacts\<something> is allowed;
#   * an existing directory is replaced only when it is empty or carries the marker file of an
#     earlier publish (.faranou-publish-output); an existing file is refused.
# Nothing is deleted unless every check passes.
#
# Assert-FaranouCustomerPublishClean (G2 / D-041) refuses Vendor assemblies and private-key
# material in the customer package after a real publish, and enforces the D-041 G2 allow-list
# of the customer package (publish-assembly-allowlist.txt next to this lib): every published assembly/native binary or symbol file
# (.dll/.exe/.so/.dylib/.pdb and the extension-less apphost) outside wwwroot/ must be
# explicitly listed; anything unlisted is refused fail-closed. Non-binary root files are not
# assemblies and are skipped here; the PEM scan still inspects their content.

$script:FaranouPublishMarker = '.faranou-publish-output'
$script:FaranouAllowListPath = Join-Path $PSScriptRoot 'publish-assembly-allowlist.txt'

function Get-FaranouComparison {
    if ($IsWindows -or $env:OS -eq 'Windows_NT') { return [System.StringComparison]::OrdinalIgnoreCase }
    return [System.StringComparison]::Ordinal
}

function Get-FaranouNormalizedPath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetPathRoot($full)
    if ($full.Length -gt $root.Length) { $full = $full.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) }
    return $full
}

function Test-FaranouSameOrAncestor([string]$Candidate, [string]$Path) {
    $comparison = Get-FaranouComparison
    if ([string]::Equals($Candidate, $Path, $comparison)) { return $true }
    $prefix = $Candidate
    if (-not $prefix.EndsWith([System.IO.Path]::DirectorySeparatorChar)) { $prefix += [System.IO.Path]::DirectorySeparatorChar }
    return $Path.StartsWith($prefix, $comparison)
}

function Test-FaranouLink([string]$Path) {
    $current = $Path
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if ($item.LinkType -or ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) { return $current }
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
    return $null
}

function Resolve-FaranouPublishOutput([string]$Requested, [string]$RepoRoot) {
    if ([string]::IsNullOrWhiteSpace($Requested)) { throw 'publish: refusing output directory: the path is empty' }

    $comparison = Get-FaranouComparison
    $repo = Get-FaranouNormalizedPath (Resolve-Path -LiteralPath $RepoRoot).ProviderPath
    $current = Get-FaranouNormalizedPath (Get-Location).ProviderPath
    $homeSource = if ($env:USERPROFILE) { $env:USERPROFILE } elseif ($env:HOME) { $env:HOME } else { [System.IO.Path]::GetPathRoot($current) }
    $homeDir = Get-FaranouNormalizedPath $homeSource

    $candidate = if ([System.IO.Path]::IsPathRooted($Requested)) { $Requested } else { Join-Path $current $Requested }
    $canonical = Get-FaranouNormalizedPath $candidate

    $link = Test-FaranouLink $canonical
    if ($link) { throw "publish: refusing output directory: '$link' is a symbolic link or junction" }

    if ([string]::Equals($canonical, [System.IO.Path]::GetPathRoot($canonical), $comparison)) {
        throw "publish: refusing output directory: '$canonical' is the file-system root"
    }
    if (Test-FaranouSameOrAncestor $canonical $repo) { throw "publish: refusing output directory: '$canonical' is the repository root or contains it" }
    if (Test-FaranouSameOrAncestor $canonical $homeDir) { throw "publish: refusing output directory: '$canonical' is the home directory or contains it" }
    if (Test-FaranouSameOrAncestor $canonical $current) { throw "publish: refusing output directory: '$canonical' is the current directory or contains it" }

    if (Test-FaranouSameOrAncestor $repo $canonical) {
        $artifacts = Join-Path $repo 'artifacts'
        if ([string]::Equals($canonical, $artifacts, $comparison) -or -not (Test-FaranouSameOrAncestor $artifacts $canonical)) {
            throw "publish: refusing output directory: '$canonical' is inside the repository but not under artifacts"
        }
    }

    if (Test-Path -LiteralPath $canonical -PathType Leaf) { throw "publish: refusing output directory: '$canonical' exists and is not a directory" }
    if (Test-Path -LiteralPath $canonical -PathType Container) {
        $hasContent = $null -ne (Get-ChildItem -LiteralPath $canonical -Force | Select-Object -First 1)
        if ($hasContent -and -not (Test-Path -LiteralPath (Join-Path $canonical $script:FaranouPublishMarker) -PathType Leaf)) {
            throw "publish: refusing output directory: '$canonical' is not empty and was not created by a publish (no $script:FaranouPublishMarker)"
        }
    }

    return $canonical
}

function Initialize-FaranouPublishOutput([string]$Output) {
    if (Test-Path -LiteralPath $Output) { Remove-Item -LiteralPath $Output -Recurse -Force }
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Output $script:FaranouPublishMarker) -Value 'Created by scripts/publish.ps1; the whole directory is replaced by the next publish.'
}

function Assert-FaranouPublishAllowList([string]$Output) {
    if (-not (Test-Path -LiteralPath $script:FaranouAllowListPath -PathType Leaf)) {
        throw "publish: G2 content guard: allow-list file missing or unreadable: $script:FaranouAllowListPath"
    }

    $rootOk = @{}
    $satOk = @{}
    $cultOk = @{}
    $runOk = @{}
    foreach ($line in (Get-Content -LiteralPath $script:FaranouAllowListPath)) {
        $trimmed = $line.TrimEnd("`r")
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        $idx = $trimmed.IndexOf(':')
        if ($idx -le 0) { throw "publish: G2 content guard: malformed allow-list entry: $trimmed" }
        $kind = $trimmed.Substring(0, $idx).Trim()
        $value = $trimmed.Substring($idx + 1).Trim()
        switch ($kind) {
            'root' { $rootOk[$value.ToLowerInvariant()] = $true }
            'satellite' { $satOk[$value.ToLowerInvariant()] = $true }
            'culture' { $cultOk[$value.ToLowerInvariant()] = $true }
            'runtime' { $runOk[$value.ToLowerInvariant()] = $true }
            default { throw "publish: G2 content guard: malformed allow-list entry: $trimmed" }
        }
    }

    $binaryExt = @('.dll', '.exe', '.so', '.dylib', '.pdb')
    $violations = @()
    Get-ChildItem -LiteralPath $Output -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
        # Normalize to '/' so the entries of publish-assembly-allowlist.txt (written with
        # '/') match on Windows just like on Linux: TrimStart drops the separator left by
        # Substring (either style), Replace canonicalizes the rest of the path. Both
        # separator styles and any mix of them therefore compare identically and no
        # allowed runtime/satellite path is refused because of the host OS; everything
        # that is still not listed stays fail-closed.
        $rel = $_.FullName.Substring($Output.Length).TrimStart('\', '/').Replace('\', '/')
        if ($rel -like 'wwwroot/*' -or $rel -like 'wwwroot\*') { return }
        if ($_.Name -ieq $script:FaranouPublishMarker) { return }
        $segments = $rel -split '[\\/]'
        if ($segments[0] -ieq 'runtimes') {
            if (-not $runOk.ContainsKey($rel.ToLowerInvariant())) { $violations += $rel }
            return
        }
        if ($segments.Count -gt 1) {
            # Nested (not runtimes/, not wwwroot/): only an allowed satellite
            # <culture>/<base>.resources.dll may live here.
            $culture = $segments[0].ToLowerInvariant()
            $name = $_.Name
            if ($cultOk.ContainsKey($culture) -and $name.ToLowerInvariant().EndsWith('.resources.dll') -and
                $satOk.ContainsKey($name.Substring(0, $name.Length - '.resources.dll'.Length).ToLowerInvariant())) { return }
            $violations += $rel
            return
        }
        # Root level: binaries and the extension-less apphost must be allow-listed.
        if ($_.Extension) {
            if ($binaryExt -contains $_.Extension.ToLowerInvariant() -and -not $rootOk.ContainsKey($_.Name.ToLowerInvariant())) { $violations += $rel }
        }
        else {
            if (-not $rootOk.ContainsKey($_.Name.ToLowerInvariant())) { $violations += $rel }
        }
    }

    foreach ($v in $violations) {
        Write-Error "publish: G2 content guard: file outside the D-041 publish allow-list: $v"
    }
    if ($violations.Count -gt 0) {
        throw 'publish: G2 content guard failed (D-041): customer package must not contain files outside the publish allow-list'
    }
}

function Assert-FaranouCustomerPublishClean([string]$Output) {
    if (-not (Test-Path -LiteralPath $Output -PathType Container)) {
        throw "publish: G2 content guard: output directory does not exist: $Output"
    }

    $found = $false
    $vendorNames = @('Vendor.*.dll', 'Vendor.*.exe', 'Vendor.*.so', 'Vendor.*.dylib', 'Vendor.*.pdb')
    foreach ($pattern in $vendorNames) {
        Get-ChildItem -LiteralPath $Output -Recurse -File -Force -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Error "publish: G2 content guard: forbidden Vendor assembly or library: $($_.FullName.Substring($Output.Length).TrimStart('\','/'))"
            $found = $true
        }
    }

    foreach ($ext in @('*.pfx', '*.p12', '*.key')) {
        Get-ChildItem -LiteralPath $Output -Recurse -File -Force -Filter $ext -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Error "publish: G2 content guard: forbidden sensitive file: $($_.FullName.Substring($Output.Length).TrimStart('\','/'))"
            $found = $true
        }
    }

    $skipExt = @('.dll', '.exe', '.so', '.dylib', '.pdb', '.png', '.jpg', '.jpeg', '.gif', '.woff', '.woff2', '.ttf', '.eot', '.ico', '.zip', '.nupkg')
    $pemPattern = '-----BEGIN ([A-Z0-9 ]+)?PRIVATE KEY-----'
    # Hits are collected first and reported after the loop: a Write-Error inside this
    # try/catch would itself be caught by the catch below when ErrorActionPreference is
    # Stop, silently disabling the whole scan (found by the publish-content-guard fixture
    # tests in 2026-11-23). Unreadable files are still skipped as before.
    $pemHits = @()
    Get-ChildItem -LiteralPath $Output -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -lt 512KB -and ($skipExt -notcontains $_.Extension.ToLowerInvariant()) } |
        ForEach-Object {
            try {
                $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction Stop
                if ($text -match $pemPattern) { $pemHits += $_.FullName.Substring($Output.Length).TrimStart('\', '/') }
            } catch { }
        }
    foreach ($hit in $pemHits) {
        Write-Error "publish: G2 content guard: private key material detected in: $hit"
        $found = $true
    }

    if ($found) {
        throw 'publish: G2 content guard failed (D-041): customer package must not contain Vendor assemblies or private keys'
    }

    Assert-FaranouPublishAllowList -Output $Output
}
