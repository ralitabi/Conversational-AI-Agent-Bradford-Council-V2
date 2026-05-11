param(
    [switch]$Frontend,
    [switch]$Backend
)

$doBoth     = -not $Frontend -and -not $Backend
$doFrontend = $Frontend -or $doBoth
$doBackend  = $Backend  -or $doBoth

$root    = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$VERCEL  = "$env:APPDATA\npm\vercel.ps1"
$RAILWAY = "$env:APPDATA\npm\railway.ps1"

$deployErrors = @()

Clear-Host
Write-Host ""
Write-Host "  Bradford Council AI - Deploy" -ForegroundColor Cyan
Write-Host "  $(Get-Date -Format 'dd MMM yyyy  HH:mm')" -ForegroundColor DarkGray
Write-Host "  ----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

if ($doFrontend) {
    Write-Host "  [1] Frontend -> Vercel..." -ForegroundColor Yellow
    Set-Location "$root\Frontend"
    & $VERCEL --prod --yes
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [1] OK  https://bradford-council-ai.vercel.app" -ForegroundColor Green
    } else {
        Write-Host "  [1] FAILED" -ForegroundColor Red
        $deployErrors += "Frontend"
    }
    Write-Host ""
}

if ($doBackend) {
    Write-Host "  [2] Backend -> Railway..." -ForegroundColor Yellow
    Set-Location "$root\Backend"
    & $RAILWAY up --detach --service "bradford-council-api"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [2] OK  Build queued - live in ~3 min" -ForegroundColor Green
        Write-Host "          https://bradford-council-api-production.up.railway.app" -ForegroundColor DarkGray
    } else {
        Write-Host "  [2] FAILED" -ForegroundColor Red
        $deployErrors += "Backend"
    }
    Write-Host ""
}

Write-Host "  ----------------------------------------" -ForegroundColor DarkGray
if ($deployErrors.Count -eq 0) {
    Write-Host "  Done!  https://bradford-council-ai.vercel.app" -ForegroundColor Green
} else {
    Write-Host "  Errors: $($deployErrors -join ', ')" -ForegroundColor Red
}
Write-Host ""