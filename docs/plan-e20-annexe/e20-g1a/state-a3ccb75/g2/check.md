# Vérification du relevé g2 (g2a lancer, g2b briser) au HEAD `a3ccb759`

Tout a été fait en lecture seule. Outils utilisés : `git`, `rg`, `python -B`. J'ai lancé deux scripts personnels, `scratchpad/g1-state/g2v/lift.py` et `names.py`, qui lisent `sprite-records.json` et appellent `scan_types.external_types()` puis `check_names.collision_findings`. J'ai aussi lancé `check_names.py --table` et `--tree --stage final`. Aucun projet .NET n'a été lancé. `git status` est inchangé (` m CasaEngineMonogame`, ` m alundra-datas-analyser`).

`check.md` n'a pas été écrit : la consigne de l'environnement interdit à un sous-agent d'écrire un fichier rapport, comme pour les critiques g3. Ce message est le rapport.

## Erreurs

1. **§0.4 : « Une caisse par enregistrement n'a jamais `JoinsBirthTick` » est faux.**
   - Les opcodes 0x2D, 0x8A et 0x8B posent le marqueur sur une entité née d'un enregistrement : `EventProgramRunner.cs:2367`, `:2378`, `:2406`, `:2445`. Le commentaire de `ScriptedMotion.cs:173-174` le dit aussi.
   - Les 73 soulevables qui ne naissent qu'au script (§1.28 ; critic-complet, Réfuté 6) l'ont donc. Le test `AlundraRecordBirthTickTests.cs:364` (B6_R), cité par le relevé lui-même, épingle ce cas.
   - Correction : la garde actuelle ne couvre pas une caisse lancée à cause de sa **seconde** condition, `CurrentAnimationId == ~0u` (`ScriptedMotion.cs:175`). Cette condition est fausse dès la première validation d'animation. Ce n'est pas l'absence du marqueur.
   - Même défaut au §2.3, ligne e0a5/e0a4 : « pas de caisse de carte concernée (73 soulevables ne naissent qu'au script) ». e0a4 s'applique bien à ces 73 : `SwitchedAtCreation` en `WorldProxy.cs:3425` et `JoinsBirthTick` posé par les opcodes.

2. **§2.2, §3 (TR2) et §4.3 : la sortie de la liste d'une caisse brisée ne dépend pas du filtre `FlagToDestroy`.**
   - C23 efface `0x80` (Collidable, `EntityFlags.cs:92`) au lancer : `0x3323E4` devient `0x332374`. E2 ne le rétablit jamais (§1.28, « La libération »).
   - Une caisse lancée puis brisée est donc hors de `BuildCollidables` dès le lancer, par `IsEligibleSubject` (`EntitySupport.cs:73-76`).
   - Le filtre de `EntitySupport.cs:55` est l'écart d'un tick O-G3-1 : la prémisse « comme le binaire » est réfutée (critic-recon Réfuté 1, critic-complet Réfuté 5). En faire un critère d'acceptation de g2b reviendrait à épingler un écart connu.
   - Le test `AlundraEntityDestructionTests.cs:30` ne relève donc pas de g2b.

3. **§2.4 (Q-G1A-1) et §4.2 : « h3 avant g3, donc graines et bombe portées plus tôt » contredit l'enveloppe.**
   - La ligne 11 (`docs/plan-e20-heros.md:722`) exige `E20.g1, E20.g2` en entier, donc g1b. Or g1b vient après g3 (§1.28).
   - La règle « h3 n'exige que g1a et g2a » n'est qu'une proposition : D-G-1 (`:6345`) et les corrections induites (`:6599-6601`, « Proposées, non appliquées … l'enveloppe READY (révision 9) fait foi »).
   - En l'état, h3 passe après g3.

4. **§4.1 : les lignes de §1.24 sont mal caractérisées.**
   - Seule la ligne `:6368` inverse l'arête (« E20.g3 … exige g1b et g2b »).
   - Les lignes `:6296`, `:6299` et `:6534` ne disent pas « g1b avant g3 ». Elles omettent g3 comme prérequis de g1b, et `:6299` et `:6534` gardent l'alternative e0b2, désormais sans objet.
   - Le relevé ne cite pas l'enveloppe, ligne 12 (`:723`, prérequis « E20.g1, E20.g2 »), que critic-complet (conséquence 1) demande aussi d'inverser.

