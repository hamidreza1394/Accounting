# Tests of the output-directory guard of scripts/publish.ps1 (no build: -PrepareOutputOnly).
# Mirrors scripts/tests/publish-output.test.sh. Runs on Windows and on PowerShell 7 elsewhere.
# Usage: pwsh -NoProfile -File scripts/tests/publish-output.tests.ps1
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).ProviderPath
$publish = Join-Path $repoRoot 'scripts/publish.ps1'
$pwshPath = (Get-Process -Id $PID).Path
$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("faranou-guard-" + [guid]::NewGuid().ToString('N'))
$script:passed = 0
$script:failed = 0

New-Item -ItemType Directory -Path (Join-Path $sandbox 'home'), (Join-Path $sandbox 'work'), (Join-Path $sandbox 'outside') -Force | Out-Null
Set-Content (Join-Path $sandbox 'home/sentinel') 'keep'
Set-Content (Join-Path $sandbox 'work/sentinel') 'keep'

function Invoke-Guard([string]$Cwd, [string]$Path, [switch]$NoPath) {
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $publish, '-PrepareOutputOnly')
    if (-not $NoPath) { $arguments += @('-Output', $Path) }
    $previousHome = $env:HOME; $previousProfile = $env:USERPROFILE
    $env:HOME = Join-Path $sandbox 'home'
    if ($previousProfile) { $env:USERPROFILE = Join-Path $sandbox 'home' }
    Push-Location $Cwd
    try {
        $output = & $pwshPath @arguments 2>&1 | Out-String
        return @{ Code = $LASTEXITCODE; Output = $output }
    }
    finally {
        Pop-Location
        $env:HOME = $previousHome; $env:USERPROFILE = $previousProfile
    }
}

function Assert-Refused([string]$Name, [string]$Cwd, [string]$Path, [string]$Survivor) {
    $result = Invoke-Guard $Cwd $Path
    if ($result.Code -eq 0) { Write-Host "FAIL ${Name}: accepted '$Path'"; $script:failed++; return }
    if ($result.Output -notmatch 'refusing output directory') { Write-Host "FAIL ${Name}: no refusal message: $($result.Output)"; $script:failed++; return }
    if (-not (Test-Path -LiteralPath $Survivor)) { Write-Host "FAIL ${Name}: $Survivor was deleted"; $script:failed++; return }
    Write-Host "ok   $Name"; $script:passed++
}

function Assert-Accepted([string]$Name, [string]$Cwd, [string]$Path, [string]$Expected) {
    $result = Invoke-Guard $Cwd $Path
    if ($result.Code -ne 0) { Write-Host "FAIL ${Name}: refused '$Path': $($result.Output)"; $script:failed++; return }
    if (-not (Test-Path -LiteralPath (Join-Path $Expected '.faranou-publish-output'))) { Write-Host "FAIL ${Name}: marker missing"; $script:failed++; return }
    Write-Host "ok   $Name"; $script:passed++
}

