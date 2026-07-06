<#
    CORELESS — release build pipeline
    ---------------------------------
    Publishes self-contained single-file builds (win-x64 + win-arm64),
    then compiles the Inno Setup installer, dropping artifacts in dist/.

    Usage:
        ./build-release.ps1                 # x64 + arm64 + installer
        ./build-release.ps1 -SkipArm64      # x64 only
        ./build-release.ps1 -SkipInstaller  # portable exes only, no setup

    Requirements:
        - .NET 9 SDK
        - Inno Setup 6 (ISCC.exe) for the installer  ->  winget install JRSoftware.InnoSetup
#>
[CmdletBinding()]
param(
    [switch]$SkipArm64,
    [switch]$SkipInstaller,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src\CORELESS.App\CORELESS.App.csproj'
$iss  = Join-Path $root 'installer\coreless.iss'
$distDir = Join-Path $root 'dist'

# Make sure dotnet (freshly installed) is on PATH for this session.
$env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path','User')

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# --- version from csproj ------------------------------------------------
$csproj = Get-Content $proj -Raw
$version = [regex]::Match($csproj, '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $version) { $version = '0.0.0' }
Write-Host "CORELESS release v$version" -ForegroundColor Green

# --- clean output -------------------------------------------------------
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

function Publish-Rid([string]$rid) {
    Write-Step "Publish $rid (self-contained, single-file)"
    $outDir = Join-Path $root "publish\$rid"
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

    # Pipe dotnet output to the host so it is NOT captured as this function's
    # return value (otherwise $srcX64 would be polluted with build log lines).
    dotnet publish $proj -c $Configuration -r $rid --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:DebugType=none -p:DebugSymbols=false `
        -o $outDir | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "publish $rid failed" }

    # portable copy into dist/
    $exe = Join-Path $outDir 'CORELESS.exe'
    if (Test-Path $exe) {
        Copy-Item $exe (Join-Path $distDir "CORELESS-v$version-$rid.exe") -Force
    }
    return $outDir
}

# --- publish ------------------------------------------------------------
$srcX64 = Publish-Rid 'win-x64'
if (-not $SkipArm64) {
    try { Publish-Rid 'win-arm64' | Out-Null }
    catch { Write-Warning "arm64 publish failed (LibreHardwareMonitor is x64-only; arm64 is experimental): $_" }
}

# --- installer ----------------------------------------------------------
if (-not $SkipInstaller) {
    Write-Step 'Build Inno Setup installer'
    $iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
    if (-not $iscc) {
        foreach ($p in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
                         "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                         "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
            if (Test-Path $p) { $iscc = $p; break }
        }
    }

    if ($iscc) {
        & $iscc "/DMyAppVersion=$version" "/DSourceDir=$srcX64" $iss
        if ($LASTEXITCODE -ne 0) { throw 'ISCC failed' }
        Write-Host "Installer -> dist\CORELESS-Setup-v$version.exe" -ForegroundColor Green
    }
    else {
        Write-Warning 'Inno Setup (ISCC.exe) not found — skipping installer.'
        Write-Warning 'Install it with:  winget install JRSoftware.InnoSetup   then re-run.'
    }
}

# --- summary ------------------------------------------------------------
Write-Step 'Artifacts (dist/)'
Get-ChildItem $distDir | Select-Object Name, @{n='Size(MB)';e={[math]::Round($_.Length/1MB,1)}} | Format-Table -Auto
