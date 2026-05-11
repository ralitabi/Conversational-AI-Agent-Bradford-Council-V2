# Bradford Council AI - Restart API only
# Run this if the API window crashed or port 5000 is busy

$root = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "  Restarting API on port 5000..." -ForegroundColor Cyan
Write-Host ""

$lines = netstat -ano | Select-String ":5000\s"
foreach ($line in $lines) {
    $pid5000 = ($line.ToString().Trim() -split '\s+')[-1]
    if ($pid5000 -match '^\d+$') {
        Write-Host "  Freeing port 5000 (PID $pid5000)..." -ForegroundColor DarkGray
        Stop-Process -Id ([int]$pid5000) -Force -ErrorAction SilentlyContinue
    }
}
Start-Sleep -Seconds 1

Start-Process powershell -ArgumentList "-NoExit -ExecutionPolicy Bypass -Command `"Set-Location '$root\Backend\Api'; `$env:ASPNETCORE_ENVIRONMENT='Development'; Write-Host 'Bradford Council API - DO NOT CLOSE' -ForegroundColor Cyan; & 'C:\Program Files\dotnet\dotnet.exe' run`""

Write-Host "  API starting... (takes ~15 seconds)" -ForegroundColor Yellow
Write-Host ""

for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction Stop -UseBasicParsing
        if ($r.StatusCode -eq 200) {
            Write-Host "  API ready at http://localhost:5000" -ForegroundColor Green
            Start-Process "$root\Frontend\login.html"
            break
        }
    } catch {}
    Write-Host "  Waiting... ($($i * 2)s)" -ForegroundColor DarkGray
}

Write-Host ""