5. **§4.7 : la garde des fichiers neufs (Q-R-7) ne s'applique pas à g2b.**
   - `LiftableAi.cs` est créé par g1a : le périmètre de g1a (`:6389`) dit « nouveau `Alundra/Scripts/AlundraLiftableAi.cs` (C23) ». g2b ne fait que le modifier (`:6496`).
   - `check_names --tree` ne lit que `Alundra/` (`scan_types.py:150-164`) : un fichier de test neuf n'y déclenche pas `[hors table]`.

6. **§1.1 : trois numéros de ligne faux.**
   - **`TryUseItem`** : la définition est en `PlayerManager.cs:932` (doc `:930-931`). Le seam est le champ `:937` (`[ThreadStatic]` en `:936`, doc en `:934-935`), pas `:935-936`.
   - **Ordre du tick** : `:3000` est la définition de `RunCountersPass`, l'appel est en `WorldProxy.cs:2960`. `RunEntityBlock` va de `:2905` à `:2987`. La liste du relevé omet aussi le passage « pending » (`:2943-2957`), `BuildHitList` (`:2968`) et les effets actifs (`:2982`).
   - **Fiches de vitesse** : l'ancien `ScriptedMotion.cs:614-632` correspond au HEAD à `:620-638` (de `var acceleration` à `ForceStepY`, décalage de +6). Le relevé donne `:612-636`, qui commence dans la doc.

7. **§2.1 : « États 0x05 à 0x0C … sans constante ni cas » est inexact pour 0x08.**
   - 0x08 (rebond du fléau) est traité par `IsGroundAttackGroup` (`PlayerManager.cs:1026-1027`).
   - Les états de portage sans cas sont 0x05, 0x06, 0x07, 0x0B, 0x0C et 0x2E à 0x30. 0x09 et 0x0A ne sont dans un groupe que nominalement.
   - Ajout (FAIT) : aucun octet d'animation des fiches d'armes ne vaut 9 ni 0x0A (`WeaponTables.cs:55-69`, positions 1, 5 et 9).

8. **§2.1, Données : deux listes incomplètes (`lift.py`).**
   - **Contents** : 130 pour les banques 34, 35, 36 et 37 ; **248 pour 38** ; 247 pour 41 ; 0 pour 48 et 182 ; 255 pour `alundra_234`.
   - **Drapeaux de `0x3323E4`** : la liste omet `0x04` (HitsClassA, `EntityFlags.cs:53`), qui porte la touche d'une cible de classe A visée par (b3) et (v10). Elle omet aussi `0x200` (genre 1) et `0x30000` (ombre). La jarre `0x2323E4` n'a pas `0x100000`.

9. **§2.1 et Inconnu 6 : la fin de l'animation 2 de la caisse par Hold se lit dans l'export.**
   - `IdsvAnimDirs` de l'animation 2 vaut `End = "Hold"` dans les quatre directions, pour les 7 en-têtes C23 (sortie de `lift.py`).
   - Ce qui reste inconnu, c'est le **tick**. `+0xAC = 1` est posé par l'événement de fin d'animation du moteur (`EntitySpawnFactory.cs:308-325`), pas par l'horloge du tick. La chronologie T+23 à T+25 de (b2) n'est donc pas acquise à 2 ou 3 ticks par image (INFÉRENCE).

10. **§2.2, ligne 1 : « 7 → 8 → 9 » de `report.md` §6 n'est pas faux.**
    - À `2010ec3`, le registre avait déjà 7 clés (f3c était faite).
    - Le relevé §6 n'était qu'incomplet : une seule épingle citée, e0c non connu. Seul le « 6 » du plan est faux.

11. **§2.2, ligne 6 : « D-G-14, ligne 6299 » est mal référencé.**
    - D-G-14 est en `:6358`.
    - Les clauses e0b2 désormais sans objet sont en `:6295`, `:6299`, `:6358`, `:6492`, `:6534` et `:6601`.

