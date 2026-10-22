# Critique indépendante de la préparation E19.sm4 (lecture seule)

Dossier de sortie : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4-critic/`. Appelé `CRIT/` ci-dessous.

Rien n'a été modifié ailleurs. Je n'ai lancé aucun script de la préparation ni de l'annexe : tout ce que j'ai exécuté est écrit de zéro dans `CRIT/`. Le HEAD du dépôt est `2b0c3f8b`. Le code de la DLL est inchangé depuis `36ac0686`, les 5 commits suivants ne touchent que des fichiers `docs/`.

## 0. Verdict global

La préparation est solide. Sur les faits qui portent une décision, je n'ai réfuté qu'un chiffre (« 52 » au §4.1, qui vaut 50). Je ne trouve ni P0 ni P1. Je trouve 2 défauts P2, 5 P3 et 2 P4.

- **Gestionnaires et règles.** Les cinq gestionnaires, leurs tailles, leurs valeurs « rouge aujourd'hui » et les règles R1 à R5 sont confirmés. Seules l'écriture de certaines assertions et la liste de mutants sont à corriger.
- **Corpus.** Les 90 sites, les 50 cartes, les formes d'octets et les comptes de sm0 sont confirmés par un parcours que j'ai porté du C# vers Python.
- **Arcs.** Les arcs ne sont pas mesurables en lecture seule. Je les marque NON ÉTABLI, avec une réserve statique précise pour ARC-306.

Sur la sévérité, les P2 ne bloquent pas l'exécution de la tranche. Les vrais bloqueurs restent ceux que la préparation liste elle-même : Q-E19-SM1, R5, Q-R-7 et sm0.

## 1. Méthode (tout est dans `CRIT/`)

1. **Listing du binaire.** J'ai lu moi-même, avec capstone, les gestionnaires de `0x72 0x88 0x96 0x97 0xB3`, de `0x15 0x66 0x7A`, de `0x42 0x43 0x40 0x2F 0x0D`, la chaîne `IncreaseMp` et la recherche `0x8003C954`. La table `0x80098FAC` m'a donné les adresses. J'ai lu le dispatcheur du binaire en `0x800422B4-0x800422F4`. SHA-1 du binaire : `a705f8da…58`, conforme. Sortie : `results/handlers_listing.txt`.
2. **Interpréteur MIPS écrit de zéro.** `cmips.py` décode les mots à la main, sans capstone ni code de la préparation. Il fait tourner les vrais gestionnaires avec ma propre disposition mémoire, dans `replay_critic.py`.
3. **Contrôles négatifs.** J'ai falsifié une valeur attendue dans une copie des vecteurs : le rejeu rend 2 écarts. J'ai cassé une règle dans le fuzz : 16 écarts. Mes comparaisons peuvent donc échouer.
4. **Modèle des règles en termes DLL.** `dllmodel.py` transcrit `EntitySearchService.cs`, R1 à R5, `SetPlayerMp` et `ClampToShort` d'après le C# lu au HEAD.
5. **Recensement du corpus.** `census_critic.py` est mon propre port du parcours C# de `AlundraStoryChainOpcodeAudit` (`WalkMap`, `WalkProgram`, `ReadRoots`), sur les 483 cartes.
6. **Mutants.** `mutants_critic2.py` rejoue les 25 mutants et en ajoute d'autres, sur les 43 tests chiffrés et les 348 vecteurs.

## 2. Verdict par fait porteur de décision

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| F1 | Les gestionnaires font ce que dit la préparation. | CONFIRMÉ | Mon listing : `0x72` copie `+0x88/+0x8C` vers `+0x26C/+0x270` de a0, sans statut. `0x88` lit le mot de `0x80023D2C` = {2,3,4} et l'écrit en `+0x10` des cibles. `0x96` fait `Hp = min(HpMax signé, Hp+octet1)` avec un `addu` 32 bits. `0x97` appelle `0x8004DF10`. `0xB3` écrit 4 demi-mots en `0x80126F28/2A/2C/2E`. |
| F2 | Tailles 1, 3, 3, 2, 9 et ancrages du tableau R6. | CONFIRMÉ | Valeurs de retour du listing ; lignes `EventOpcodeSizeTable.cs` `:63,148,160,170,184,198,199,227` relues. |
| F3 | `0x97` = ajouter des PM (pas de l'argent). Disposition des stats : `+4` Mp, `+6` MpMax, `+8` argent. | CONFIRMÉ | `0x8004debc` (SetMp) écrit `+4`, plafonne par `+6` ; `0x8004df80` plafonne `+8` à 9999 ; `0x8004df10` = `+4` plus l'octet. Recoupé par `docs/plan-e20-annexe/e20-armes/e0-check.md:114`. |
| F4 | `0x96` : l'octet 1 sert de type et de montant, l'octet 2 n'est jamais lu. | CONFIRMÉ | Listing `0x800409a8` : `lbu s0,1(v0)`, aucune lecture de l'octet 2. La décompilation (`EntityEventHandlers.cs:2859-2876`) est ici d'accord avec le binaire. |
| F5 | `0x88` : mot d'indice 3 = « No.1 » ; indice 4 : Shift-JIS. | CONFIRMÉ | Lecture de `0x80023D2C`. |
| F6 | `0x72` agit sur l'entité logique ; le chemin de reprise de C existe déjà. | CONFIRMÉ | Dispatcheur : a0 = `[owner+0x230]`. Binaire `0x80042108-0x8004214C` et `0x80042170-0x8004218C` ; DLL `EventProgramRunner.cs:200-204` et `:225-230`. |
| F7 | Un opcode sauté par sa taille laisse tout intact, `Result` compris. | CONFIRMÉ | `UnknownOpcode :3065-3091` renvoie `entry.Size` ; la boucle `:424-470` ne fait qu'avancer `CodeIndex`. Aucun des 5 gestionnaires n'écrit à `+0x2C` (rejeu : 0 écriture d'état). |
| F8 | Les 348 vecteurs rejouables (270 « binaire » après exclusion des 22 hors table). | CONFIRMÉ | Mon interpréteur : 348 vecteurs, 0 écart ; rouge/vert par opcode 66/1, 30/31, 45/34, 64/10, 67/0, identique à la préparation (272 et 76). `results/replay_348_vectors.out`. |
| F9 | Les 43 valeurs de tests chiffrés sont celles du binaire. | CONFIRMÉ | `replay_audit.py` : 43 sur 43. U88-9 est une politique, non binaire : le binaire écrit « No.1 » en statut. |
| F10 | Les règles R1 à R5 en termes DLL égalent le binaire. | CONFIRMÉ | Mon modèle DLL : 0 écart sur les 348 vecteurs. Fuzz de mes propres mondes (statuts 0 à 5, indices de pointeurs, 12 types de groupe plus identifiants bruts, propriétaire différent du logique) : 110 000 exécutions, 0 écart. Cela confirme aussi que `EntitySearchService.cs` égale le binaire pour ces usages. |
| F11 | Rattachement octet → membre DLL. | CONFIRMÉ par comportement | Le modèle DLL reproduit toutes les valeurs. Seul `Collidable = 0x80` (`EntityFlags.cs:92`) était à confirmer pour le type 4 : confirmé. |
| F12 | 90 sites, 50 cartes, 42/24/20/3/1, formes d'octets. | CONFIRMÉ | Mon port du parcours donne 483 cartes, 110 140 instructions, 171 opcodes, 510 sites dans 96 cartes. L'ensemble `(carte, créneau, programme, pc, opcode)` des 90 sites est égal au leur. Formes : `96 80 32` ×29, `96 81 32` ×4, `96 81 FF` ×9, `97 32` ×23, `97 0A` ×1, `88 80 00` ×2, `88 80 02` ×1, `72` ×20, `B3 00 10 00…` ×1. |
| F13 | Comptes de sm0 après sm4 : 420 lignes, 418 instructions, 53 cartes, 16 opcodes, 7 cartes qui gardent d'autres sites (29, 54, 159, 160, 199, 421, 442). | CONFIRMÉ | Recalculé sur mon recensement. Par propriétaire : sm1 187, sm2 129, sm3 42, sm5 17, sm7 42, sm8 3. |
| F14 | Tous les `96 80` ont le héros comme entité logique. | CONFIRMÉ | Aucun `0x42` ni `0x43` accessible dans les programmes B concernés. Dans 394 C[3], `0x42` en 540 et 592, aucun `0x43` entre 592 et 749, et `0x42` du binaire (`0x8003E808`) pose `[a1+0x230] = 0x80127D30` (le héros). 475 B[2] est de type explicite `0x81`. |
| F15 | Ordre d'un tick : pavé, boîte, événements de carte, recyclage, bloc d'entités (`MovePlayer` en tête). | CONFIRMÉ | `WorldProxy.cs:2527-2582` et `:2905-2930` relus. Binaire : `UpdatePads` (`0x8002E38C`, appelé en `0x8002BB5C`) précède l'appel de `UpdateWorld` (`0x8002E058`, appelé en `0x8002BC4C`), qui lance `RunMapEvents` (`0x8003C67C`) puis `UpdateEntities` (`0x8003B388`). |
| F16 | `Update` relit `ButtonsHold`, donc le tick suivant calcule ses fronts depuis le mot forcé. | CONFIRMÉ | Binaire `0x8002E250` : appuyé = nouveau & (nouveau ^ ancien) ; relâché = ancien & (nouveau ^ ancien), avec l'ancien lu en `+0x10`. DLL `TickPad.cs:56-98`. |
| F17 | `0x2F` : mode 0 = maintenu, mode 1 = pressé, UB3-6 attendu. | CONFIRMÉ | Exécution du vrai `0x2F` dans mon interpréteur. Les modes 2 et 3 sont relâché et intervalle. `results/check_2f_modes.out`. |
| F18 | Pas de test existant sensible, sauf le témoin `0xB3`. | CONFIRMÉ | Recherche par nom et par suffixe `-ID"` des 50 cartes : seules 140, 411 (`AlundraEventProgramRunnerTests`) et 159 (`BackdropStageDefinitionTests`). 140 B[4] et 411 B[9] n'atteignent aucun site sm4. La 40 est un faux positif. `IntroTraceHarnessTests.ImplementedOpcodes` ne sert qu'à un libellé. |
| F19 | La matrice de retrait suit des définitions de tests. | CONFIRMÉ | Recalculée depuis `tests-catalog.json` : mêmes ensembles (10, 12, 19, 15, 11, 1). Totaux 83 = 65 rouges + 18 verts, répartition par classe exacte. |
| F20 | Les 25 mutants sont tous tués par un test unitaire nommé. | CONFIRMÉ avec réserves | 23 ensembles de tueurs identiques aux miens. M88e n'est tué que par U88-9, test de politique : normal. M97d est défini autrement qu'annoncé (voir C-8). La liste est incomplète (C-2, C-3). |
| F21 | « Arc : 52 en octets réels ou arc ciblé ». | RÉFUTÉ | La somme du tableau est 50 et le fichier `sm4-sites-coverage.tsv` donne 50 plus 40 « forme seule ». 52 + 40 ≠ 90 (C-5). |
| F22 | Zéro site pour `0x15 0x66 0x7A` ; descriptions et vecteurs. | CONFIRMÉ | Mon recensement : 0 site. J'ai relu les trois gestionnaires et rejoué les vecteurs de `0x7A` et `0x15` (8 + 8, 0 écart). Les vecteurs de `0x66` ne sont pas rejoués (format d'état différent). Mon listing concorde avec le texte du §1.8. |
| F23 | Aucune dépendance à E20.g1a. | CONFIRMÉ | Les sites n'utilisent que les types `0x80/0x81`. |
| F24 | Le prochain ADR libre est 0076. | CONFIRMÉ sur cette branche | `docs/decisions` s'arrête à 0075. Le risque de collision entre branches reste (note de mémoire sur 0076/0077) : la consigne « prendre le numéro à l'exécution » est la bonne. |
| F25 | Les arcs : héros qui atterrit, accessibilité, nombre d'exécutions, chemin de 161. | NON ÉTABLI | Voir §6 (C-6 et C-7 pour les deux points statiques). |

