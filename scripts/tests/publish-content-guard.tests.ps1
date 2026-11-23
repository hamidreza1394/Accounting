# G2 (D-041) tests of Assert-FaranouCustomerPublishClean (PublishOutput.ps1), including the
# publish allow-list. Mirrors scripts/tests/publish-content-guard.test.sh with the same
# synthetic fixtures — no real private keys, no real Vendor build.
# Usage: pwsh -NoProfile -File scripts/tests/publish-content-guard.tests.ps1
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).ProviderPath
. (Join-Path $repoRoot 'scripts/lib/PublishOutput.ps1')

$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("faranou-content-guard-" + [guid]::NewGuid().ToString('N'))
$script:passed = 0
$script:failed = 0

function Assert-Rejected([string]$Name, [string]$Output) {
    try {
        Assert-FaranouCustomerPublishClean -Output $Output
        Write-Host "FAIL ${Name}: guard accepted the fixture"
        $script:failed++
    }
    catch {
        if ($_.Exception.Message -notmatch 'G2 content guard') {
            Write-Host "FAIL ${Name}: unexpected error: $($_.Exception.Message)"
            $script:failed++
            return
        }
        Write-Host "ok   $Name"
        $script:passed++
    }
}

function Assert-Accepted([string]$Name, [string]$Output) {
    try {
        Assert-FaranouCustomerPublishClean -Output $Output
        Write-Host "ok   $Name"
        $script:passed++
    }
    catch {
        Write-Host "FAIL ${Name}: guard rejected a valid tree: $($_.Exception.Message)"
        $script:failed++
    }
}

function Set-File([string]$Path, [string]$Value = 'x') {
    Set-Content -LiteralPath $Path -Value $Value -NoNewline
}

