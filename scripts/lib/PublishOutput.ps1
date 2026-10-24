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
# material in the customer package after a real publish.

$script:FaranouPublishMarker = '.faranou-publish-output'

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

function Assert-FaranouCustomerPublishClean([string]$Output) {
    if (-not (Test-Path -LiteralPath $Output -PathType Container)) {
        throw "publish: G2 content guard: output directory does not exist: $Output"
    }

    $found = $false
    $vendorNames = @('Vendor.*.dll', 'Vendor.*.exe', 'Vendor.*.so', 'Vendor.*.dylib', 'Vendor.*.pdb')
    foreach ($pattern in $vendorNames) {
        Get-ChildItem -LiteralPath $Output -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Error "publish: G2 content guard: forbidden Vendor assembly or library: $($_.FullName.Substring($Output.Length).TrimStart('\','/'))"
            $found = $true
        }
    }

    foreach ($ext in @('*.pfx', '*.p12', '*.key')) {
        Get-ChildItem -LiteralPath $Output -Recurse -File -Filter $ext -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Error "publish: G2 content guard: forbidden sensitive file: $($_.FullName.Substring($Output.Length).TrimStart('\','/'))"
            $found = $true
        }
    }

    $skipExt = @('.dll', '.exe', '.so', '.dylib', '.pdb', '.png', '.jpg', '.jpeg', '.gif', '.woff', '.woff2', '.ttf', '.eot', '.ico', '.zip', '.nupkg')
    $pemPattern = '-----BEGIN ([A-Z0-9 ]+)?PRIVATE KEY-----'
    Get-ChildItem -LiteralPath $Output -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -lt 512KB -and ($skipExt -notcontains $_.Extension.ToLowerInvariant()) } |
        ForEach-Object {
            try {
                $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction Stop
                if ($text -match $pemPattern) {
                    Write-Error "publish: G2 content guard: private key material detected in: $($_.FullName.Substring($Output.Length).TrimStart('\','/'))"
                    $found = $true
                }
            } catch { }
        }

    if ($found) {
        throw 'publish: G2 content guard failed (D-041): customer package must not contain Vendor assemblies or private keys'
    }
}
