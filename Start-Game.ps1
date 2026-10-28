param(
    [switch]$OpenWatcher
)

$ErrorActionPreference = "Stop"
$applicationRoot = $PSScriptRoot
$publishedExecutable = Join-Path $PSScriptRoot "VGame.Server.exe"

if (-not (Test-Path $publishedExecutable)) {
    $publishedExecutable = Join-Path $PSScriptRoot "artifacts\publish\win-x64\VGame.Server.exe"
}

$env:Host__OpenPlayer = "true"
$env:Host__OpenWatcher = $OpenWatcher.IsPresent.ToString().ToLowerInvariant()

if (Test-Path $publishedExecutable) {
    Push-Location (Split-Path $publishedExecutable -Parent)
}
else {
    Push-Location $applicationRoot
}

try {
    if (Test-Path $publishedExecutable) {
        & $publishedExecutable
    }
    else {
        dotnet run --project "src/VGame.Server/VGame.Server.csproj" --launch-profile http
    }

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
    Remove-Item Env:Host__OpenPlayer -ErrorAction SilentlyContinue
    Remove-Item Env:Host__OpenWatcher -ErrorAction SilentlyContinue
}