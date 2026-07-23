param (
    [string]$Version = "",
    [int]$Port = 8088
)

$CsprojPath = "$PSScriptRoot\ClientApp\ClientApp.csproj"
if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path $CsprojPath) {
        $xml = [xml](Get-Content $CsprojPath)
        $Version = $xml.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "1.2.9"
    }
}

Write-Host "================================================" -ForegroundColor Cyan
Write-Host " LOCAL LAN UPDATE SERVER FOR FAST TESTING " -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Port: $Port" -ForegroundColor Yellow

# Step 1: Compile WPF App & Inno Setup Installer
Write-Host "`n[1/3] Compiling WPF Client Executable..." -ForegroundColor Cyan
$PublishDir = "$PSScriptRoot\ClientApp\bin\Release\net10.0-windows\win-x64\publish"
if (Test-Path $PublishDir) { Remove-Item -Path "$PublishDir\*" -Recurse -Force }

$publishArgs = "publish `"$CsprojPath`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
Invoke-Expression "dotnet $publishArgs" | Out-Null

$ISCC_System = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$ISCC_User = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
$ISCC = if (Test-Path $ISCC_System) { $ISCC_System } else { $ISCC_User }

Write-Host "[2/3] Compiling Inno Setup Installer..." -ForegroundColor Cyan
$IssPath = "$PSScriptRoot\JobOrderGenerator.iss"
& $ISCC "/DMyAppVersion=$Version" $IssPath | Out-Null

$SetupExe = "$PublishDir\JobOrderGenerator_Setup_v$Version.exe"
if (-not (Test-Path $SetupExe)) {
    Write-Host "Error: Setup file not found at $SetupExe" -ForegroundColor Red
    exit
}

# Step 2: Get Local IP Address
$localIp = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" -and $_.InterfaceAlias -notlike "*vEthernet*" -and $_.InterfaceAlias -notlike "*Loopback*" } | Select-Object -First 1).IPAddress
if ([string]::IsNullOrWhiteSpace($localIp)) {
    $localIp = "127.0.0.1"
}

$localUrl = "http://${localIp}:${Port}/JobOrderGenerator_Setup_v${Version}.exe"

Write-Host "`n[3/3] LOCAL HTTP SERVER READY!" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "Paste this URL into your Cloud Admin Update File URL:" -ForegroundColor Yellow
Write-Host "  $localUrl" -ForegroundColor White -BackgroundColor DarkBlue
Write-Host "=========================================================" -ForegroundColor Green
Write-Host "Client PCs on your Wi-Fi/LAN will download the setup directly from your computer in 1-2 seconds!" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop the local update server when finished testing.`n" -ForegroundColor Gray

# Step 3: Run HTTP Listener Server
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://*:${Port}/")
try {
    $listener.Start()
} catch {
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://localhost:${Port}/")
    $listener.Start()
}

while ($listener.IsListening) {
    try {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response

        $requestedFile = [System.IO.Path]::GetFileName($request.Url.LocalPath)
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Request from $($request.RemoteEndPoint.Address) for file: $requestedFile" -ForegroundColor Cyan

        $targetPath = Join-Path $PublishDir $requestedFile
        if (-not (Test-Path $targetPath)) {
            $targetPath = $SetupExe
        }

        if (Test-Path $targetPath) {
            $bytes = [System.IO.File]::ReadAllBytes($targetPath)
            $response.ContentType = "application/octet-stream"
            $response.ContentLength64 = $bytes.Length
            $response.AddHeader("Content-Disposition", "attachment; filename=$requestedFile")
            $response.OutputStream.Write($bytes, 0, $bytes.Length)
            $response.StatusCode = 200
            Write-Host "   -> Sent $($bytes.Length / 1MB) MB successfully to client!" -ForegroundColor Green
        } else {
            $response.StatusCode = 404
        }
        $response.Close()
    } catch {
        # Listener stopped or client disconnected
    }
}
