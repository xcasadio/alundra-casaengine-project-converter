# E20.eR: read-only preparation of 2026-10-22 (revision 0 of the plan), kept verbatim

The section « (2) Texte de plan proposé » is left out: it is in `docs/plan-e20-heros.md` §1.27a. `S/` in the text is the
session scratchpad folder `prep-eR/`, whose scripts and lists are copied next to this file (names in `README.md`).

**Conclusion.** Tout est établi sans rien modifier dans le dépôt, le remaster ni `data-extracted/`. J'ai fait une répétition à blanc entièrement dans le scratchpad (voir 1.D). Elle prédit des écarts précis (483 fichiers à la ré-extraction, 966 à l'export). Je n'ai pas fait tourner le convertisseur lui-même ; l'export 966 reste donc une prédiction, qu'il faudra confirmer par le vrai export.

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/prep-eR/`, noté `S/` ci-dessous.

Réponse à la question posée : l'exporteur n'a **aucun mode « cartes seules »**. Il faut tout ré-extraire, ce qui prend 73 s et produit 4 536 fichiers (4 Go).

---

## (1) Faits vérifiés au HEAD 1d0b6953

### A. État des dépôts et des dossiers
- **Parent.** Branche `chantier/theme1-porter-plateforme`. Le statut montre :
  - ` m CasaEngineMonogame`, dont `CasaEngine.Launcher/Program.cs` modifié ; pointeur `92d8e847`.
  - ` m alundra-datas-analyser`.
  - `?? tools/Convert-All-AlundraSaves.ps1`, `tools/Convert-AlundraSave.ps1` et `tools/Saves/`.
- **Analyseur.**
  - HEAD `fc16872` = pointeur du parent (`git ls-tree`) = `master` = `origin/master` = `chantier/e19-suite3`, qui est la branche courante.
  - Modifications locales de l'auteur : pointeur `MGUI` (` m MGUI`, `heads/develop 3db3c769`) et `?? AlundraTools/log.txt`.
  - `core.autocrlf=true` : index en LF, copie de travail en CRLF.
  - `AlundraTools/AlundraDataExtractor/bin/Release` existe (DLL du 2026-10-08 20:04) mais n'est pas à reprendre : une construction fraîche est nécessaire.
- **Gitignore.**
  - `data-extracted/` est ignoré (`.gitignore:60`), 0 fichier suivi.
  - `alundra-project/*` est ignoré sauf `UI/Screens/` (`.gitignore:64-67`).
  - L'extraction et l'export ne produisent donc aucun changement git.
- **Remaster = copie.**
  - `diff -rq "D:/development/repo/Alundra Remake/remaster-data-extracted" data-extracted` est vide (8 s).
  - Les deux arbres ont 4 536 fichiers ; le remaster date du 2026-10-08 20:05 et pèse 4,0 Go.
  - Huit dossiers de sauvegarde d'environ 4 Go existent déjà : `data-extracted.bak-{e19g0, e19g0b, lb, x2}` et `remaster-data-extracted.bak-{2026-10-03, e19g0b, lb, x2}`.
  - D: a 80 Go libres sur 954 (92 % utilisé).
- **Projet exporté.**
  - `alundra-project/report.json` date du 2026-10-17 21:52. Il indique `OutputFileCount` 24 107 et `OutputSizeBytes` 1 955 373 164.
  - Les commits convertisseur postérieurs (`15804ae3`, `a25b815d`, `2964d9ae`) ne portent que des commentaires, hors le deuxième export de R4 qui date du même soir.
  - Le motif `BalanceLevel` apparaît exactement une fois dans chacun des 483 `.tmj` et des 483 `.tileMap` du projet, plus `Data/balance.json` (un autre champ, 613 occurrences). Cela fait 967 fichiers.
  - `AssetInfos.json` ne porte que id, nom, chemin et type (`AssetInfos.json:785-788`) : ni empreinte ni taille.
  - Aucun processus Launcher, jeu ou éditeur n'est actif.

### B. L'exporteur de l'analyseur
- **Lancement et absence de mode « cartes seules ».**
  - `AlundraTools/AlundraDataExtractor/Program.cs:77-151` (`Main`) est une séquence unique.
  - Les `--` des lignes 81-115 sont BGM, films, SFX et portraits.
  - Seules options de disposition : `--tiled-tileset-layout` et `--spritesheet-layout` (`:146-147`).
  - Chaîne d'appel : `ExtractDataFromDatasBin` (`:148`, `:1504-1541`) boucle sur les 483 cartes (`:1523-1530`), puis `SaveMap` (`:1556-1574`), puis `TiledMapExporter.ExportMap` (`:1573`).
  - Cette dernière écrit `<sortie>/data/tiled/map_N.tmj` (`TiledMapExporter.cs:25-41`).
  - La ligne à changer est `TiledMapExporter.cs:275` : `TiledProperty.Int("BalanceLevel", gameMap.Info.BalanceLevel),`.
- **Où il écrit.**
  - Là où l'argument 2 le dit.
  - Le `launchSettings.json` de l'extracteur écrit par défaut **directement** dans `…/Alundra Remake/remaster-data-extracted`.
  - La doc d'usage (`docs/alundra-tiled-map-exporter-usage.md:5-27`) fait pareil, en Debug.
  - Ni l'un ni l'autre ne doit être copié (précédent G0b-R2, `plan-e19-opcodes.md:7898-7907`).
- **Fichiers d'entrée.**
  - Jeu : `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted`, avec des barres obliques.
  - `EntityNames.csv` se résout dans le dossier courant puis dans `AppContext.BaseDirectory` (`AlundraEngine/EntityNames.cs:19-23`).
  - Lancer depuis le scratchpad est donc possible, et c'est préférable : `log.txt` s'écrit dans le dossier courant.
- **Ce qui ne change pas.**
  - Le JSON natif `data/map_N.json` (`Program.cs:1568`, options `IncludeFields` en `:17`) porte `Info.BalanceLevel`, le champ de la décompilation.
  - Exemple : `data-extracted/data/map_0.json:26`.
  - Le compagnon `map_N.alundra.json` ne contient pas la propriété.
  - `GameMapInfo.cs:14`, `:41`, `:121` ; `AlundraEngine/GameEngine.cs:2520` ; `AlundraGameRuntimeMcpServer/RuntimeHelperTools.cs:49`, `:172`.
  - `balance-bin-structure.md:35` et `AlundraEngine/Balance/*` (l'autre `BalanceLevel`, celui des fiches d'ennemis).
- **La doc.**
  - `AlundraTools/docs/datas-bin-structure.md:78` vaut ``| 9 | `byte` | `BalanceLevel` | Balance/anim deletion parameter |``.
  - Cette ligne nomme le **champ de `GameMapInfo`**. Elle ne note **pas** le nom exporté, contrairement à l'esquisse du plan (§1.27) et à ADR-0079.
  - Le fichier est en **ISO-8859-1**. Cinq lignes portent l'octet `0xD7` (« × »), aussi dans le blob git.
  - Un outil d'édition qui réencode en UTF-8 le corromprait.
- **Tests de l'analyseur.** `scripts/validate-tiled-export.ps1:100` n'exige pas la propriété, et aucun test n'existe sur `TiledMapExporter`.

### C. Côté parent
- **Le convertisseur ne nomme pas la propriété.**
  - `TileMapWriter.cs` passe par `TiledMapImporter` du moteur.
  - `CopyCustomProperties` (`CasaEngineMonogame/CasaEngine.EditorServices/Tiled/TiledMapImporter.cs:79`, `:640-646`) recopie toutes les propriétés de carte par leur nom, en ordre d'insertion.
  - Le `.tileMap` les écrit dans cet ordre : `SlideEffectId`, `BalanceLevel`, `C`… (par ex. `alundra-project/Maps/Ancient Shrine/Ancient Shrine-26/tilemap/Ancient Shrine-26.tileMap`, lignes 24-26).
  - Le renommage garde donc la position, et la taille augmente de 3 octets (12 → 15 caractères).
  - Seul `alundra-casaengine-project-converter/Writers/CellMetadataWriter.cs:13` cite le nom, en commentaire.
- **Tests aux noms d'aujourd'hui.**
  - Le plan cite `Alundra.Tests/AlundraBalanceTablesTests.cs:154` ; la ligne est maintenant `Alundra.Tests/Combat/BalanceTablesTests.cs:155`.
  - Classe `BalanceTablesTests`, namespace `Alundra.Tests.Combat`, test `TheThresholdIsThePropertyDOfTheMap_AsAString_ZeroWhenMissing` (renommage R5 : `tools/dll-structure/r5-files.tsv:206`).
  - La ligne est un témoin négatif : `map.CustomProperties["BalanceLevel"] = "2"; // the byte 9 of the header, NOT the threshold.`
  - Aucun test du convertisseur ni du moteur ne cite le nom.
- **DLL.**
  - `Alundra/Combat/BalanceTables.cs:27` : commentaire de `ThresholdPropertyName = "D"` qui cite le `BalanceLevel` exporté.
  - `:114` lit `GetProperty("BalanceLevel")` dans `balance.json` ; il ne change pas.
  - Aucun lecteur de l'octet 9 n'existe dans la DLL ni dans le moteur.
  - `SlideEffectId`, propriété voisine, n'est lue nulle part non plus.
- **Docs.**
  - Vivante : `docs/formats/cells-companion.md:73`.
  - Historiques, non modifiées (D-R-6) :
    - `docs/demarrage-nouvelle-partie.md:67`, classé « histoire, gardée » (`plan-restructuration-dll.md:1175`) ;
    - `docs/plan-conversion-agent-ia.md:194` ;
    - `docs/rapport-analyse-conversion-alundra-casaengine.md:50` (champs du JSON natif, inchangés).
  - Annexes d'oracles figées :
    - `plan-e19-sm-annexe/sm4/tools/mapinfo.py:12` et `plan-e19-sm-annexe/sm4/critic/work/cx_ents.py:18` filtrent les propriétés du `.tileMap` par une liste, donc afficheront une entrée de moins, sans erreur.
    - `plan-e14-annexe/e14-roster/extract_maps.py:23,43` et `plan-e20-annexe/e20-g3/critic-recon/lib392.py:31-32` lisent `Info` du JSON natif, donc ne changent pas.
- **Déjà écrits ailleurs.**
  - ADR-0079 existe (`docs/decisions/README.md:95`).
  - D-E20-60 est au plan `:238-242`, Q-E3b à `:9411`, la ligne E20.eR du tableau à `:613`, l'esquisse à `:7446-7458`.
  - L'ordre voulu est « E20.eR, E20.e0c, puis E20.e » (`:6207`).
- **Outillage de preuve.**
  - `docs/plan-e20-annexe/e20-e0x/manifest.py` et `diff_manifests.py` : manifeste SHA-1, hors `Alundra.dll`, `Alundra.pdb` et `.casaeditor/`.
  - Commande d'export : `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project`.
  - La garde de structure de la DLL est `python -B tools/dll-structure/check_names.py --rules` (`tools/dll-structure/README.md:5`).

### D. Répétition à blanc (scratchpad uniquement)
Méthode, scriptée dans `S/dry-run.sh` :
1. `git archive HEAD` de l'analyseur vers un dossier du scratchpad.
2. Renommage appliqué dans la copie seulement.
3. Construction Release (5 s), puis extraction (73 s, hors du dépôt) vers `S/extract-new/` (4 536 fichiers, 4,0 Go).
4. `diff -rq` contre `data-extracted/` : `S/diff-new-vs-dataextracted.txt`.

Résultats :
- **Écarts à la ré-extraction.**
  - Exactement **483 lignes**, toutes `data/tiled/map_N.tmj` pour N de 0 à 482, aucun « Only in ».
  - Chaque paire redevient identique octet pour octet quand `"name": "TileCutEffectId"` est remis en `"BalanceLevel"` : 483 sur 483.
  - Une seule occurrence par fichier, valeur **2** dans les 483 cartes, +3 octets par fichier, +1 449 au total.
  - L'extracteur est donc déterministe : aucune autre dérive, ni audio, ni texte, ni JSON. Console : `Extracted BGM: rendered=45 silent=1 failed=0 (of 46)`, `pitch guard hits: sequencer=18 sfx=0 exporter=0`.
  - Script : `S/verify_rename.py`. Liste : `S/expected_extract_changes.txt` (483 lignes `M data/tiled/map_N.tmj`, SHA-1 `1ae6a9cba062e96effaa535379276ba305c03aa6`).
- **Prédiction de l'export.**
  - Les 483 `.tmj` du projet sont des copies octet pour octet de `data-extracted/data/tiled/map_N.tmj` (0 écart).
  - Prédiction : 966 fichiers changent (483 `.tmj` et 483 `.tileMap`), +3 octets chacun, soit +2 898 au total, plus `report.json`.
  - Rien n'est ajouté ni retiré, et `AssetInfos.json` ne change pas.
  - Fichiers : `S/predict_export.py` et `S/predicted_export_changes.tsv` (163 103 o, SHA-1 `bc3730b889540fdcd63b5f2e45acddadaa460f88`, avec les SHA-1 avant et après de chaque fichier). La liste des chemins seule a pour SHA-1 `a0fa9280e71d9defc455ef868a921727d4293e99`.

---

