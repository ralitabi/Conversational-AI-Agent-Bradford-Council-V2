# Bradford Council AI - Start Everything
# Run from project root:  .\scripts\start-all.ps1

Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force

$root = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "  Bradford Council AI - Starting Local Stack" -ForegroundColor Cyan
Write-Host "  -------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# 1. Qdrant
Write-Host "  [1/4] Qdrant..." -ForegroundColor Yellow
$qdrantExe = "$root\Database\qdrant.exe"
if (Test-Path $qdrantExe) {
    Start-Process powershell -ArgumentList @(
        "-NoExit", "-ExecutionPolicy", "Bypass", "-Command",
        "Set-Location '$root\Database'; Write-Host 'Qdrant - DO NOT CLOSE' -ForegroundColor Cyan; .\qdrant.exe --config-path config.yaml"
    )
    Start-Sleep -Seconds 3
    Write-Host "  [1/4] Qdrant  ->  http://localhost:6333" -ForegroundColor Green
} else {
    Write-Host "  [1/4] Qdrant not found - skipping" -ForegroundColor DarkGray
}

# 2. API
Write-Host "  [2/4] API backend..." -ForegroundColor Yellow

$portLines = netstat -ano | Select-String ":5000\s" | Where-Object { $_ -match "LISTENING" }
foreach ($line in $portLines) {
    $p = ($line.ToString().Trim() -split '\s+')[-1]
    if ($p -match '^\d+$') {
        Write-Host "         Freeing port 5000 (PID $p)" -ForegroundColor DarkGray
        Stop-Process -Id ([int]$p) -Force -ErrorAction SilentlyContinue
    }
}
if ($portLines) { Start-Sleep -Seconds 1 }

Start-Process powershell -ArgumentList @(
    "-NoExit", "-ExecutionPolicy", "Bypass", "-Command",
    "Set-Location '$root\Backend\Api'; `$env:ASPNETCORE_ENVIRONMENT='Development'; Write-Host 'API - DO NOT CLOSE' -ForegroundColor Cyan; dotnet run"
)
Write-Host "  [2/4] API window opened (building ~20s)..." -ForegroundColor Green

# 3. Frontend
Write-Host "  [3/4] Frontend server..." -ForegroundColor Yellow

$port3Lines = netstat -ano | Select-String ":3000\s" | Where-Object { $_ -match "LISTENING" }
foreach ($line in $port3Lines) {
    $p = ($line.ToString().Trim() -split '\s+')[-1]
    if ($p -match '^\d+$') {
        Write-Host "         Freeing port 3000 (PID $p)" -ForegroundColor DarkGray
        Stop-Process -Id ([int]$p) -Force -ErrorAction SilentlyContinue
    }
}

Start-Process python -ArgumentList "`"$root\scripts\serve_frontend.py`"" -WindowStyle Normal
Write-Host "  [3/4] Frontend  ->  http://localhost:3000" -ForegroundColor Green

# 4. Wait for API then open browser
Write-Host "  [4/4] Waiting for API..." -ForegroundColor Yellow
$ready = $false
for ($i = 1; $i -le 25; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction Stop -UseBasicParsing
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch {}
    Write-Host "         $($i * 2)s..." -ForegroundColor DarkGray
}

Start-Process "http://localhost:3000"

Write-Host ""
if ($ready) {
    Write-Host "  All services ready!" -ForegroundColor Green
} else {
    Write-Host "  Services started - API still building, browser will connect once ready" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "  Frontend  ->  http://localhost:3000" -ForegroundColor White
Write-Host "  API       ->  http://localhost:5000" -ForegroundColor White
Write-Host "  Qdrant    ->  http://localhost:6333" -ForegroundColor White
Write-Host ""
Write-Host "  Login  :  bradford2026  /  00000000" -ForegroundColor Yellow
Write-Host ""