## 3. Défauts et corrections

### P2

**C-1. V-MANIFEST dépend des fins de ligne et sera rouge dans un worktree.**
- FAIT : `git config core.autocrlf` vaut `true`. `git ls-files --eol docs/plan-e19-sm-annexe` donne 96 fichiers `i/lf w/crlf` (par exemple `census.json`, `cases.json`) et 117 `w/lf`. Les JSON de la préparation ont 0 CR.
- Un test xUnit qui compare le SHA-256 d'octets bruts avec `MANIFEST.sha256` passe sur les fichiers copiés, puis devient rouge après une extraction normale ou dans un worktree (l'auteur en utilise).
- Précédent de normalisation dans les tests : `SpeakerRawRun.Digest` joint ses lignes par `'\n'`.
- Correction : hacher après normalisation CRLF vers LF, ou déclarer le dossier `-text` dans `.gitattributes`, ou garder la vérification dans un script de T0 et la retirer de la suite xUnit.

**C-2. U96-2 ne prouve pas son nom, et le mutant « référence = propriétaire » de `0x96` survit à tous les tests unitaires.**
- FAIT : dans `named-tests-audit.json`, U96-2 (`RestoreLife_0x96_SaveRoomShape_ReferenceIsTheLogicEntity`) a `logicSlot 0` et `ownerSlot 0`. Aucun des 16 tests U96 n'a un propriétaire différent du logique avec un type dépendant de la référence.
- Mon mutant M96h (la recherche part de `owner`) n'est tué par aucun test unitaire nommé. Il l'est par 15 lignes de vecteurs sur 79, et par RB-394-C3 seulement si ce test monte vraiment le logique (héros) distinct du propriétaire (PNJ). M88f, son pendant pour `0x88`, est bien listé et tué.
- C'est la confusion que E19.a T1 (D-E19-2) a corrigée.
- Correction : U96-2 avec propriétaire = PNJ (slot 1), logique = héros (slot 0), `96 80 32`, héros 1/20 vers 20, PNJ inchangé. Ajouter M96h aux mutants. Vérifier que RB-394-C3 pose bien `owner.LogicEntity = héros` avec un propriétaire distinct.

