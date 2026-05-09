# Bradford Council AI -- Restart API only
# Run this if the API window crashed or port 5000 is in use

$root = $PSScriptRoot

Write-Host ""
Write-Host "  Restarting API on port 5000..." -ForegroundColor Cyan

# Kill anything holding port 5000
$lines = netstat -ano | Select-String ":5000\s"
foreach ($line in $lines) {
    $parts = ($line.ToString().Trim() -split '\s+')
    $pid5000 = $parts[-1]
    if ($pid5000 -match '^\d+$') {
        Write-Host "  Killing PID $pid5000 on port 5000..." -ForegroundColor DarkGray
        Stop-Process -Id ([int]$pid5000) -Force -ErrorAction SilentlyContinue
    }
}
Start-Sleep -Seconds 1

# Start fresh API window (Development mode so CORS allows file:// frontend)
Start-Process powershell -ArgumentList `
    "-NoExit -ExecutionPolicy Bypass -Command `"Set-Location '$root\Backend\Api'; `$env:ASPNETCORE_ENVIRONMENT='Development'; Write-Host 'Bradford Council API - DO NOT CLOSE' -ForegroundColor Cyan; & 'C:\Program Files\dotnet\dotnet.exe' run`""

Write-Host "  API starting... (takes ~15 seconds)" -ForegroundColor Green
Write-Host ""

# Wait for it to be ready
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 2
    try {
        $r = Invoke-WebRequest "http://localhost:5000/health" -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -eq 200) {
            Write-Host "  API is ready at http://localhost:5000" -ForegroundColor Green
            Start-Process "$root\Frontend\login.html"
            break
        }
    } catch {}
    Write-Host "  Waiting... ($($i*2)s)" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "  Press Enter to close this window..." -ForegroundColor DarkGray
$null = Read-Host
