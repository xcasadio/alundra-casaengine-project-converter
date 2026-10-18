# Contrôle de state.md (tâche A, DLL et tests pour E20.h0, h1, h3, h4)

## Ce qui a été fait

- Lecture complète de `Alundra/Hero/PlayerManager.cs` (2326 lignes), des zones citées de `EntityScriptProxy.cs`, `WorldProxy.cs`, `EventProgramRunner.cs`, `NativeSpriteRegistry.cs`, `InventoryDirector.cs`, `ScriptedMotion.cs`, `CameraSway.cs`, `CameraMath.cs`, `NativeEffects.cs`, `TestSaves.cs`, `GameState.cs`, `SaveGame.cs`, et des bancs de `Alundra.Tests` (`AlundraArcSupport.cs`, `AlundraHitArena.cs`, `AlundraContactTestSupport.cs`, `AlundraRunSupport.cs`, `AlundraJumpTestSupport.cs`).
- Lecture des cinq sources à traduire : `e20-h4/report.md` (§8 et le reste), `e20-h4/check.md`, `e20-g1a/report.md` (§1.2 à §1.9, §3, §4, §6, §7), `e20-themes/attacks.md`, `e20-armes/h-g-deps.md` (§2), `e20-0-rapports/D4-D5-equipement-0x8003634C.md`, `decouverte-2026-10-09/` (grep ciblé), plus les sections 1.24 (bloc HEAD), 1.25, 1.27 et 2 du plan et le tableau de découpage (l. 690-740).
- Données exportées lues (lecture seule) : `alundra-project/Data/sprite-records.json` (en-têtes `alundra_0`, 1, 2, 16 à 24, 27 à 29, 154), `Data/items-properties.json`, `Data/effects/effects-global.json`, `AssetInfos.json` (préfabs), `Entities/Alundra/bankalundra_0_anim50|51|52_down.anim2d`, `Data/Alundra.buttonsMapping`.
- Oracles Alundra.Tests : script Python (`python3 -B`, lecture seule) sur les JSON de `docs/plan-e20-annexe/**` pour trouver les traces dont le mot de maintien porte le bit 0x20 : 26 fichiers, tous sous `e20-d/oracle/out*` (aucun dans les oracles de coups, de charge, d'armes, de vague).
- `git diff --stat a3ccb75 HEAD -- Alundra Alundra.Tests` : vide.
- `verify_cites.py` (dans ce dossier) : 273 citations fichier:ligne de `state.md` vérifiées contre le dépôt par une expression régulière sur le texte des lignes citées : **273 / 273 conformes** à la dernière exécution. Premier passage : 8 écarts (lignes du Rond, de `CameraSway`, de `NativeEffects`, de `TestSaves`, de `Arc` dans `AlundraWeaponSpawnSeamTests`, de l'appel de `MovePlayer` dans `EntityScriptProxy`), corrigés avant la version finale ; une affirmation fausse corrigée aussi (« `RegenerationCounters` a trois remises à zéro » : il n'en a aucune, `GameState.cs:120-123`).
- Rejouer : `python -B verify_cites.py` depuis ce dossier.

## Ce qui n'a pas été fait

- Le binaire ALUN_CD.EXE et la décompilation n'ont pas été relus : toute adresse `0x8…` de `state.md` vient des rapports (étiquette FAIT-RAPPORT) ; seuls les faits de la DLL, des tests et des données exportées sont des FAIT.
- Aucun build, aucun test, aucun export ; aucun harnais Python de l'annexe h4 n'a été lancé (le harnais nommant le chemin du binaire n'était pas nécessaire pour la tâche A).
- Les corps de C62, D30, E12, C73, E21 ne sont pas lus : l'effet exact de h3 sur `AlundraLogicalZWorldTests.cs:292,398` reste une INFÉRENCE.
- Non vérifiés : placement du sprite 154 dans les cartes ; durées et chaînes des animations 0x32 à 0x34 (seuls les types Once/Loop/Once et les fiches de vitesse ont été relus) ; adresse par défaut de la table d'états principale `0x80022D3C`.

## Limites de méthode

- Les numéros de ligne sont ceux du HEAD `66d86ebc` ; un commit de code entre-temps les déplacerait.
- La classification de chaque test est un raisonnement d'atteignabilité fait à la lecture (gardes, hôtes, registre), pas une exécution : « casse » signifie que l'assertion citée est fausse dès que la clé est enregistrée ; « à revoir » est une INFÉRENCE.
