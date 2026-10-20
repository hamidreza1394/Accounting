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
    # The path itself or any existing ancestor being a link would let the delete escape.
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
