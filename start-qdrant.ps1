Write-Host "Starting Qdrant vector database on ports 6333 (HTTP) and 6334 (gRPC)..." -ForegroundColor Cyan
Set-Location "$PSScriptRoot\Database"
& ".\qdrant.exe" --config-path config.yaml
