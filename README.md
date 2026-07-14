<div align="center">

# CORELESS

**Le moniteur matériel Windows qui fait tout — capteurs, benchmarks, stress tests, rapports.**

Alternative simple, rapide et graphique à HWiNFO / CPU-Z / CrystalDiskMark, réunis dans une seule app.

[**Télécharger pour Windows**](https://github.com/Louis765900/CORELESS/releases/latest) · [Site officiel](https://coreless.vercel.app) · [Signaler un bug](https://github.com/Louis765900/CORELESS/issues)

![version](https://img.shields.io/badge/version-0.5.0-E4002B?style=flat-square) ![platform](https://img.shields.io/badge/plateforme-Windows%2010%2F11-1E1E22?style=flat-square) ![license](https://img.shields.io/badge/licence-MIT-8A8F98?style=flat-square)

</div>

---

## Pourquoi CORELESS

La plupart des outils de monitoring font **une seule chose** : soit les capteurs (HWiNFO), soit un benchmark (Cinebench), soit le disque (CrystalDiskMark). Pour avoir une vue complète de sa machine il faut installer cinq logiciels différents, tous avec une interface différente d'il y a quinze ans.

**CORELESS regroupe tout ça dans une seule app**, avec un thème sombre soigné, des graphiques temps réel, et un export de rapport en un clic.

## Fonctionnalités

| | |
|---|---|
| 🖥️ **Vue d'ensemble matérielle** | CPU, GPU, RAM, cartes mères, disques — détectés automatiquement |
| 🌡️ **Capteurs en direct** | Températures, charge, fréquences, tensions, ventilateurs — rafraîchis en continu avec min/max |
| ⚡ **Suite de benchmarks maison** | Rendu 3D (ray tracer), calcul Pi haute précision, compression, mémoire, disque — voir détail ci-dessous |
| 🔥 **Stress tests CPU & GPU** | Charge maximale avec jauges et courbes temps réel, détection du throttling thermique |
| 📄 **Export de rapport** | TXT ou PDF graphique complet (composants, capteurs, scores) en un clic |
| 🎨 **Interface soignée** | Thème noir/rouge, coins nets, animations fluides — pensé pour être lisible, pas juste fonctionnel |

### La suite de benchmarks

CORELESS n'embarque **aucun logiciel tiers** (ils sont propriétaires et fermés). Il fournit ses **propres moteurs**, inspirés des références du marché :

| Module CORELESS | Dans l'esprit de | Ce qui est mesuré |
|---|---|---|
| Rendu 3D (CPU) | Cinebench | Ray tracer managé — débit mono et multi-cœur (Mpx/s) |
| Calcul Pi | y-cruncher | Précision arbitraire, Chudnovsky + binary splitting |
| Compression | 7-Zip | Deflate multi-thread — débit + taux de compression |
| Mémoire | AIDA64 | Bande passante lecture/écriture/copie + latence + test d'intégrité |
| Stockage | CrystalDiskMark | Grille SEQ1M / RND4K en Q1 et Q32 (Mo/s + IOPS) |
| **Indice CORELESS** | PCMark / Novabench | Score global unique, pondéré sur tous les modules |

> Le test mémoire vérifie la RAM allouée en espace utilisateur (pas la RAM totale comme MemTest86, qui exige un boot hors OS). Les débits disque sont influencés par le cache du système.

## Installation

1. Va sur la page [**Releases**](https://github.com/Louis765900/CORELESS/releases/latest)
2. Télécharge `CORELESS-Setup-vX.Y.Z.exe`
3. Lance l'installeur — Windows affichera **« Éditeur inconnu »** (normal, l'app n'est pas encore signée avec un certificat payant, voir [docs/BUILD.md](docs/BUILD.md))
4. Au premier lancement, accepte l'invite **UAC** — les droits administrateur sont nécessaires pour lire températures et tensions

Un exécutable portable (sans installation) est aussi disponible sur la même page.

**Prérequis :** Windows 10/11, x64. Aucune dépendance à installer — l'app est autonome.

## Utilisation

- **Vue d'ensemble** : tous tes composants d'un coup d'œil, températures et charges en direct
- **Détail composant** : clique un composant pour voir tous ses capteurs individuels avec historique
- **Benchmarks & Stress** : lance un test individuel ou **TOUT TESTER** pour la suite complète + l'indice global
- **Export** : bouton export en haut à droite → choisis TXT (rapide) ou PDF (rapport complet mis en page)

## Pour les développeurs

Envie de compiler depuis les sources, contribuer, ou publier ta propre release ? Tout est documenté dans **[docs/BUILD.md](docs/BUILD.md)** : build local, pipeline de release, signature de code, structure du projet.

```powershell
git clone https://github.com/Louis765900/CORELESS.git
cd CORELESS
./run.ps1
```

## Feuille de route

- [x] Vue d'ensemble matérielle + capteurs live
- [x] Export TXT + PDF graphique
- [x] Graphiques temps réel
- [x] Suite de benchmarks maison (rendu 3D, Pi, compression, mémoire, CrystalDiskMark-style) + indice global
- [x] Stress tests CPU & GPU avec détection de throttling
- [x] Exécutable distribuable + installeur Windows
- [ ] GPU stress/benchmark dédié (D3D/compute)
- [ ] Historique / export CSV dans le temps
- [ ] Lecture SMART (santé disque, type CrystalDiskInfo)

## Licence

MIT — voir [LICENSE](LICENSE).
