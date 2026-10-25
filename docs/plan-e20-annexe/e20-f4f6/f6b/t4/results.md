# E20.f6b, T4 (2026-10-25) : la phase C et les tests de production

Plan : `docs/plan-e20-heros.md` section 1.22.2, T4. HEAD de depart `0d4f9308`, branche `chantier/theme2-attaques-objets`.

## Les tests (rouges avant l'ensemble)

Un fichier neuf, `Alundra.Tests/Ai/Projectiles/BallProductionTests.cs` (dossier miroir de `BallAi`, ADR-0080), et `BallRun` apprend le chemin de production (`production: true` : ni crochet de naissance ni ensemble de test, seul `PlayerManager.ProductionWiredWeaponTypes` decide ; `freeSlotsAtBirth` remplit la table avant le tick de naissance ; `SpawnCalls`, `LowestFreeSlotBeforeBirth`, `BirthSlot`, `PointerAfter`), sur le modele d'`ArrowRun`.

- (p1) 8 cas : feu et glace, normale et chargee, a 1 et 3 ticks par image. Calendrier de l'oracle (R = 96) : naissance a 12 (normale), 107 (chargee, 1 tick par image) et 108 (chargee, 3 ticks par image) ; fille du heros, a la plus basse case libre mesuree avant le tir ; la ligne de la boule (statut, X, Z, pente, sol) egale la cle a chaque tick jusqu'a sa mort, la traînee (effets 17 / 18) et les sons egalent la cle ; `ProductionWiredWeaponTypes.Contains(type)` egale « la boule est nee » dans la meme assertion.
- (p2) 2 cas : `fire-m303`, le monde reel de la carte 303 (`ArcRun`, enregistrement 4, sprite 51, programme D au code 700 = `0x87 [128, 6]`). La boule de feu allume l'interrupteur (le programme D s'execute, `0x87` repond oui, l'opcode `0x05` pose le drapeau, `0x2B` efface la classe A) ; la boule de glace le touche (le programme D s'execute, `0x87` repond non), rien n'est pose, la classe A reste. Le cas negatif exige que le programme D ait tourne : une boule jamais nee ne peut pas le passer.
- (p3) 4 cas : table pleine (aucune case libre : le tir est demande, aucune entite, pointeur efface) puis une seule case libre (une boule nee au tick prevu), pour les quatre types.
- (p4) 1 cas : la fermeture. L'ensemble de production egale les octets de type non nuls des fiches d'arme (sol, air et charge des 12 fiches, derives de `WeaponTables`), et le registre de production traite tous les codes de ces types.

Avant la ligne de l'ensemble : **15 cas, 15 rouges, 0 vert** (`red-before-T4.txt`). Causes : aucune boule ne nait (`Assert.NotNull` echoue en p1, le programme D ne s'execute pas en p2, la case libre ne donne pas de boule en p3), et l'ensemble de production n'a pas les types 11, 12, 14, 15 (p4).

## La ligne de l'ensemble (phase C)

Un seul changement de code : `PlayerManager.ProductionWiredWeaponTypes` passe de `{ 3, 4, 7, 27, 28, 29 }` a `{ 3, 4, 7, 11, 12, 14, 15, 27, 28, 29 }` (et les trois commentaires qui disaient « E20.f6 les ajoutera »). Les types 10 et 13 n'ont pas de fiche d'arme. Seul changement de comportement : les tirs des baguettes de feu et de glace.

## Vert apres