### P3

**C-3. Le mutant « octet signé » de `0x97` survit aux 12 tests U97.**
- Mon mutant M97f lit l'octet 1 en signé. Aucun test unitaire ne le tue : U97-9 (`97 FF` avec Mp 32767) donne 4 dans les deux cas. Seules 6 lignes de vecteurs sur 74 le tuent.
- Aucun site n'a un octet ≥ 0x80, mais le binaire fait `lbu`.
- Correction : ajouter un test unitaire `97 FF` avec Mp 0 et MpMax 4, attendu 4 (le mutant donne 0).
- Le pendant de `0x96` (M96k) est tué par U96-1 et d'autres.

**C-4. L'extrait R5 ne compile pas tel quel.**
- `v` est un `int[]` et `Overwrite` prend des `uint` : `Overwrite(v[1]|v[2]<<8, …)` donne CS1503.
- Le précédent dans le fichier est `var padFlag = (uint)((v[2] << 8) | v[1]);` (`0x2F`, `:1829`).
- Correction : `(uint)(v[1] | (v[2] << 8))` pour chacun des 4 mots, ou une couture qui prend des `int`.

**C-5. Chiffre de couverture faux (§4.1).**
- « Arc : 52 » doit être 50, et reste 40 « forme seule ». Le tableau et le TSV disent 50 et 40.