12. **§2.1 : « les dix sites de D-G-15 sont exacts » est non démontré.**
    - D-G-15 nomme `CarriedEntity` **et** `PlatformEntity`. Les deux réunis donnent 13 lignes de test, identiques à `9ecc7d5d` et au HEAD (`git grep`).
    - On trouve 10 en ne comptant que `PlatformEntity`. Le compte voulu par D-G-15 est INCONNU. Sur le fond, rien n'a changé depuis.

13. **§2.2, ligne 3 : verdict discutable.**
    - « E2 le lit déjà » (`:6358`) peut se lire comme le E2 prévu par g2b, et non comme du code existant.
    - Le fond du relevé est juste : l'AND n'est nourri que pour le héros (`EntityScriptProxy.cs:1621`, `:2044-2072`).

14. **« Les tests gardent leur dossier plat » est approximatif.**
    - 307 fichiers sont à la racine, dont 24 sans préfixe `Alundra`.
    - 26 fichiers sont dans des sous-dossiers : `UI/` (20) et `Scripts/` (6) (`git ls-files`).

15. **Q-G1A-5 :** le plan la rattache au T0 de g1a (`:7258`), pas à g1b.

## Omissions

- **Citations absentes des tables, alors que le relevé dit qu'elles y sont toutes :**
  - `report.md` §4, `AlundraEntityDepthKey.cs` : au HEAD `Alundra/Entities/EntityDepthKey.cs:11` (« no raise by … a carrier »).
  - `report.md` §6, `AlundraWarpDepartureTests`.
  - Hors de la portée annoncée, `check.md` §3, `HitPass.cs:21` : au HEAD `EntityScriptProxy.HitPass.cs:23-24` (`BalanceRecord` nul dans les « degraded cases »).
- **« 12 écritures dans les tests » (`check.md` §2) non rapproché.**
  - Le compte est encore 12 au HEAD : 9 écritures de `PlatformEntity` et `CarriedEntity`, plus `RelativeWarpOffsetX` en `AlundraWorldProxyEventPassTests.cs:437`, `:553` et `:557`.
  - Ces trois dernières sont des sentinelles relues en `:575`. INFÉRENCE : elles restent vertes.
- **Le type 35 n'est pas rapproché.**
  - `check.md` §1.5 demande d'ajouter le type brut 35 (carte 231) à (C,23) et (E,2).
  - critic-complet, Réfuté 4, dit que l'export y voit « I05_Arc du chasseur », genre 0. Les deux critiques divergent aussi sur l'écart 2 252 / 2 251 (la bombe contre la carte 231, rec3).
  - Cela touche l'ensemble des types de (p2) de g2b.
- **Texte de D-F5-5 (`:5952`) :** la règle vise « une entité née par l'interface ». Même après f5, une caisse marquée par `PutInHands` n'est couverte que si la règle lit `HasLogicalZ`. Cela renforce Q-G1A-4 et (v7).
- **Accès privés (INFÉRENCE) :**
  - `SpawnDirections` est privé (`PlayerManager.cs:1282`), tout comme `NoAttackVramFlag` (`:943`) et `NoJumpVramFlag` (`:110`).
  - Placer `ThrowCarried` dans `CarryRules` demande d'en changer l'accès ou de dupliquer la table. Le périmètre de g2a (`:6456`) parle de `SpawnDirections` « partagée avec f3a ».
- **Commentaires périmés manquants dans la liste :**
  - `WorldProxy.cs:3315` : le bouchon (6) lui-même.
  - `PlayerManager.cs:405-406`.
  - `ScriptedMotion.cs:611-612` : « entities born from a record keep their unbounded step », faux pour une caisse marquée.
  - `EntityScriptProxy.cs:1379` : « never by the creations from a record ».
  - `AlundraLogicalZStepTests.cs:21` : « posed by the interface and by it alone ».
  - `IntroTraceHarnessTests.cs:913`.
  - Docs du registre `NativeSpriteRegistry.cs:29` et `:35`, et `EventProgramRunner.cs:47-50` : ce dernier est déjà périmé, sans C61.
