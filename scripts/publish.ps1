# PowerShell (Windows and PowerShell 7 on Linux/macOS) equivalent of scripts/publish.sh: same order and checks.
# Usage: pwsh scripts/publish.ps1 [-Output <directory>] [-PrepareOutputOnly]
# The output directory is validated before anything is built or deleted
# (scripts/lib/PublishOutput.ps1). -PrepareOutputOnly stops after that step (tests).
param(
    [string]$Output = (Join-Path (Join-Path $PSScriptRoot '..') 'artifacts/publish'),
    [switch]$PrepareOutputOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).ProviderPath
. (Join-Path $PSScriptRoot 'lib/PublishOutput.ps1')

$Output = Resolve-FaranouPublishOutput -Requested $Output -RepoRoot $repoRoot
Initialize-FaranouPublishOutput -Output $Output
Write-Host "==> Output directory: $Output"
if ($PrepareOutputOnly) { return }

function Invoke-Step([string]$Title, [scriptblock]$Command) {
    Write-Host "==> $Title"
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Title failed (exit code $LASTEXITCODE)." }
}

Push-Location (Join-Path $repoRoot 'ClientApp')
try {
    Invoke-Step 'Client: npm ci' { npm ci --no-fund --no-audit }
    Invoke-Step 'Client: type check' { npm run typecheck }
    Invoke-Step 'Client: tests' { npm test }
    Invoke-Step 'Client: production build' { npm run build }
    Invoke-Step 'Client: offline check' { npm run check:offline }
}
finally { Pop-Location }

Invoke-Step "Server: dotnet publish -> $Output" {
    dotnet publish (Join-Path $repoRoot 'Server/Server.csproj') --configuration Release --output $Output
}

$app = Join-Path $Output 'wwwroot/app'
if (-not (Test-Path (Join-Path $app 'index.html'))) { throw 'missing wwwroot\app\index.html in the publish output' }
if (-not (Get-ChildItem (Join-Path $app 'assets') -Filter *.js)) { throw 'no JavaScript bundle in wwwroot\app\assets' }
if (-not (Get-ChildItem (Join-Path $app 'assets') -Filter *.css)) { throw 'no stylesheet in wwwroot\app\assets' }
Invoke-Step 'Publish output: offline check' {
    node (Join-Path $repoRoot 'ClientApp/scripts/check-offline-build.mjs') --wwwroot (Join-Path $Output 'wwwroot')
}

Write-Host '==> Publish output: G2 content guard (no Vendor assemblies / private keys)'
Assert-FaranouCustomerPublishClean -Output $Output

Write-Host "==> Published to $Output"
