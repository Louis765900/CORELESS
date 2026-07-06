# CORELESS

**Outil d'analyse système pour Windows** — vue d'ensemble matérielle, capteurs en direct, benchmarks et export de rapport. Alternative simple et graphique à HWInfo / CPU-Z.

> État actuel : **v0.4 — thème MSI noir/rouge**. Détection matérielle + capteurs live, vue d'ensemble + détail composant, benchmarks (CPU / RAM / disque), stress tests CPU & GPU avec jauges et courbes temps réel, export TXT + PDF graphique.

## Stack

- **.NET 9** / **WPF** (interface native, thème sombre)
- **LibreHardwareMonitorLib** — moteur capteurs bas-niveau (pilote Ring0)
- Architecture **MVVM**

## Prérequis

- Windows 10 / 11 (x64)
- [.NET 9 SDK](https://dotnet.microsoft.com/download) pour compiler
- **Droits administrateur** au lancement (nécessaire pour lire températures / tensions)

## Lancer

```powershell
./run.ps1
```

ou manuellement :

```powershell
dotnet build -c Debug
# puis lancer l'exe en tant qu'administrateur :
src/CORELESS.App/bin/Debug/net9.0-windows/CORELESS.exe
```

## Structure

```
src/CORELESS.App/
  App.xaml                 point d'entrée + thème global
  MainWindow.xaml          coquille UI (barre titre custom, sidebar, contenu)
  Themes/Theme.xaml        palette + styles (cartes, boutons, nav)
  Models/                  SensorFormat, InfoItem
  Mvvm/                    ObservableObject, RelayCommand
  Services/
    HardwareMonitorService boucle capteurs LibreHardwareMonitor
    ReportBuilder          génération rapport texte
  ViewModels/              Main / Component / Sensor / SensorGroup
  Converters/              convertisseurs visibilité
```

## Distribution — exécutable & installeur

Le pipeline produit un **exécutable self-contained mono-fichier** (aucun .NET requis sur la machine cible) et un **installeur Windows** (Inno Setup).

### Prérequis build

- .NET 9 SDK
- [Inno Setup 6](https://jrsoftware.org/isdl.php) pour l'installeur : `winget install JRSoftware.InnoSetup`

### Générer un release complet

```powershell
./build-release.ps1
```

Enchaîne : `dotnet publish` (win-x64 + win-arm64, self-contained, mono-fichier) → compile l'installeur Inno Setup → dépose tout dans `dist/` :

```
dist/
  CORELESS-Setup-v0.4.0.exe      installeur Windows (x64)
  CORELESS-v0.4.0-win-x64.exe    exécutable portable x64
  CORELESS-v0.4.0-win-arm64.exe  exécutable portable arm64 (expérimental)
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
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DMyAppVersion=0.4.0 /DSourceDir=publish\win-x64 installer/coreless.iss
```

### Notes techniques

- **`PublishTrimmed` reste désactivé** : le trimming n'est **pas supporté par WPF** (limitation Microsoft) — l'activer casse l'app.
- **Élévation admin** : le `app.manifest` déclare `requireAdministrator` ; l'exe publié et l'installeur demandent donc l'UAC.
- **arm64** : compilé pour complétude, mais le pilote capteurs (WinRing0) est x64 — sur Windows ARM, préférer le build x64 (émulation).

## Release GitHub

```powershell
# 1. Taguer la version (correspond au <Version> du .csproj)
git tag -a v0.4.0 -m "CORELESS v0.4.0"
git push origin v0.4.0

# 2a. Créer la Release + uploader l'installeur (avec GitHub CLI)
gh release create v0.4.0 dist/CORELESS-Setup-v0.4.0.exe `
  --title "CORELESS v0.4.0" --notes "Analyse système Windows — installeur x64."

# 2b. Sans gh : créer la Release sur github.com/Louis765900/CORELESS/releases/new
#     choisir le tag v0.4.0, puis glisser le .exe depuis dist/.
```

## Feuille de route

- [x] Vue d'ensemble matérielle + capteurs live
- [x] Export TXT + PDF graphique
- [x] Graphiques temps réel (courbes température / charge)
- [x] Benchmarks (CPU multi-thread, RAM bande passante, disque I/O)
- [x] Stress tests CPU & GPU avec surveillance (throttling)
- [x] Thème MSI noir/rouge + logo
- [x] Exécutable distribuable + installeur Windows
- [ ] GPU stress dédié (D3D/compute)
- [ ] Historique / export CSV dans le temps
