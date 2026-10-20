# Windows equivalent of scripts/publish.sh (same order and checks).
# Usage: pwsh scripts/publish.ps1 [-Output <directory>]
# Note: CI runs scripts/publish.sh; this script mirrors it for Windows developers.
param([string]$Output = (Join-Path $PSScriptRoot '..\artifacts\publish'))

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$Output = [System.IO.Path]::GetFullPath($Output)

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

if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
Invoke-Step "Server: dotnet publish -> $Output" {
    dotnet publish (Join-Path $repoRoot 'Server\Server.csproj') --configuration Release --output $Output
}

$app = Join-Path $Output 'wwwroot\app'
if (-not (Test-Path (Join-Path $app 'index.html'))) { throw 'missing wwwroot\app\index.html in the publish output' }
if (-not (Get-ChildItem (Join-Path $app 'assets') -Filter *.js)) { throw 'no JavaScript bundle in wwwroot\app\assets' }
if (-not (Get-ChildItem (Join-Path $app 'assets') -Filter *.css)) { throw 'no stylesheet in wwwroot\app\assets' }
Invoke-Step 'Publish output: offline check' {
    node (Join-Path $repoRoot 'ClientApp\scripts\check-offline-build.mjs') --wwwroot (Join-Path $Output 'wwwroot')
}
Write-Host "==> Published to $Output"
