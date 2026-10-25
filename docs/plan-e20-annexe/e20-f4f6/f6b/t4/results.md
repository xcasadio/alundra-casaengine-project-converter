# E20.f6b, T4 (2026-10-25) : la phase C et les tests de production

Plan : `docs/plan-e20-heros.md` section 1.22.2, T4. HEAD de depart `0d4f9308`, branche `chantier/theme2-attaques-objets`.

## Les tests (rouges avant l'ensemble)

Un fichier neuf, `Alundra.Tests/Ai/Projectiles/BallProductionTests.cs` (dossier miroir de `BallAi`, ADR-0080), et `BallRun` apprend le chemin de production (`production: true` : ni crochet de naissance ni ensemble de test, seul `PlayerManager.ProductionWiredWeaponTypes` decide ; `freeSlotsAtBirth` remplit la table avant le tick de naissance ; `SpawnCalls`, `LowestFreeSlotBeforeBirth`, `BirthSlot`, `PointerAfter`), sur le modele d'`ArrowRun`.

- (p1) 8 cas : feu et glace, normale et chargee, a 1 et 3 ticks par image. Calendrier de l'oracle (R = 96) : naissance a 12 (normale), 107 (chargee, 1 tick par image) et 108 (chargee, 3 ticks par image) ; fille du heros, a la plus basse case libre mesuree avant le tir ; la ligne de la boule (statut, X, Z, pente, sol) egale la cle a chaque tick jusqu'a sa mort, la traînee (effets 17 / 18) et les sons egalent la cle ; `ProductionWiredWeaponTypes.Contains(type)` egale « la boule est nee » dans la meme assertion.
- (p2) 2 cas : `fire-m303`, le monde reel de la carte 303 (`ArcRun`, enregistrement 4, sprite 51, programme D au code 700 = `0x87 [128, 6]`). La boule de feu allume l'interrupteur (le programme D s'execute, `0x87` repond oui, l'opcode `0x05` pose le drapeau, `0x2B` efface la classe A) ; la boule de glace le touche (le programme D s'execute, `0x87` repond non), rien n'est pose, la classe A reste. Le cas negatif exige que le programme D ait tourne : une boule jamais nee ne peut pas le passer.
- (p3) 4 cas : table pleine (aucune case libre : le tir est demande, aucune entite, pointeur efface) puis une seule case libre (une boule nee au tick prevu), pour les quatre types.
- (p4) 1 cas : la fermeture. L'ensemble de production egale les octets de type non nuls des fiches d'arme (sol, air et charge des 12 fiches, derives de `WeaponTables`), et le registre de production traite tous les codes de ces types.

Avant la ligne de l'ensemble : **15 cas, 15 rouges, 0 vert** (`red-before-T4.txt`). Causes : aucune boule ne nait (`Assert.NotNull` echoue en p1, le programme D ne s'execute pas en p2, la case libre ne donne pas de boule en p3), et l'ensemble de production n'a pas les types 11, 12, 14, 15 (p4).