try {
    $work = Join-Path $sandbox 'work'
    $outside = Join-Path $sandbox 'outside'
    $fsRoot = [System.IO.Path]::GetPathRoot($repoRoot)

    Assert-Refused 'file-system root'           $work $fsRoot (Join-Path $work 'sentinel')
    Assert-Refused 'empty path'                 $work ''      (Join-Path $work 'sentinel')
    Assert-Refused 'repository root'            $work $repoRoot (Join-Path $repoRoot 'Template.sln')
    Assert-Refused 'repository root via ..'     $work (Join-Path $repoRoot 'Server/..') (Join-Path $repoRoot 'Template.sln')
    Assert-Refused 'ancestor of the repository' $work (Split-Path $repoRoot -Parent) (Join-Path $repoRoot 'Template.sln')
    Assert-Refused 'source folder in the repo'  $work (Join-Path $repoRoot 'Server') (Join-Path $repoRoot 'Server/Program.cs')
    Assert-Refused 'artifacts itself'           $work (Join-Path $repoRoot 'artifacts') (Join-Path $repoRoot 'Template.sln')
    Assert-Refused 'home directory'             $work (Join-Path $sandbox 'home') (Join-Path $sandbox 'home/sentinel')
    Assert-Refused 'ancestor of home'           $work $sandbox (Join-Path $sandbox 'home/sentinel')
    Assert-Refused 'current directory (.)'      $work '.' (Join-Path $work 'sentinel')
    Assert-Refused 'current directory (abs)'    $work $work (Join-Path $work 'sentinel')

    New-Item -ItemType Directory -Path (Join-Path $outside 'data') -Force | Out-Null
    Set-Content (Join-Path $outside 'data/important.txt') 'keep'
    Assert-Refused 'non-empty foreign directory' $work (Join-Path $outside 'data') (Join-Path $outside 'data/important.txt')

    Set-Content (Join-Path $outside 'a-file') 'keep'
    Assert-Refused 'existing file' $work (Join-Path $outside 'a-file') (Join-Path $outside 'a-file')

    $linkCreated = $true
    try {
        if ($IsWindows) {
            New-Item -ItemType Junction -Path (Join-Path $outside 'link-to-data') -Target (Join-Path $outside 'data') | Out-Null
        }
        else {
            New-Item -ItemType SymbolicLink -Path (Join-Path $outside 'link-to-data') -Target (Join-Path $outside 'data') | Out-Null
        }
    }
    catch { $linkCreated = $false; Write-Host "skip link case: $($_.Exception.Message)" }
    if ($linkCreated) {
        Assert-Refused 'link to a folder' $work (Join-Path $outside 'link-to-data') (Join-Path $outside 'data/important.txt')
        Assert-Refused 'path below a link' $work (Join-Path $outside 'link-to-data/sub') (Join-Path $outside 'data/important.txt')
    }

    Assert-Accepted 'new directory outside the repo' $work (Join-Path $outside 'publish-1') (Join-Path $outside 'publish-1')
    Set-Content (Join-Path $outside 'publish-1/stale.txt') 'stale'
    Assert-Accepted 'earlier publish output (marker) is replaced' $work (Join-Path $outside 'publish-1') (Join-Path $outside 'publish-1')
    if (Test-Path (Join-Path $outside 'publish-1/stale.txt')) { Write-Host 'FAIL stale file survived'; $script:failed++ }
    New-Item -ItemType Directory -Path (Join-Path $outside 'empty') -Force | Out-Null
    Assert-Accepted 'existing empty directory' $work (Join-Path $outside 'empty') (Join-Path $outside 'empty')
    Assert-Accepted 'relative path' $outside 'relative-out' (Join-Path $outside 'relative-out')

    $defaultOut = Join-Path $repoRoot 'artifacts/publish'
    $hadDefault = Test-Path $defaultOut
    if (-not $hadDefault -or (Test-Path (Join-Path $defaultOut '.faranou-publish-output'))) {
        $result = Invoke-Guard $work '' -NoPath
        if ($result.Code -eq 0 -and (Test-Path (Join-Path $defaultOut '.faranou-publish-output'))) { Write-Host 'ok   default output (artifacts/publish)'; $script:passed++ }
        else { Write-Host "FAIL default output: $($result.Output)"; $script:failed++ }
        if (-not $hadDefault) {
            Remove-Item -LiteralPath $defaultOut -Recurse -Force
            if (-not (Get-ChildItem (Join-Path $repoRoot 'artifacts') -Force)) { Remove-Item (Join-Path $repoRoot 'artifacts') -Force }
        }
    }
    else { Write-Host "skip default output: $defaultOut exists without a publish marker" }
}
finally {
    Get-ChildItem -LiteralPath $sandbox -Recurse -Force -Attributes ReparsePoint -ErrorAction SilentlyContinue | ForEach-Object { $_.Delete() }
    Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "$script:passed passed, $script:failed failed"
if ($script:failed -ne 0) { exit 1 }
