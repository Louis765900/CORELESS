<#
    CORELESS — release build pipeline
    ---------------------------------
    Publishes self-contained single-file builds (win-x64 + win-arm64),
    optionally Authenticode-signs them, then compiles (and signs) the
    Inno Setup installer, dropping artifacts in dist/.

    Usage:
        ./build-release.ps1                          # x64 + arm64 + installer, unsigned
        ./build-release.ps1 -SkipArm64               # x64 only
        ./build-release.ps1 -SkipInstaller           # portable exes only
        ./build-release.ps1 -CertThumbprint <hex>    # sign exes + installer

    Requirements:
        - .NET 9 SDK
        - Inno Setup 6 (ISCC.exe)          -> winget install JRSoftware.InnoSetup
        - (signing) a code-signing certificate + signtool.exe (Windows SDK)

    Code signing (gives the "Verified publisher" line in UAC):
        Buy a code-signing cert (Certum Open Source is the cheapest for individuals;
        OV/EV from DigiCert/Sectigo otherwise). Import it (or plug the token), find
        its SHA1 thumbprint:  Get-ChildItem Cert:\CurrentUser\My | Format-List Subject,Thumbprint
        Then:  ./build-release.ps1 -CertThumbprint ABCD...   (token PIN is prompted).
        Cloud-HSM certs (DigiCert KeyLocker, Certum cloud): set -SignToolPath to your
        provider's signtool wrapper, or sign manually per their docs.
#>
[CmdletBinding()]
param(
    [switch]$SkipArm64,
    [switch]$SkipInstaller,
    [string]$Configuration = 'Release',
    [string]$CertThumbprint,                                   # SHA1 thumbprint of the signing cert
    [string]$TimestampUrl = 'http://timestamp.digicert.com',   # RFC-3161 timestamp server
    [string]$SignToolPath                                      # optional explicit signtool.exe
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

# --- signtool discovery -------------------------------------------------
$script:SignTool = $null
function Resolve-SignTool {
    if ($SignToolPath) { return $SignToolPath }
    $cmd = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
    if ($cmd) { return $cmd }
    $sdk = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $sdk) {
        $found = Get-ChildItem $sdk -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -match '\\x64\\' } |
                 Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

function Invoke-Sign([string]$path) {
    if (-not $CertThumbprint) { return }               # signing disabled
    if (-not $script:SignTool) {
        $script:SignTool = Resolve-SignTool
        if (-not $script:SignTool) {
            Write-Warning 'signtool.exe not found (install the Windows SDK) — skipping signing.'
            $script:CertThumbprint = $null; return
        }
    }
    Write-Host "  signing $([System.IO.Path]::GetFileName($path))" -ForegroundColor DarkGray
    & $script:SignTool sign /sha1 $CertThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $path | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "signtool failed on $path" }
}

# --- version from csproj ------------------------------------------------
$csproj = Get-Content $proj -Raw
$version = [regex]::Match($csproj, '<Version>([^<]+)</Version>').Groups[1].Value
if (-not $version) { $version = '0.0.0' }
Write-Host "CORELESS release v$version$(if($CertThumbprint){' (signed)'})" -ForegroundColor Green

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

    # sign the published exe (installer then bundles the signed binary)
    $exe = Join-Path $outDir 'CORELESS.exe'
    Invoke-Sign $exe

    # portable copy into dist/ (carries the signature)
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
        $setup = Join-Path $distDir "CORELESS-Setup-v$version.exe"
        Invoke-Sign $setup
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
if (-not $CertThumbprint) {
    Write-Host 'NOTE: artifacts are UNSIGNED (UAC shows "Unknown publisher", SmartScreen may warn).' -ForegroundColor Yellow
    Write-Host '      Pass -CertThumbprint <hex> once you have a code-signing certificate.' -ForegroundColor Yellow
}
