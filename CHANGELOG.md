# Changelog

Toutes les modifications notables de CORELESS sont documentées ici. Reconstruit à partir de l'historique Git (pas de tags formels avant cette version).

## v0.5.0 — 2026-07-14

- Suite de benchmarks maison : rendu 3D (ray tracer CPU), calcul Pi (Chudnovsky), compression (Deflate multi-thread), suite mémoire (bande passante + latence + intégrité), suite disque (grille SEQ1M/RND4K, Q1/Q32).
- Indice CORELESS : score global pondéré calculé sur les 5 modules exécutés via "Tout tester".
- Pipeline de signature de code + métadonnées éditeur pour l'exécutable et l'installeur.

## v0.4.x — 2026-07-08 → 2026-07-14

- Correctif installeur : lancement de l'app après installation via shellexec (résout l'erreur 740 liée à l'élévation).
- Build autonome (self-contained) + installeur Windows (Inno Setup), exécutables portables win-x64/win-arm64.
- Site vitrine : téléchargement direct de l'installeur depuis le site, puis bascule vers les GitHub Releases pour plus de fiabilité.
- Site vitrine complet ("Kinetic HUD"), page d'aperçu, logo réel, sparklines dans la vue d'ensemble, vue détail composant enrichie, stress tests peaufinés.

## v0.1 — v0.3 (base)

- Détection matérielle via LibreHardwareMonitorLib (CPU, GPU, RAM, carte mère, stockage, réseau, capteurs).
- Vue d'ensemble + vue détail par composant, capteurs live avec min/max.
- Export de rapport TXT et PDF (QuestPDF).
- Stress tests CPU & GPU avec détection de throttling thermique.
- Thème noir/rouge façon MSI.
