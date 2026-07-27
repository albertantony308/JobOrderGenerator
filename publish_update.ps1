param (
    [string]$GithubToken = "",
    [string]$Version = "",
    [string]$Changelog = ""
)

if ([string]::IsNullOrWhiteSpace($GithubToken)) {
    $GithubToken = Read-Host -Prompt "Enter your GitHub Personal Access Token (PAT)"
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Read-Host -Prompt "Enter new version (e.g. 1.2.0)"
}
if ([string]::IsNullOrWhiteSpace($Changelog)) {
    $Changelog = Read-Host -Prompt "Enter changelog/description for this release"
}

$RepoOwner = "albertantony308"
$RepoName = "JobOrderGenerator"
$Headers = @{
    "Authorization" = "token $GithubToken"
    "Accept" = "application/vnd.github.v3+json"
    "User-Agent" = "PowerShell-Auto-Publisher"
}

Write-Host "Checking if repository $RepoOwner/$RepoName exists..." -ForegroundColor Cyan
try {
    $repoCheck = Invoke-RestMethod -Uri "https://api.github.com/repos/$RepoOwner/$RepoName" -Headers $Headers -Method Get
    Write-Host "Repository exists." -ForegroundColor Green
} catch {
    Write-Host "Repository does not exist. Creating it now..." -ForegroundColor Yellow
    $body = @{
        name = $RepoName
        private = $false
        description = "Job Order Generator Client Application Releases"
    } | ConvertTo-Json
    try {
        $createRepo = Invoke-RestMethod -Uri "https://api.github.com/user/repos" -Headers $Headers -Method Post -Body $body -ContentType "application/json"
        Write-Host "Repository created successfully." -ForegroundColor Green
    } catch {
        Write-Host "Failed to create repository: $_" -ForegroundColor Red
        exit
    }
}

Write-Host "`nUpdating version in ClientApp.csproj and JobOrderGenerator.iss..." -ForegroundColor Cyan
$CsprojPath = "$PSScriptRoot\ClientApp\ClientApp.csproj"
if (Test-Path $CsprojPath) {
    (Get-Content $CsprojPath) -replace "<Version>.*?</Version>", "<Version>$Version</Version>" -replace "<AssemblyVersion>.*?</AssemblyVersion>", "<AssemblyVersion>$Version.0</AssemblyVersion>" -replace "<FileVersion>.*?</FileVersion>", "<FileVersion>$Version.0</FileVersion>" | Set-Content $CsprojPath
}
$IssPath = "$PSScriptRoot\JobOrderGenerator.iss"
if (Test-Path $IssPath) {
    (Get-Content $IssPath) -replace '#define MyAppVersion ".*?"', "#define MyAppVersion `"$Version`"" | Set-Content $IssPath
}

Write-Host "`nPublishing WPF Application as Single File Executable..." -ForegroundColor Cyan
$ProjectPath = "$PSScriptRoot\ClientApp\ClientApp.csproj"
$PublishDir = "$PSScriptRoot\ClientApp\bin\Release\net10.0-windows\win-x64\publish"

# Clean previous publishes
if (Test-Path $PublishDir) { Remove-Item -Path "$PublishDir\*" -Recurse -Force }

# Run dotnet publish
$publishArgs = "publish `"$ProjectPath`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
Invoke-Expression "dotnet $publishArgs"

$ExePath = "$PublishDir\JobOrderGenerator.exe"
if (-Not (Test-Path $ExePath)) {
    Write-Host "Error: Published executable not found at $ExePath" -ForegroundColor Red
    exit
}
Write-Host "App published successfully to $ExePath" -ForegroundColor Green

Write-Host "`nCreating GitHub Release v$Version..." -ForegroundColor Cyan
$releaseBody = @{
    tag_name = "v$Version"
    name = "v$Version"
    body = $Changelog
} | ConvertTo-Json

try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$RepoOwner/$RepoName/releases" -Headers $Headers -Method Post -Body $releaseBody -ContentType "application/json"
    Write-Host "Release created with ID $($release.id)" -ForegroundColor Green
} catch {
    Write-Host "Failed to create release: $_" -ForegroundColor Red
    exit
}

function Upload-ToGitHub {
    param([string]$FilePath, [string]$FileName)
    $uploadUrl = $release.upload_url -replace '\{\?name,label\}', "?name=$FileName"
    Write-Host "`nUploading $FileName to GitHub Release Assets (Showing Live Progress)..." -ForegroundColor Cyan
    try {
        $curlArgs = @(
            "--fail",
            "-X", "POST",
            "-H", "Authorization: token $GithubToken",
            "-H", "Accept: application/vnd.github.v3+json",
            "-H", "Content-Type: application/octet-stream",
            "--data-binary", "@$FilePath",
            $uploadUrl
        )
        & curl.exe @curlArgs
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`n$FileName uploaded successfully!" -ForegroundColor Green
        } else {
            Write-Host "`nFailed to upload $FileName using curl. Exit code: $LASTEXITCODE" -ForegroundColor Red
            exit
        }
    } catch {
        Write-Host "Failed to upload $($FileName): $_" -ForegroundColor Red
        exit
    }
}

Write-Host "`nChecking for Inno Setup Compiler..." -ForegroundColor Cyan
$ISCC_System = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$ISCC_User = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"

$ISCC = ""
if (Get-Command iscc -ErrorAction SilentlyContinue) { $ISCC = "iscc" }
elseif (Test-Path $ISCC_System) { $ISCC = $ISCC_System }
elseif (Test-Path $ISCC_User) { $ISCC = $ISCC_User }

if ($ISCC -eq "") {
    Write-Host "Inno Setup not found. Downloading and installing silently..." -ForegroundColor Yellow
    $installerPath = "$env:TEMP\innosetup.exe"
    Invoke-WebRequest -Uri "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe" -OutFile $installerPath
    Write-Host "Installing Inno Setup..." -ForegroundColor Cyan
    Start-Process -FilePath $installerPath -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CURRENTUSER" -Wait -NoNewWindow
    $ISCC = $ISCC_User
}

Write-Host "`nCompiling Inno Setup Installer..." -ForegroundColor Cyan
$IssPath = "$PSScriptRoot\JobOrderGenerator.iss"
& $ISCC "/DMyAppVersion=$Version" $IssPath | Out-Host

$SetupExePath = "$PublishDir\JobOrderGenerator_Setup_v$Version.exe"
if (Test-Path $SetupExePath) {
    Upload-ToGitHub -FilePath $SetupExePath -FileName "JobOrderGenerator_Setup_v$Version.exe"
    Write-Host "`nDONE! The release v$Version setup installer is now live on GitHub." -ForegroundColor Green
} else {
    Write-Host "`nFailed to generate Inno Setup executable. Setup not found at $SetupExePath" -ForegroundColor Red
}