try {
    # --- positive: clean customer-like tree -------------------------------------------------
    $clean = Join-Path $sandbox 'clean'
    New-Item -ItemType Directory -Path (Join-Path $clean 'wwwroot/app/assets') -Force | Out-Null
    Set-File (Join-Path $clean '.faranou-publish-output') "marker`n"
    Set-File (Join-Path $clean 'Server.dll') "public class Server {}`n"
    Set-File (Join-Path $clean 'wwwroot/app/index.html') "ok`n"
    Set-File (Join-Path $clean 'wwwroot/app/assets/main.js') "js`n"
    Set-File (Join-Path $clean 'wwwroot/app/assets/main.css') "css`n"
    Set-File (Join-Path $clean 'public-cert.pem') "-----BEGIN CERTIFICATE-----`nMIIB`n-----END CERTIFICATE-----`n"
    Assert-Accepted 'clean publish output accepted' $clean

    # --- negative: Vendor assembly ----------------------------------------------------------
    $vendor = Join-Path $sandbox 'vendor'
    New-Item -ItemType Directory -Path $vendor -Force | Out-Null
    Set-File (Join-Path $vendor 'Server.dll')
    Set-File (Join-Path $vendor 'Vendor.Licensing.Host.dll')
    Assert-Rejected 'Vendor assembly rejected' $vendor

    # --- negative: .pfx / .p12 / .key ---------------------------------------------------------
    foreach ($case in @(
        @{ Name = '.pfx rejected'; File = 'signing.pfx' },
        @{ Name = '.p12 rejected'; File = 'pair.p12' },
        @{ Name = '.key rejected'; File = 'device.key' }
    )) {
        $dir = Join-Path $sandbox ($case.File + '-dir')
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Set-File (Join-Path $dir 'Server.dll')
        Set-File (Join-Path $dir $case.File) 'not-a-real-key'
        Assert-Rejected $case.Name $dir
    }

    # --- negative: PEM private key content (fixture only; not a real key) --------------------
    $pem = Join-Path $sandbox 'pem'
    New-Item -ItemType Directory -Path $pem -Force | Out-Null
    Set-File (Join-Path $pem 'Server.dll')
    Set-File (Join-Path $pem 'leak.txt') "-----BEGIN RSA PRIVATE KEY-----`nTEST-FIXTURE-NOT-A-REAL-KEY`n-----END RSA PRIVATE KEY-----`n"
    Assert-Rejected 'PEM private key content rejected' $pem

    # --- positive: tree matching the real publish output (allow-list subset) -----------------
    $allowOk = Join-Path $sandbox 'allow-ok'
    foreach ($d in @('wwwroot/app/assets', 'fa-IR', 'de', 'runtimes/linux-x64/native', 'runtimes/win-x64/native')) {
        New-Item -ItemType Directory -Path (Join-Path $allowOk $d) -Force | Out-Null
    }
    Set-File (Join-Path $allowOk '.faranou-publish-output') "marker`n"
    Set-File (Join-Path $allowOk 'Server.dll')
    Set-File (Join-Path $allowOk 'Server')
    Set-File (Join-Path $allowOk 'Application.dll')
    Set-File (Join-Path $allowOk 'Faranou.Licensing.Contracts.dll')
    Set-File (Join-Path $allowOk 'Microsoft.Data.SqlClient.dll')
    Set-File (Join-Path $allowOk 'fa-IR/Resources.resources.dll')
    Set-File (Join-Path $allowOk 'de/Microsoft.Data.SqlClient.resources.dll')
    Set-File (Join-Path $allowOk 'runtimes/linux-x64/native/libmsalruntime.so')
    Set-File (Join-Path $allowOk 'runtimes/win-x64/native/Microsoft.Data.SqlClient.SNI.dll')
    Set-File (Join-Path $allowOk 'wwwroot/app/index.html') "ok`n"
    Set-File (Join-Path $allowOk 'web.config') "config`n"
    Set-File (Join-Path $allowOk 'Server.deps.json') "deps`n"
    Assert-Accepted 'allow-listed publish output accepted' $allowOk

    # --- negative: assembly not in the allow-list ---------------------------------------------
    $unlisted = Join-Path $sandbox 'allow-unlisted'
    New-Item -ItemType Directory -Path $unlisted -Force | Out-Null
    Set-File (Join-Path $unlisted 'Server.dll')
    Set-File (Join-Path $unlisted 'NotListed.Library.dll')
    Assert-Rejected 'unlisted assembly rejected' $unlisted

    # --- negative: satellite assembly in the wrong place ---------------------------------------
    $satRoot = Join-Path $sandbox 'allow-sat-root'
    New-Item -ItemType Directory -Path $satRoot -Force | Out-Null
    Set-File (Join-Path $satRoot 'Server.dll')
    Set-File (Join-Path $satRoot 'Microsoft.Data.SqlClient.resources.dll')
    Assert-Rejected 'misplaced satellite rejected' $satRoot

    # --- negative: unknown satellite culture ----------------------------------------------------
    $satCult = Join-Path $sandbox 'allow-sat-culture'
    New-Item -ItemType Directory -Path (Join-Path $satCult 'ar') -Force | Out-Null
    Set-File (Join-Path $satCult 'Server.dll')
    Set-File (Join-Path $satCult 'ar/Microsoft.Data.SqlClient.resources.dll')
    Assert-Rejected 'unknown satellite culture rejected' $satCult

    # --- negative: unknown satellite base name --------------------------------------------------
    $satBase = Join-Path $sandbox 'allow-sat-base'
    New-Item -ItemType Directory -Path (Join-Path $satBase 'fa-IR') -Force | Out-Null
    Set-File (Join-Path $satBase 'Server.dll')
    Set-File (Join-Path $satBase 'fa-IR/Foo.resources.dll')
    Assert-Rejected 'unknown satellite base rejected' $satBase

    # --- negative: unexpected path under runtimes/ -----------------------------------------------
    $runtimeEvil = Join-Path $sandbox 'allow-runtime-evil'
    New-Item -ItemType Directory -Path (Join-Path $runtimeEvil 'runtimes/linux-x64/native') -Force | Out-Null
    Set-File (Join-Path $runtimeEvil 'Server.dll')
    Set-File (Join-Path $runtimeEvil 'runtimes/linux-x64/native/evil.so')
    Assert-Rejected 'unexpected runtimes file rejected' $runtimeEvil

    # --- negative: binary in an unknown nested directory ------------------------------------------
    $nestedEvil = Join-Path $sandbox 'allow-nested-evil'
    New-Item -ItemType Directory -Path (Join-Path $nestedEvil 'plugins') -Force | Out-Null
    Set-File (Join-Path $nestedEvil 'Server.dll')
    Set-File (Join-Path $nestedEvil 'plugins/Evil.Plugin.dll')
    Assert-Rejected 'nested unknown binary rejected' $nestedEvil

    # --- negative: unlisted extension-less root file -----------------------------------------------
    $stray = Join-Path $sandbox 'allow-stray'
    New-Item -ItemType Directory -Path $stray -Force | Out-Null
    Set-File (Join-Path $stray 'Server.dll')
    Set-File (Join-Path $stray 'stray-binary')
    Assert-Rejected 'unlisted extension-less root file rejected' $stray
}
finally {
    Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "$script:passed passed, $script:failed failed"
if ($script:failed -ne 0) { exit 1 }