**C-6. Recette de l'auteur : mauvaise nature d'entité, et renvoi manquant à Q-sm-8.**
- FAIT (`ents.py`) : les `72` de la carte 40 sont dans les F[1..4] des « Interrupteur poussoir » (sprite 89), pas d'un PNJ. Les E[14] et E[15] de la 306 sont ceux des « Plateforme flottante (petite) » (sprite 56), pas de créatures. L'E[1] de la 442 est celui des « Mur à boule de fer (111) » (sprite 126), pas d'une créature.
- Le texte de recette cherche donc de mauvaises choses en jeu.
- Le §6 ne renvoie pas à Q-sm-8 (élargie le 2026-10-18, atteignabilité de 442 non prouvée). Accès statique par portail : 442 depuis la 440, 306 depuis la 305.
- Correction : reformuler la recette par la nature réelle de l'entité et son effet visible, et citer Q-sm-8.

**C-7. ARC-306 : « une fois par programme » n'est pas établi.**
- FAIT : `Data/sprite-records.json`, sprite 56 : `MoreFlags = 192 (0xC0)`. `EntitySpawnFactory.cs:740` donne `Flags = MoreFlags | CanPickup<<8 | …`. Le sprite porte donc `DeactivateOnAnimationEnd` (0x40) et `Collidable` (0x80).
- La branche Normal de `PickEventTrigger` (`EntityScriptProxy.cs`, branche Normal) redésactive l'entité dès que `ForceResetAnimationFlag` est non nul.
- Si l'animation de la plateforme lève ce drapeau, E se rejoue après le `88` et l'attendu « exactement une exécution » est faux. INCONNU en lecture seule : cela dépend d'un drapeau d'animation que je n'ai pas pu mesurer.
- Correction : asserter « au moins un `0x88` exécuté par programme, puis statut Normal observé après lui », et faire mesurer par T0 le nombre de redéclenchements.

