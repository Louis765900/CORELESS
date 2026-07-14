# Build & distribution

Documentation technique pour compiler, packager et publier CORELESS. Pour l'installation et l'usage normal, voir le [README](../README.md).

## Stack

- **.NET 9** / **WPF** (interface native, thème sombre)
- **LibreHardwareMonitorLib** — moteur capteurs bas niveau (pilote Ring0)
- **QuestPDF** — export PDF
- Architecture **MVVM**

## Structure du projet

```
src/CORELESS.App/
  App.xaml                 point d'entrée + thème global
  MainWindow.xaml           coquille UI (barre titre custom, sidebar, contenu)
  Themes/Theme.xaml         palette + styles (cartes, boutons, nav)
  Models/                   SensorFormat, InfoItem
  Mvvm/                     ObservableObject, RelayCommand
  Services/
    HardwareMonitorService  boucle capteurs LibreHardwareMonitor
    ReportBuilder           génération rapport texte
    PdfReportBuilder        génération rapport PDF (QuestPDF)
    Benchmarks/             moteurs de la suite de benchmarks
  ViewModels/                Main / Component / Sensor / SensorGroup / Benchmark
  Controls/                  TrendChart, RadialGauge, LogoMark (contrôles WPF custom)
  Converters/                convertisseurs visibilité
```

## Compiler en local

Prérequis : [.NET 9 SDK](https://dotnet.microsoft.com/download).

```powershell
./run.ps1
```

ou manuellement :

```powershell
dotnet build -c Debug
# puis lancer l'exe en tant qu'administrateur (requis pour lire températures/tensions) :
src/CORELESS.App/bin/Debug/net9.0-windows/CORELESS.exe
```

## Générer un release complet

Le pipeline produit un **exécutable self-contained mono-fichier** (aucun .NET requis sur la machine cible) et un **installeur Windows** (Inno Setup).

Prérequis supplémentaire : [Inno Setup 6](https://jrsoftware.org/isdl.php) — `winget install JRSoftware.InnoSetup`.

```powershell
./build-release.ps1
```

Enchaîne : `dotnet publish` (win-x64 + win-arm64, self-contained, mono-fichier) → compile l'installeur Inno Setup → dépose tout dans `dist/` :

```
dist/
  CORELESS-Setup-vX.Y.Z.exe      installeur Windows (x64)
  CORELESS-vX.Y.Z-win-x64.exe    exécutable portable x64
  CORELESS-vX.Y.Z-win-arm64.exe  exécutable portable arm64 (expérimental)
```

Options :

```powershell
./build-release.ps1 -SkipArm64      # x64 uniquement
./build-release.ps1 -SkipInstaller  # exes portables seulement
```

### Publier manuellement (sans le script)

```powershell
dotnet publish src/CORELESS.App/CORELESS.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true `
  -o publish/win-x64

# puis l'installeur :
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DMyAppVersion=X.Y.Z /DSourceDir=publish\win-x64 installer/coreless.iss
```

### Notes techniques

- **`PublishTrimmed` reste désactivé** : le trimming n'est pas supporté par WPF (limitation Microsoft) — l'activer casse l'app.
- **Élévation admin** : `app.manifest` déclare `requireAdministrator` ; l'exe publié et l'installeur demandent donc l'UAC.
- **arm64** : compilé pour complétude, mais le pilote capteurs (WinRing0) est x64 — sur Windows ARM, préférer le build x64 (émulation).

## Signature de code (« Éditeur vérifié »)

Non signé, l'UAC affiche **« Éditeur inconnu »** et SmartScreen peut avertir. La ligne **« Éditeur vérifié »** (comme HWiNFO) vient uniquement d'une signature Authenticode avec un certificat payant.

| Type | Prix indicatif | Effet |
|---|---|---|
| **Certum Open Source Code Signing** | ~100–150 €/an | Éditeur vérifié. Le moins cher pour un particulier / projet OSS. |
| **OV** (DigiCert, Sectigo, GlobalSign) | ~200–400 €/an | Éditeur vérifié. SmartScreen avertit jusqu'à réputation acquise. |
| **EV** (token matériel) | ~300–600 €/an | Éditeur vérifié + SmartScreen OK immédiatement. |

Depuis 2023, la clé privée doit être sur un token USB ou un HSM cloud (plus de simple `.pfx`).

Une fois le certificat importé (ou le token branché), récupère son empreinte :

```powershell
Get-ChildItem Cert:\CurrentUser\My | Format-List Subject, Thumbprint
```

Puis build + signature automatique de l'exe et de l'installeur :

```powershell
./build-release.ps1 -CertThumbprint <EMPREINTE_SHA1>
```

Nécessite `signtool.exe` (Windows SDK). Pour un HSM cloud (DigiCert KeyLocker, Certum cloud), passe `-SignToolPath` vers le wrapper du fournisseur. Les métadonnées éditeur (Société, Produit, Copyright) sont déjà dans le `.csproj` et le `.iss`.

## Publier une release GitHub

```powershell
# 1. Bump <Version> dans src/CORELESS.App/CORELESS.App.csproj, puis tag
git tag -a vX.Y.Z -m "CORELESS vX.Y.Z"
git push origin vX.Y.Z

# 2. Build
./build-release.ps1

# 3a. Créer la Release + uploader les binaires (avec GitHub CLI)
gh release create vX.Y.Z dist/CORELESS-Setup-vX.Y.Z.exe dist/CORELESS-vX.Y.Z-win-x64.exe dist/CORELESS-vX.Y.Z-win-arm64.exe `
  --title "CORELESS vX.Y.Z" --notes "..."

# 3b. Sans gh : github.com/Louis765900/CORELESS/releases/new
#     choisir le tag, glisser les .exe depuis dist/.

# 4. Mettre à jour les liens de téléchargement du site (site/download.html)
```
