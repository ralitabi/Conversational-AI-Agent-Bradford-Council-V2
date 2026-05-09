# Serves the Bradford Council website on http://localhost:8080
$port = 8080
$path = "$PSScriptRoot\Frontend"

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$port/")
$listener.Start()
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  Bradford Council Website" -ForegroundColor Cyan
Write-Host "  Open: http://localhost:$port" -ForegroundColor Green
Write-Host "  Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$mimeTypes = @{
  ".html" = "text/html; charset=utf-8"
  ".css"  = "text/css"
  ".js"   = "application/javascript"
  ".png"  = "image/png"
  ".jpg"  = "image/jpeg"
  ".svg"  = "image/svg+xml"
  ".ico"  = "image/x-icon"
}

try {
  while ($listener.IsListening) {
    $ctx  = $listener.GetContext()
    $req  = $ctx.Request
    $res  = $ctx.Response

    $localPath = $req.Url.LocalPath
    if ($localPath -eq '/') { $localPath = '/index.html' }
    $filePath = Join-Path $path $localPath.TrimStart('/')

    if (Test-Path $filePath -PathType Leaf) {
      $ext  = [System.IO.Path]::GetExtension($filePath)
      $mime = if ($mimeTypes[$ext]) { $mimeTypes[$ext] } else { "application/octet-stream" }
      $bytes = [System.IO.File]::ReadAllBytes($filePath)
      $res.ContentType   = $mime
      $res.ContentLength64 = $bytes.Length
      $res.Headers.Add("Access-Control-Allow-Origin", "*")
      $res.OutputStream.Write($bytes, 0, $bytes.Length)
    } else {
      $res.StatusCode = 404
    }
    $res.OutputStream.Close()
  }
} finally {
  $listener.Stop()
}