- `BallProductionTests` : **15 cas verts sur 15** (`green-after-T4.txt`), contre 15 rouges avant. Mesures lues : la boule normale nait a 12 (1 et 3 ticks par image), la chargee a 107 (1) et 108 (3), feu et glace ; la ligne de la boule, la traînee et les sons egalent la cle sur toute la vie de la boule ; l'interrupteur de feu du 303 : le programme D (`0x87`, `0x04`, `0x05`, `0x2B`, `0xFF`) s'execute a la meme trame pour les deux baguettes, mais seule la boule de feu pose le drapeau et efface la classe A (la glace : `0x87`, `0x04`, `0xFF`, classe A gardee).
- **Tests existants deplaces : 1**, exactement la liste predite de T0 pour la phase C (les quatre epingles du registre ont deja ete reecrites en phase B) : `Hero.WeaponSpawnSeamTests.S5_TheProductionSetPassesTheInvariant_...` (l'egalite de l'ensemble de production, deja derivee des classes : `+ BallAi.SpriteTypes` des types 11, 12, 14, 15). Mesure : l'ancien S5 devant le nouveau code est rouge (`removal-B-oldS5.txt`, 1 rouge sur 7), et les 6 921 tests de la suite sont verts avec le S5 reecrit (aucun autre test ne bouge).
- Suites, Release (copie neutralisee) puis Debug (derniere, elle depose la DLL), au premier plan, `--blame-hang-timeout 60s`, partitions disjointes de `suite-wired/runpart.sh` (en Debug la premiere partition est coupee en deux, Arcs / Audio / Cameras / Cells / Combat / Dialogues puis Ai) : Release 1 870 + 3 085 + 1 966 = **6 921 tests, zero echec** ; Debug 967 + 903 + 3 085 + 1 966 = **6 921, zero echec** (6 906 a la fin de T3, +15) (`suite-release/`, `suite-debug/`). `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll` : identique. `check_names.py --rules` : 0 finding, 1 avertissement preexistant. Les fins de ligne des quatre traces `docs/hero-trace-389-*.txt` : reecrites par les suites, `diff --ignore-cr-at-eol` vide, restaurees (jamais indexees).
- **Preuves de retrait** (mutations reelles appliquees par script puis annulees, `git diff` identique apres ; Release, copie neutralisee ; sous-ensemble : `Ai.Projectiles.*`, `WeaponSpawnSeamTests`, `NativeSpriteRegistryTests`, `LiftableAiRegistrationTests`, `MarkedEntityGroundSlopeTests`) :
  - retirer l'ensemble `{11, 12, 14, 15}` (`removal-A-set.txt`) : 390 tests, **16 rouges** : exactement (p1) x8, (p2) x2, (p3) x4, (p4), et `S5` (derive des classes, rouge en plus comme dans f4 et f5) ; rien d'autre ;
  - retirer l'appel de f6a (la branche des entites marquees de `UpdateGroundSlope()` dans `TickMotion`, `removal-C-f6a-call.txt`) : 856 tests, 86 rouges, dont les (s3) (`S3a`, `S3b`) de `MarkedEntityGroundSlopeTests` . Les rouges se repartissent en `BallLifeTests` 35, `PitFallWorldTests` 22, `MarkedEntityGroundSlopeTests` 20, `C4WorldTests` 6, `LiftableBreakWorldTests` 3 : la mutation retire l'alimentation de la pente de toutes les entites marquees, pas seulement celle des boules ; le plan ne demande que (s3) rouge.
- Moteur (`CasaEngineMonogame/`), convertisseur, donnees (`data-extracted/`, `alundra-project/` hors la DLL deposee par la construction Debug) : aucun diff de ma part (`git status` identique au depart).

## Ecarts au plan

1. **La preuve de retrait de l'ensemble** : S5 est rouge en plus de (p1) a (p4), comme l'ecrivaient deja f4 et f5 (il est derive des classes) ; le plan ecrit « exactement (p1) a (p4) ».
2. **La preuve de retrait de f6a** : plus large que (s3) (voir plus haut), parce que la mutation retire l'alimentation de la pente de toutes les entites marquees.
3. **(p1) a 3 ticks par image pour la boule normale** : naissance a 12 (le relâchement se lit a la frontiere d'image, le tick 12 termine la trame 10-12) ; le plan ne donne que 107 / 108 pour la chargee.
4. **(p2)** ne lit pas la valeur de `Result` apres `0x87` (l'opcode `0x04` qui suit la remplace) : « `0x87` repond oui » est lu par ce qui suit (`0x05` puis `0x2B` s'executent pour le feu, pas pour la glace).