### P4

**C-8. Citations et libellés à corriger.**
- `docs/plan-e20-heros.md:8141, 8165` sont devenues `:8297, :8321` au HEAD : cinq commits de docs ont décalé le fichier de 156 lignes.
- E20.h1 est maintenant READY. Il ajoute `HeroHeals.cs` et `InstantItems.cs` et touche « les cinq entrées de h0a dans `PlayerManager.cs` » (`plan-e20-heros.md`, périmètre de E20.h1). Il faut l'ajouter à E20.h4a dans les risques de conflit de fusion sur `PlayerManager.cs`, et sérialiser.
- Le mutant M97d est décrit « pas de plafond MpMax » mais calculé comme « aucun plafond ni plancher, avec repli 16 bits » (65 lignes sur 74). Le vrai « sans plafond » tue 46 lignes et U97-1, 4, 5, 9, 11, 12.
- « Les 44 tests unitaires » sont 44 des 53 : il en manque 9, non rejouables par valeur (U72-6, U88-3b, U88-10, U96-14, U96-15, U96-16, U97-13, UB3-6, UB3-7).
- La liste fermée du code de test existant (§5.4) emploie les noms d'avant R5. T0 doit les traduire par `r5-files.tsv` et `r5-types.tsv`, comme sm0.
- Q-sm-1 dit « en y dormant » : dans les salles de sauvegarde on marche sur une case.

