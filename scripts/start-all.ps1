# Bradford Council AI - Start Everything (local development)
# Usage: double-click scripts\start-all.bat  OR  .\scripts\start-all.ps1

$root = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "  Bradford Council AI - Starting Local Stack" -ForegroundColor Cyan
Write-Host "  ------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# 1. Qdrant vector database
Write-Host "  [1/3] Starting Qdrant..." -ForegroundColor Yellow
$qdrantExe = "$root\Database\qdrant.exe"
if (Test-Path $qdrantExe) {
    Start-Process powershell -ArgumentList "-NoExit -ExecutionPolicy Bypass -Command `"Set-Location '$root\Database'; Write-Host 'Qdrant DB - DO NOT CLOSE' -ForegroundColor Cyan; .\qdrant.exe --config-path config.yaml`""
    Start-Sleep -Seconds 3
    Write-Host "  [1/3] Qdrant started on http://localhost:6333" -ForegroundColor Green
} else {
    Write-Host "  [1/3] Qdrant not found - skipping (optional)" -ForegroundColor DarkGray
}
Write-Host ""

# 2. .NET API backend
Write-Host "  [2/3] Starting API backend..." -ForegroundColor Yellow

$portLines = netstat -ano | Select-String ":5000\s" | Where-Object { $_ -match "LISTENING" }
foreach ($line in $portLines) {
    $pid5000 = ($line.ToString().Trim() -split '\s+')[-1]
    if ($pid5000 -match '^\d+$') {
        Write-Host "         Freeing port 5000 (PID $pid5000)..." -ForegroundColor DarkGray
        Stop-Process -Id ([int]$pid5000) -Force -ErrorAction SilentlyContinue
    }
}
if ($portLines) { Start-Sleep -Seconds 1 }

Start-Process powershell -ArgumentList "-NoExit -ExecutionPolicy Bypass -Command `"Set-Location '$root\Backend\Api'; `$env:ASPNETCORE_ENVIRONMENT='Development'; Write-Host 'Bradford Council API - DO NOT CLOSE' -ForegroundColor Cyan; & 'C:\Program Files\dotnet\dotnet.exe' run`""
Write-Host "  [2/3] API starting (takes ~15 seconds)..." -ForegroundColor Green
Write-Host ""

# 3. Wait for API then open frontend
Write-Host "  [3/3] Waiting for API to be ready..." -ForegroundColor Yellow
$ready = $false
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction Stop -UseBasicParsing
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch {}
    Write-Host "         Waiting... ($($i * 2)s)" -ForegroundColor DarkGray
}

Start-Process "$root\Frontend\login.html"

Write-Host ""
if ($ready) {
    Write-Host "  All services are running!" -ForegroundColor Green
} else {
    Write-Host "  Services started (API may still be loading)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "  Frontend : $root\Frontend\index.html" -ForegroundColor White
Write-Host "  Live     : https://bradford-council-ai.vercel.app" -ForegroundColor White
Write-Host "  API      : http://localhost:5000" -ForegroundColor White
Write-Host "  Qdrant   : http://localhost:6333" -ForegroundColor White
Write-Host ""
Write-Host "  Username : bradford2026" -ForegroundColor Yellow
Write-Host "  Password : 00000000" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Keep all open windows running. Close them to stop services." -ForegroundColor DarkGray
Write-Host ""