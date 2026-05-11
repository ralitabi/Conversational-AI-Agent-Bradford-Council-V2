# Bradford Council AI - Start Qdrant vector database only
# Runs on http://localhost:6333  (HTTP)  and  localhost:6334  (gRPC)

$root = Split-Path -Parent $PSScriptRoot
$qdrantExe = "$root\Database\qdrant.exe"

if (-not (Test-Path $qdrantExe)) {
    Write-Host "qdrant.exe not found at $qdrantExe" -ForegroundColor Red
    Write-Host "Download it from https://github.com/qdrant/qdrant/releases" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Starting Qdrant on ports 6333 (HTTP) and 6334 (gRPC)..." -ForegroundColor Cyan
Set-Location "$root\Database"
& ".\qdrant.exe" --config-path config.yaml