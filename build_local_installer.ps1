# Local Build & Inno Setup Installer Generator Script
# Compiles WPF Single-File Executable & Inno Setup Installer locally WITHOUT uploading to GitHub/Cloud.

param (
    [string]$Version = "1.4.0"
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Building Job Order Generator v$Version Local Setup Package " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Update Version in ClientApp.csproj
$CsprojPath = "$PSScriptRoot\ClientApp\ClientApp.csproj"
if (Test-Path $CsprojPath) {
    Write-Host "`nUpdating version in ClientApp.csproj to $Version..." -ForegroundColor Cyan
    (Get-Content $CsprojPath) -replace "<Version>.*?</Version>", "<Version>$Version</Version>" -replace "<AssemblyVersion>.*?</AssemblyVersion>", "<AssemblyVersion>$Version.0</AssemblyVersion>" -replace "<FileVersion>.*?</FileVersion>", "<FileVersion>$Version.0</FileVersion>" | Set-Content $CsprojPath
}

# 2. Update Version in JobOrderGenerator.iss
$IssPath = "$PSScriptRoot\JobOrderGenerator.iss"
if (Test-Path $IssPath) {
    Write-Host "Updating version in JobOrderGenerator.iss to $Version..." -ForegroundColor Cyan
    (Get-Content $IssPath) -replace '#define MyAppVersion ".*?"', "#define MyAppVersion `"$Version`"" | Set-Content $IssPath
}

# 3. Publish Single-File WPF Executable
Write-Host "`nPublishing WPF Application (Release win-x64 Single File)..." -ForegroundColor Cyan
$ProjectPath = "$PSScriptRoot\ClientApp\ClientApp.csproj"
$PublishDir = "$PSScriptRoot\ClientApp\bin\Release\net10.0-windows\win-x64\publish"

if (Test-Path $PublishDir) {
    Remove-Item -Path "$PublishDir\*" -Recurse -Force -ErrorAction SilentlyContinue
}

$publishArgs = "publish `"$ProjectPath`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
Invoke-Expression "dotnet $publishArgs"

$ExePath = "$PublishDir\JobOrderGenerator.exe"
if (-Not (Test-Path $ExePath)) {
    Write-Host "`nError: Published executable not found at $ExePath" -ForegroundColor Red
    exit 1
}
Write-Host "Single-File Executable published successfully: $ExePath" -ForegroundColor Green

# 4. Check for Inno Setup Compiler (ISCC)
Write-Host "`nChecking for Inno Setup Compiler (ISCC)..." -ForegroundColor Cyan
$ISCC_System = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$ISCC_System64 = "C:\Program Files\Inno Setup 6\ISCC.exe"
$ISCC_User = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"

$ISCC = ""
if (Get-Command iscc -ErrorAction SilentlyContinue) { $ISCC = "iscc" }
elseif (Test-Path $ISCC_System) { $ISCC = $ISCC_System }
elseif (Test-Path $ISCC_System64) { $ISCC = $ISCC_System64 }
elseif (Test-Path $ISCC_User) { $ISCC = $ISCC_User }

if ($ISCC -eq "") {
    Write-Host "Inno Setup Compiler not found in standard paths. Downloading silently..." -ForegroundColor Yellow
    $installerPath = "$env:TEMP\innosetup.exe"
    try {
        Invoke-WebRequest -Uri "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe" -OutFile $installerPath
        Write-Host "Installing Inno Setup..." -ForegroundColor Cyan
        Start-Process -FilePath $installerPath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CURRENTUSER" -Wait -NoNewWindow
        $ISCC = $ISCC_User
    } catch {
        Write-Host "Could not download Inno Setup automatically. Please compile $IssPath manually using Inno Setup." -ForegroundColor Red
    }
}

if ($ISCC -ne "") {
    Write-Host "Compiling Inno Setup Installer..." -ForegroundColor Cyan
    & $ISCC "/DMyAppVersion=$Version" $IssPath | Out-Host

    $SetupExePath = "$PublishDir\JobOrderGenerator_Setup_v$Version.exe"
    if (Test-Path $SetupExePath) {
        Write-Host "`n============================================================" -ForegroundColor Green
        Write-Host " LOCAL INSTALLER READY FOR TESTING! " -ForegroundColor Green
        Write-Host " Output Setup File: $SetupExePath " -ForegroundColor Green
        Write-Host "============================================================" -ForegroundColor Green
    } else {
        Write-Host "`nInno Setup compilation finished. Check OutputDir ($PublishDir) for setup file." -ForegroundColor Yellow
    }
}
