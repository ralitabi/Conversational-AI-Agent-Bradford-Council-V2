# Bradford Council AI — Deploy
# Usage:
#   Right-click → Run with PowerShell    (deploys both)
#   .\deploy.ps1                          (deploys both)
#   .\deploy.ps1 -Frontend               (frontend only)
#   .\deploy.ps1 -Backend                (backend only)

param(
    [switch]$Frontend,
    [switch]$Backend
)

# If neither flag given, deploy both
$doBoth     = -not $Frontend -and -not $Backend
$doFrontend = $Frontend -or $doBoth
$doBackend  = $Backend  -or $doBoth

# Resolve project root (works from IDE terminal, right-click, or double-click)
$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

$errors = @()

Clear-Host
Write-Host ""
Write-Host "  Bradford Council AI — Deploy" -ForegroundColor Cyan
Write-Host "  $(Get-Date -Format 'dd MMM yyyy  HH:mm')" -ForegroundColor DarkGray
Write-Host "  ─────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# ── 1. Frontend → Vercel ─────────────────────────────────────────────────────
if ($doFrontend) {
    Write-Host "  [Frontend]  Deploying to Vercel..." -ForegroundColor Yellow
    Set-Location "$root\Frontend"

    $vercelOut = vercel --prod --yes 2>&1
    $vercelExit = $LASTEXITCODE

    if ($vercelExit -eq 0) {
        Write-Host "  [Frontend]  Live at https://bradford-council-ai.vercel.app" -ForegroundColor Green
    } else {
        Write-Host "  [Frontend]  FAILED (exit $vercelExit)" -ForegroundColor Red
        Write-Host ($vercelOut | Select-Object -Last 5 | Out-String).Trim() -ForegroundColor DarkRed
        $errors += "Frontend"
    }
    Write-Host ""
}

# ── 2. Backend → Railway ──────────────────────────────────────────────────────
if ($doBackend) {
    Write-Host "  [Backend]   Deploying to Railway..." -ForegroundColor Yellow
    Set-Location "$root\Backend"

    $railOut = railway up --detach --service "bradford-council-api" 2>&1
    $railExit = $LASTEXITCODE

    if ($railExit -eq 0) {
        Write-Host "  [Backend]   Build queued — live in ~3 min" -ForegroundColor Green
        Write-Host "              https://bradford-council-api-production.up.railway.app" -ForegroundColor DarkGray
    } else {
        Write-Host "  [Backend]   FAILED (exit $railExit)" -ForegroundColor Red
        Write-Host ($railOut | Select-Object -Last 5 | Out-String).Trim() -ForegroundColor DarkRed
        $errors += "Backend"
    }
    Write-Host ""
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host "  ─────────────────────────────────────────" -ForegroundColor DarkGray

if ($errors.Count -eq 0) {
    Write-Host "  All done!  https://bradford-council-ai.vercel.app" -ForegroundColor Green
} else {
    Write-Host "  Deploy errors in: $($errors -join ', ')" -ForegroundColor Red
    Write-Host "  Check Railway / Vercel dashboards for details." -ForegroundColor DarkGray
}

Write-Host ""

# Keep window open only when run by double-click (no parent process that is a terminal)
$parentName = (Get-Process -Id (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId -ErrorAction SilentlyContinue).Name
if ($parentName -eq "explorer") {
    Write-Host "  Press Enter to close..." -ForegroundColor DarkGray
    $null = Read-Host
}
