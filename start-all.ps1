# Bradford Council AI -- Start Everything
# Double-click or run this file to launch the full local stack

$root = $PSScriptRoot

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "   Bradford Council AI -- Starting All Services" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# -- 1. Qdrant vector database -------------------
Write-Host "  [1/3] Starting Qdrant database..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList `
    "-NoExit -ExecutionPolicy Bypass -Command `"Set-Location '$root\Database'; Write-Host 'Qdrant DB - DO NOT CLOSE' -ForegroundColor Cyan; .\qdrant.exe --config-path config.yaml`""
Start-Sleep -Seconds 3
Write-Host "         Qdrant started" -ForegroundColor Green

# -- 2. .NET API backend -------------------------
Write-Host "  [2/3] Starting API backend..." -ForegroundColor Yellow

# Free port 5000 if a previous instance is still running
$portLines = netstat -ano | Select-String ":5000\s" | Where-Object { $_ -match "LISTENING" }
foreach ($line in $portLines) {
    $pid5000 = ($line.ToString().Trim() -split '\s+')[-1]
    if ($pid5000 -match '^\d+$') {
        Write-Host "         Freeing port 5000 (PID $pid5000)..." -ForegroundColor DarkGray
        Stop-Process -Id ([int]$pid5000) -Force -ErrorAction SilentlyContinue
    }
}
if ($portLines) { Start-Sleep -Seconds 1 }

Start-Process powershell -ArgumentList `
    "-NoExit -ExecutionPolicy Bypass -Command `"Set-Location '$root\Backend\Api'; `$env:ASPNETCORE_ENVIRONMENT='Development'; Write-Host 'Bradford Council API - DO NOT CLOSE' -ForegroundColor Cyan; & 'C:\Program Files\dotnet\dotnet.exe' run`""
Write-Host "         API starting (takes ~15 seconds)..." -ForegroundColor Green

# -- 3. Wait for API then open frontend ----------
Write-Host "  [3/3] Waiting for API to be ready..." -ForegroundColor Yellow
$ready = $false
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch {}
    Write-Host "         Waiting... ($($i*2)s)" -ForegroundColor DarkGray
}

# Open login page (auth guard will redirect to index once logged in)
Start-Process "$root\Frontend\login.html"

Write-Host ""
if ($ready) {
    Write-Host "  OK  Everything is running!" -ForegroundColor Green
} else {
    Write-Host "  OK  Services started (API may still be loading)" -ForegroundColor DarkYellow
}
Write-Host ""
Write-Host "   Login    : $root\Frontend\login.html" -ForegroundColor White
Write-Host "   Frontend : $root\Frontend\index.html"  -ForegroundColor White
Write-Host "   Online   : https://bradford-council-ai.vercel.app" -ForegroundColor White
Write-Host "   API      : http://localhost:5000"       -ForegroundColor White
Write-Host "   Qdrant   : http://localhost:6333"       -ForegroundColor White
Write-Host ""
Write-Host "   Username : bradford2026" -ForegroundColor Yellow
Write-Host "   Password : 00000000"     -ForegroundColor Yellow
Write-Host ""
Write-Host "   Keep the Qdrant and API windows open." -ForegroundColor DarkGray
Write-Host "   Close them to stop the services."      -ForegroundColor DarkGray
Write-Host ""
Write-Host "   Press Enter to close this window..." -ForegroundColor DarkGray
$null = Read-Host
