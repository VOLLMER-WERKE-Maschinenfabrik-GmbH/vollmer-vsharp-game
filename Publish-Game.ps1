$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$clientDirectory = Join-Path $repositoryRoot "src/VGame.Client"
$publishDirectory = Join-Path $repositoryRoot "artifacts/publish/win-x64"

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js 22.x version 22.12.0 or newer is required to build the client."
}

$nodeVersionText = (node --version).Trim().TrimStart("v")
$nodeVersion = [version]$nodeVersionText
if ($nodeVersion -lt [version]"22.12.0" -or $nodeVersion.Major -ge 25) {
    throw "Node.js 22.12.0 through 24.x is required; found $nodeVersionText."
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is not available."
}

Push-Location $clientDirectory
try {
    if (Test-Path "package-lock.json") {
        npm ci
    }
    else {
        npm install
    }
    if ($LASTEXITCODE -ne 0) { throw "npm dependency installation failed." }

    npm run build
    if ($LASTEXITCODE -ne 0) { throw "The Phaser/TypeScript build failed." }
}
finally {
    Pop-Location
}

dotnet publish (Join-Path $repositoryRoot "src/VGame.Server/VGame.Server.csproj") -c Release -r win-x64 --self-contained true -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Windows packaging failed." }

Copy-Item (Join-Path $repositoryRoot "Start-Game.ps1") $publishDirectory -Force