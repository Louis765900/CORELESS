# CORELESS

**Outil d'analyse système pour Windows** — vue d'ensemble matérielle, capteurs en direct, benchmarks et export de rapport. Alternative simple et graphique à HWInfo / CPU-Z.

> État actuel : **v0.1 — MVP vue d'ensemble**. Détection matérielle + capteurs live (température, charge, fréquence, tension…) + export TXT. Benchmarks / stress tests et export PDF à venir.

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

## Feuille de route

- [x] Vue d'ensemble matérielle + capteurs live
- [x] Export TXT
- [ ] Export PDF (mise en page graphique)
- [ ] Graphiques temps réel (courbes température / charge)
- [ ] Benchmarks (CPU multi-thread, RAM bande passante, disque I/O)
- [ ] Stress tests avec surveillance de stabilité
