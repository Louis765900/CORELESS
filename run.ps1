# CORELESS — build + lancement (élévation admin requise pour capteurs profonds)
$ErrorActionPreference = 'Stop'
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

Push-Location $PSScriptRoot
try {
    dotnet build -c Debug -v minimal
    $exe = Join-Path $PSScriptRoot 'src\CORELESS.App\bin\Debug\net9.0-windows\CORELESS.exe'
    Start-Process -FilePath $exe -Verb RunAs
} finally {
    Pop-Location
}