**C-9. Convention d'absence de l'export.**
- `alundra-project/Maps` n'est pas suivi par git (`git ls-files alundra-project` : 20 fichiers, tous dans `UI`).
- La préparation fait échouer les 12 tests d'octets réels et les arcs quand l'export manque (convention d'`ArcRun`). Le précédent le plus proche, `IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27`, s'ignore silencieusement.
- Ce n'est pas faux, mais à trancher explicitement : la suite devient rouge dans tout worktree sans copie de l'export.

## 4. Verdict par tâche et par décision

| Élément | Verdict | Réserves |
|---|---|---|
| T0 (annexe, rejeux, mesures) | CONFIRMÉ | Corriger C-1 et C-8. Verser `cmips.py` et `replay_critic.py` à l'annexe comme second interpréteur indépendant. Mes 110 000 exécutions fuzz sont un contrôle à reprendre. |
| T1 (couture, tests rouges d'abord) | CONFIRMÉ | C-2, C-3, C-4, C-9. L'ordre « couture sans appelant, puis tests rouges » est cohérent avec le précédent L1-1. |
| T2 (code, libellés, miroir, liste) | CONFIRMÉ | C-4. Ajouter `sm4` à `ClosedOwners` reste un trou de suivi, comme la préparation le dit. |
| T3 (Release neutralisée puis Debug, `cmp`, six traces, mutations réelles) | CONFIRMÉ | Les mutants structurels MS-1 à MS-8 sont « tués par construction » : NON ÉTABLI avant T3. Y exécuter aussi M96h et M97f. |
| T4 (plan, ADR) | CONFIRMÉ | Numéro d'ADR pris à l'exécution. |
| T5 (verifier frais) | CONFIRMÉ | |
| Q-sm-1, défaut A (`IncreaseMp`) | CONFIRMÉ | Binaire et logique du jeu concordent : sur la case de sauvegarde, `96` soigne et `97` rend les PM. |
| Q-sm-2, défaut « octet 1 » | CONFIRMÉ | Les 42 montants sont 128 ou 129, soin complet partout. |
| Q-sm-3, défaut « `Overwrite` seul » | CONFIRMÉ pour la conception | Seul site : hold = 0x1000 et trois mots nuls, donc les enregistrements par tick de la boîte n'y changent rien. |
| Q-sm-11 (laisser sauter `15 66 7A`) | CONFIRMÉ | |
| Q-sm-12 (`0x88` indice ≥ 3 : rien écrit, un avertissement) | CONFIRMÉ | Le binaire écrirait un mot de texte comme statut. |
| Q-sm-13 | CONFIRMÉ | |

## 5. Corrections à appliquer, dans l'ordre

1. C-1 : vérification du manifeste après normalisation LF, ou retirée de xUnit.
2. C-2 : U96-2 avec propriétaire PNJ et logique héros ; vérifier le montage de RB-394-C3 ; ajouter M96h.
3. C-3 : test `97 FF` avec Mp 0 et MpMax 4 ; ajouter M97f.
4. C-4 : casts `(uint)` dans le `case 0xB3`.
5. C-5 : remplacer 52 par 50 au §4.1.
6. C-6 et C-7 : reformuler la recette ; assouplir l'attendu d'ARC-306 ; citer Q-sm-8.
7. C-8 et C-9 : citations, E20.h1 dans les conflits, description de M97d, traduction R5, choix de convention pour l'export absent.

## 6. NON ÉTABLI ou INCONNU (impossible en lecture seule)

- **Arcs en général.** Je n'ai pas pu mesurer : le héros de 452 atterrit-il et `70` devient-il vrai ? Le chemin de 161 de (10,56,1) à (6..13,29,3) est-il praticable ? Le héros placé dans la zone du portail 0 (9..10,56) repart-il vers la 158 ? ARC-442 : le mur disparaît-il dans les deux images annoncées ? Les durées. Toute assertion d'arc reste INFÉRENCE jusqu'à T0.
- **Hero « nu » d'ARC-161a.** `TookPartInFrame` est posé par `EntityScriptProxy.Update` (`:1344`), donc `MovePlayerOnTickPad` est appelé en monde piloté. Je n'ai pas exécuté le montage. `DrivesEntityTicks` est vrai dans `ArcRun` (`WorldWithUIView` pose un `Game`, `AlundraArcSupport.cs:218`) : lecture seule.
- **Mutants structurels MS-1 à MS-8** : par construction seulement.
- **Compilation et exécution du C#.** Je n'ai pas compilé ni exécuté les cas proposés. Mon modèle DLL est une transcription lue du C#, pas son exécution.
- **Vecteurs de `0x66`** : non rejoués, seul le gestionnaire est relu.
- **Atteignabilité en jeu** des cartes (Q-sm-8).

## 7. Fichiers (`CRIT/`)

- **Outils.**
  - `cmips.py` (interpréteur).
  - `replay_critic.py`, `replay_audit.py`, `replay_zero.py`.
  - `dllmodel.py`, `fuzz_critic.py`.
  - `mutants_critic.py`, `mutants_critic2.py`, `check_catalog.py`.
  - `census_critic.py`, `cmp_sites.py`, `logic_scan.py`, `logic_scan2.py`.
  - `dumpprog.py`, `ents.py`, `portals_in.py`, `maps_in_tests.py`, `check_2f.py`, `census_2f.py`.
  - `mydis.py`, `findcalls.py`, `scan_pad.py`.
  - `dllmodel_neg.py`, `fuzz_neg.py`, `negctl/` : contrôles négatifs.
- **Données.** `my-skipped-sites.tsv`, mon recensement des 510 sites.
- **`results/`.**
  - `handlers_listing.txt` : mon listing.
  - `replay_348_vectors.out`, `replay_44_named_tests.out`.
  - `fuzz_60000_seed777.out`, `fuzz_20000_seed424242.out`.
  - `mutants_critic.out`, `sites_vs_prep.out`, `catalog_recompute.out`.
  - `negative_control_replay.out`, `negative_control_fuzz.out`.
  - `logic_scan_linear.out`, `logic_scan_reachable.out`, `portals_in.out`, `replay_zero_site_7A_15.out`, `check_2f_modes.out`.

Re-lancer : `python -B replay_critic.py <SM4>/vectors` ; `python -B fuzz_critic.py 60000 777` ; `python -B census_critic.py my-skipped-sites.tsv` (depuis `CRIT/`).