- **Épingles témoins non listées.** Elles posent « enregistrement sans marqueur au chargement » : `AlundraLogicalZStepTests.cs:116` et `AlundraMapEdgeWorldTests.cs:226`. INFÉRENCE : vertes, faute d'appel à `PutInHands`.
- **O-G-7 manque à la liste des O-G.** Il est accepté et documenté (`:6594`).
- **O-G-10 :** sa cause est expliquée par le test de zone du spawner (`report.md:249`, confirmé par `check.md` §1).
- **critic-complet, Réfuté 1 :** le roulis de la 392 (B1, bit `0x04` du masque `0x34`) passe par le mode 1 du portage. g4b, qui exige g2a et g2b, doit le mesurer (`:7224`).
- **Précédent de nommage plus proche :** e0c prévoit `Alundra/Ai/Objects/PickupAi.cs` (`:6772`). Le nom est libre aussi (`names.py`).

## Confirmé (comptes)

| Partie du relevé | Justes | Détail |
|---|---|---|
| Correspondance des fichiers | 13/13 | conforme à `table.json`, champ `final` |
| §1.1, citations DLL | 31/34 | les 3 autres : erreur 6 ; imprécisions négligeables : `MovementObstacleProbe` `:50-51`, `EntityPickupKind.cs` de 25 lignes |
| §1.2, citations de tests | 12/12 | — |
| §2.2, verdicts | 6/8 | une ligne discutable (erreur 13), une incomplète (commentaires) |
| §3, épingles | 15/16 | TR2 a la mauvaise raison (erreur 2) |
| §2.3, commits | 8/8 | identifiants justes ; manquent les commits de test `0d6bd623` et `48cf43ba` ; l'effet e0a4/e0a5 est faux (erreur 1) |
| §2.1, données | — | 17 en-têtes ; C23 sur 7 ; E2 sur 10 ; BreakEffect 10 / 2 / 11 ; vitesses 256, 128, 384 et impulsion 1 024 ; animation 2 à vitesse 0 ; héros 9 (son 26) et 10 (accélération 15) |

Autres points vérifiés et justes :
- 7 clés de registre et deux épingles du compte (`AlundraNativeSpriteRegistryTests.cs:55`, `AlundraWaveSwordLifeTests.cs:181-183`). Ni (C,23) ni (E,2) n'est utilisé par un test.
- Doublon de clé qui lève (`NativeSpriteRegistry.cs:62`). E0, E1 et E10 traités en ligne (`EventProgramRunner.cs:284-290`, `:328`).
- Statuts : e0k2 et e0b2 faits (✅), f4, f5 et f6a ⏳. `NativeSpriteTypeFilterForTests`, `PutInHands`, `ThrowCarried`, `LiftableAi` et `CarryRules` sont absents.
- Noms `LiftableAi` et `CarryRules` libres (`names.py` : 0 finding, absents de la DLL). `--table` et `--tree --stage final` : 0 finding, 1 avertissement (`TextBoxGpuHarness.cs:265`). `[hors table]` par le code (`check_names.py:96-98`).
- ADR libre suivant : 0076. Q-R-7 encore à l'état de proposition (`plan-restructuration-dll.md:1234`).
- Conséquences de §1.28 justes : ordre g1a, g2a, g2b, g3, g1b ; D-G-7 et O-G-1 caducs ; le test (s5) consomme g2a et g2b ; g4b exige g2a et g2b ; recommandation d'une seule fusion ; le contact `XCollisionEntity` hors g2.

## Inconnus

1. Aucun test lancé : tout comportement de la DLL est lu dans le code.
2. Le tick où `+0xAC` passe à 1 pour une caisse en animation 2, à 1, 2 et 3 ticks par image (erreur 9).
3. Le compte voulu par les « dix sites » de D-G-15.
4. Le type 35 de la carte 231, rec3 : cruche ou arc au moment du spawn dans le binaire. Les critiques divergent.
5. « Comme le binaire » pour `EntitySearchService` de type 11 (`:249`) : non vérifié au binaire.
6. Je reprends les inconnus 2, 3, 4, 5, 7, 8, 9 et 10 du relevé. Ils restent fondés, y compris le 8 : la carte 14 n'a aucun soulevable (`e20-g3/dll/report.md`, B3.3).