# Critique frais de la découverte E20.i2 (lecture seule, 2026-10-25)

Je n'ai rien écrit dans le dépôt et n'ai lancé aucun `dotnet`. Pour le code de la DLL, j'ai lu le HEAD `5e67722d` avec `git show` et `git grep`. Les deux commits postérieurs à `8174c0cb` ne touchent que `Alundra.Tests/`, donc `Alundra/` est inchangé, comme le dit la découverte. Pour le binaire, j'ai redésassemblé moi-même les adresses porteuses (`bx.py` copié) et rejoué les sondes de la découverte depuis une copie du harnais `e20-d`.

## 0. Verdict global

Aucun fait porteur n'est réfuté sur le binaire. Le découpage proposé (mur, forces, courants) tient.

Les corrections portent sur :
- des chiffres de recensement (cascade de la 416, entités posées sur courants) ;
- le libellé d'une question à l'auteur (Q-I2-4) ;
- quelques références de fichiers ;
- une contradiction interne dans la description d'i2b.

## 1. Faits porteurs : verdicts

### Binaire

| Fait | Verdict | Preuve que j'ai refaite |
|---|---|---|
| A1 : ordre de la passe `0x8003836C` (remises à zéro, `0x800364C8`, `0x80036828`, `0x80037364`, `0x80037E34`, `0x80038064`) ; `+0x188` lu avec un tick de retard | CONFIRMÉ | Désassemblage `0x8003836C-0x800384D8`. La boucle principale appelle `0x800386D0` (qui appelle MovePlayer en `0x800386EC`) avant `0x80038364` (appel en `0x8003B3E0`). |
| A2 : le héros prend `0x80036884`, puis `0x80036B9C` ; un non-héros avec `+0x28 ≠ 0` a ses forces à 0 et saute `0x800366FC` sans toucher à la poussée ; un seul appelant de `0x800366FC` | CONFIRMÉ | `0x80036A84-0x80036AB0` saute à `0x80036BBC`. Seul `jal` vers `0x800366FC` : `0x80036B9C`. Précision : `+0x28 ≠ 0` veut dire « est porté par un autre » (`PlatformEntity` dans la DLL), pas « porteur ». |
| A3 : `adj = ((F + tab[idx]) >> (sh & 31)) + push`, `idx = (+0x188) & 0xF`, poussée non décalée, lue puis remise à zéro, serrage strict, drapeau `+0x13C` | CONFIRMÉ | `0x800366FC-0x800367E0`. Tables relues : indices 1, 2, 4, 8 donnent Y −40960, Y +40960, X −61440, X +61440 ; X ∓983040 aux indices 7 et 11. |
| A4 : `SlideEffectId` n'est lu qu'en `0x80036744` | CONFIRMÉ | Pour chacun des 22 sites `0x4334(rX)`, j'ai suivi les chargements issus du pointeur : un seul `lb 8(…)`, les autres lisent +4, +6, +9 à +14, ou passent le pointeur à une fonction qui ne lit pas +8 (le seul cas non suivi, `0x8002C754`, passe `hdr+0x10`, donc hors de portée de +8). Écart de comptage : 21 lectures et 1 écriture (`0x8002CC80`), pas « 22 lecteurs ». |
| A5 : quatre écritures de `+0x188`, toutes dans `0x80038064`, deux lecteurs | CONFIRMÉ | Mon balayage de toute instruction à immédiat `0x188` ajoute un cas absent de la découverte : `0x8002B340` est un `addiu` d'adresse de chaîne (`lui 0x8002`), pas un champ. Les trois sites `0x8009xxxx` sont des `sh/lhu` de structures de menu, et `0x8007F540` a pour base `a1+0x68`, c'est-à-dire entité+0x1F0. Aucune table de données ne contient 0x188. |
| A6 : décodage de la sonde (bits `0x04`/`0x08`, `0xC00000`, `1 << ((w>>20)&3)`, `|0x80`, chemin sans gravité) | CONFIRMÉ | `0x80038064-0x80038340`. La case est lue au point d'origine : colonne `T[PosX>>16]` bornée 0 à `0x33`, rangée `PosY>>20` bornée 0 à `0x3B`. Niveau : `(w & 0xFF000000)>>4 + 1 == +0x1E0`. Sans gravité, il faut `0xC00000` ET `0x80000`. |
| A7 : branche verrouillée `0x80032768` | CONFIRMÉ | Ordre : bloc de blessure (`0x80031C30-0x80031C9C`), test des PV (`+0x14`), `+0x188 & 0x80` (`0x80031CB0`), pilote d'objet (`0x80031CC4`). Contenu : `[0x80126FFC]=0`, `0x800350C0` avec `[0x80127110]=0` dans le delay slot, `0x8002F884(1)`, puis `0x1D` si `+0x18C==4`, `Idle` (`0x80031E7C` = `sw 0,0x88` puis fin) au sol, `0x2D` en l'air. Fin de tick en `0x80032914`. |
| A8 : mur d'eau profonde propre au héros, test de coins `0x80037488` | CONFIRMÉ | Le test du héros a l'adresse de la structure héros en dur (`0x80127D30`) ; `0x80037764` choisit entre `0x80037488` et `0x800373E4` (le test des autres, que j'ai lu en entier : masque et hauteur seulement, aucun test d'eau). Aucun `jal` vers l'un ou l'autre. Masque `0x40`, `0x41` avec le drapeau 8, `|0x1000` avec le drapeau 1 ; `[0x80127000] < 2` et `(demi-mot & 0xE00) == 0x800` bloque sans condition de hauteur. |
| A8 : cause du court-circuit `[0x800DC058]` / `[0x800DC05C]` | NON VÉRIFIABLE | Le fait (retour 0 si `[0x800DC058] < 0` et bit 31 de `[0x800DC05C]`) est confirmé ; sa cause (transition d'écran) reste une inférence. Elle ne porte aucune décision. |
| Écritures de `+0x188` par déplacement calculé (VM) | NON VÉRIFIABLE | Même INCONNU que le critique de h4b pour `+0xCC/+0xD0`. Mon balayage des données ne trouve aucune table d'offsets contenant 0x188. |

### Mesures

| Fait | Verdict | Preuve |
|---|---|---|
| M1 : traces du mur (`W1-walk-L0`, `R9-deepwater-noboots`, `W2-L0-inside-R`) | CONFIRMÉ | Valeurs relues dans les `.txt` de l'annexe : x = 325,00 au tick 11 et ForceAdjusted au 12 ; x = 325,00 au tick 84, ForceAdjusted au 86, `3E` au 87 ; position figée à 624,00 avec ForceX +6912 par tick. |
| M2, M3, M4 : sondes `s5`, `s6`, `s7`, `s9` | CONFIRMÉ | Rejouées depuis ma copie du harnais : sorties identiques octet pour octet. Le « dès le tick 1 » est fiable, car le harnais passe par le vrai `InitializeEntity 0x80039D04`, qui appelle la sonde en `0x80039F30`. |
| M3 : « poussée ajoutée au tick suivant » | CORRIGÉ | Dans `s9`/T2, la poussée écrite avant le tick 5 est consommée au tick 5, dans la passe de forces du même tick. Les gestionnaires (`0x800386D0`) précèdent la passe (`0x80038364`). Le test d'i2b doit viser cet ordre. |
| M5 : recensement | CORRIGÉ en partie | Reproduits à l'identique : 44 cartes, 5539 cases (3772 à sec, 1767 sur eau profonde), portes 4663 / 735 / 141, 704 verrous dans 23 cartes, 16 cartes d'eau profonde, `SlideEffectId` ≠ 0 seulement sur 159 et 160. Quatre corrections au §2. |
| M6 : 76 portails sur 3316 arrivent sur une case d'eau profonde | CONFIRMÉ pour le décompte (rejoué) ; NON VÉRIFIABLE pour le sens | Que la tuile d'arrivée soit la position exacte reste une inférence, comme le dit la découverte. |

### DLL au HEAD

| Fait | Verdict | Remarque |
|---|---|---|
| C1 : `RunOneKinematicTick` pose `AdjustedForceX = ForceX` ; le serrage ne tourne que pour `HasLogicalZ`, après les porteurs | CONFIRMÉ | `ScriptedMotion.cs:643`, `:666`, `:813`. Voir la contradiction K9. |
| C2 : `PeekAdjustedForce` n'applique ni dérive ni poussée | CONFIRMÉ | `:782` appelle `PlanForce` seul. Dans le binaire, `0x80037364` ajoute bien `+0xDC/+0xE0` du porteur (`0x80037394-0x800373AC`), donc dérive et poussée comprises. |
| C3 : `PreviousAdjustedForce` sans consommateur, tests « à zéro » | CORRIGÉ | Aucun consommateur : exact. Mais `BeannoideOracleTests.cs:94-99` et `MurutaOracleTests.cs` posent le marqueur `0x5A5A5A5A` (non nul) et vérifient qu'il reste inchangé. Ces tests n'appellent que `RunPickedEvent` (aucune passe physique), donc i2b ne les touche pas, mais la phrase « à zéro » est fausse. |
| C4 : `TileAttributes` jamais écrit ni lu | CONFIRMÉ | Déclaration `EntityScriptProxy.cs:478`, copie de clone en `:2913`. |
| C5 : sondes et accesseurs | CONFIRMÉ avec précision | `RunHeroProbes` (`:1877`) et `TickMotion` (`:1795-1835`) sont dans `EntityScriptProxy.cs`, pas `ScriptedMotion.cs`. Voir K7 pour ce qui est réutilisable. |
| C6 : masque de marche | CONFIRMÉ | `WalkabilityMaskFor` (`CellsCollisionField.cs:147`), `WorldProxy.cs:2271`, `EntityScriptProxy.cs:683`, `BlockedCorners` (`ScriptedMotion.cs:983`), `RunMontage.cs:63`. Le moteur ne lit `IsWalkable` qu'en `CharacterControllerComponent.cs:1351` (sous-module `92d8e847`). |
| C7 : mots de carte `Gravity`/`ZViscosity` | CONFIRMÉ | Le fichier est `Alundra/Entities/Spawning/EntitySpawnFactory.cs` (`:888`, `:707`). `WorldProxy.cs:2254-2266` confirmé. L'export porte déjà `SlideEffectId` (propriété `"1"` sur la 159) et l'octet Slope complet (`slope[36*52+14] = 0x9C` sur la 416). |
| C8, C9 : nage faite, briques de la branche verrouillée | CONFIRMÉ | `CancelItemMode` est un bouchon (`:758`). |
| C10 : le mur s'exprime par un pseudo-bit dans le champ de cases, sans changement moteur | CONFIRMÉ comme conception | Le moteur transmet le masque tel quel ; `TrySampleGround` calcule `IsWalkable` dans la DLL. L'échelle (`0xC00 & 0x800 ≠ 0`) est bien évitée par l'égalité. |
| D : références du plan (D-E20-31/43/61, ligne 694, O-H-2 en `:12233`, D-C2B-14, D-H2-4/6) | CONFIRMÉ | Les références pointent au bon endroit. |

## 2. Corrections à reporter dans le rapport

- **K1, cascade de la 416.** « Les rangées 36 à 42 ont aussi le bit 0x08 » est faux. La rangée 38 vaut `0x54` (porte `0x04` seule). Les rangées avec les deux portes sont 36, 37, 39, 40, 41, 42 (12 cases) ; `0x04` seul pour 35 et 38 (4 cases). Verrous : 36, 39, 41 (`0x9C`). La dérive est vers le sud (direction 1). C'est le tableau de la découverte (gate1 = 4, gate3 = 12) qui est juste, pas sa phrase. À utiliser pour l'oracle de la 416. Source : `out_c3_416_belt_cells.txt`.
- **K2, entités posées sur courants.** Le nombre « 13 cartes (49, 77, 79, 116, 127, 403, 404…) » vient de la lecture x,y de l'objet de calque, qui place les entités une rangée trop haut. Le script `s8` de la découverte n'implémente d'ailleurs pas ce recensement (seule la partie portails). Avec la formule de spawn de la DLL (`EntityRecordMapper` : x = XPos·12+12, y = YPos·8+8), je trouve 10 cartes et 63 entités : 19, 77 (18), 79, 100, 116, 127 (22), 289, 324, 403, 404. La carte 49 n'en fait pas partie. Sur ces 63, 56 sont sur une case à porte `0x04` (elles dérivent au sol) et 7 (403 et 404) sur une case `0x08` seule (elles ne dérivent qu'en l'air). Sprites les plus fréquents : 54 (21), 140 (14). Source : `out_c10.txt`.
- **K3, vocabulaire de Q-I2-4.** L'objet porté n'est pas « lâché ». Le mode 1 de `0x8002F884` (`CarryRules.UpdateCarried`, `Alundra/Hero/CarryRules.cs:128`) le tire vers Alundra puis le détruit après un compte de ticks, et seulement si sa fiche d'équilibre a des attaques ; sinon il reste en main. C'est aussi ce que dit Q-C2B-6 (« arrête et détruit »).
- **K4, trois sens de « slope ».** À nommer dans le plan : (1) l'octet Slope de case (genre sur les bits 0-1, portes `0x04`/`0x08`, direction `0x30`, courant `0x40`, verrou `0x80`) ; (2) `Slope_18c` = `+0x18C`, le minimum de `(GP>>1)&7` des 4 coins (« pente 4 » = eau profonde) ; (3) le genre de pente (`Slope & 3`) dans le calcul de hauteur du sol.
- **K5, entités isolées verrouillées.** Les 7 cartes d'Inoa (271, 289, 293, 297, 301, 357, 481) ont bien une case unique en (5, 32), `0xA7`, genre 3. La 151 (Murgg Tree, intérieur) est en (5, 12), `0x94`, genre 0, donc pas `0xA7`.
- **K6, carte 19.** « `0x54`, 140 cases » : il y en a 136 sur sable (GP 6), 164 `0x54` au total. La case (44, 40, 2) proposée pour le préréglage de la cape est bien du sable sans courant (pente `0x04`, marchabilité `0x18`).
- **K7, vecteurs de poussée.** Les 200 vecteurs couvrent les 16 indices, mais 15 décalages sur 32 (0, 1, 3, 4, 6, 8, 11, 13, 18, 21, 22, 23, 26, 27, 31), et 163 des 200 lèvent le drapeau de serrage. « Tous les décalages » est trop fort ; la sémantique `srav` est un seul chemin de code, donc c'est suffisant.
- **K8, chemins.** `RunHeroProbes` et `TickMotion` : voir C5. `EntitySpawnFactory` : voir C7.
- **K9, contradiction interne dans i2b.** La description dit « fonction avec serrage, appelée avant l'ajout du porteur », alors que le risque 10 interdit de toucher à l'ordre du serrage (sous peine de déplacer les tests d'e0k3). Résolution proposée : la fonction pure rend l'ajusté sans serrage, ou le serrage est un paramètre éteint par défaut, et `ClampStepToMapEdge` reste où il est. Sinon, l'écart d'ordre de C1 devient un déplacement de tests à assumer.

## 3. Ajouts : manques relevés

1. **Guard à ne pas casser dans i2c.** `MarkedEntityGroundSlopeTests.S2_AnEntityBornByTheRecordsOfTheMap…KeepsZero…` fige que les entités posées par la carte gardent `Slope_18c = 0` et `CombinedVramFlags = 0`. La DLL le justifie par 444 instances à `DestroyOnSlidingSlope` (`EntityScriptProxy.cs`, doc de `UpdateGroundSlope`). Si Q-I2-3 prend l'option A (toutes les entités dérivent), la sonde de toutes les entités ne doit écrire que `TileAttributes`. Elle ne doit pas réutiliser `UpdateGroundSlope` ni `UpdateVramFlags`.
2. **Briques déjà présentes pour i2c.** `TileX`/`TileY` (= `+0x120`/`+0x124`, tenus à jour dans `RunOneKinematicTick`), `PlayerAnimationEffects.BinaryColumn`/`BinaryRow` (bornes 0 à `0x33` et 0 à `0x3B`), `TileEffect.ColumnOf` (`p/24`), `CellsCollisionField.TryReadBinaryCell`. Il ne manque que l'accesseur de l'octet Slope.
3. **Piège de i2a.** `ResyncControllerFromFlags` appelle d'abord `ApplyGravitySettingsToController`, qui remet `Gravity` et `MaxFallSpeed` à 0. La révision 3 de D-H2-6 l'a déjà relevé pour la cape. La fonction par tick du mur ne doit donc jamais appeler cette resynchronisation ; elle écrit seulement le masque.
4. **Même fonction que la cape.** Le mur et la cape sont les deux bits d'une même écriture du masque du héros. Celle qui passe la première (h2 ou i2a) crée la fonction ; le texte d'h2, qui dit « fonction neuve », est à ajuster.
5. **Niveau des bottes.** Le mur vaut pour `BootsLevel < 2`, c'est-à-dire aussi avec l'objet `0x1A` (niveau 1) ; la trace `W1-walk-L1` le montre. Les questions doivent dire « sans les bottes de nage (objet `0x1B` ou `0x1C`) » et non « sans bottes ». Le binaire lit la valeur mise en cache à la fin du tick précédent (`EquipmentRecords.BootsLevel`).
6. **Ordre avec h0a.** La branche verrouillée de i2c appelle `0x800350C0` et `0x8002F884(1)`. `CancelItemMode` est un bouchon tant qu'h0a n'est pas faite ; planifier i2c après h0a, ou accepter le bouchon. Pour le sol/air, relire `IsHeroInTheAir` (la lecture de `+0x144`) et sa mise en garde V-R4 sur la première image.
7. **Sonde à l'adoption.** Le binaire sonde à la naissance de toute entité créée par `InitializeEntity` (appel en `0x80039F30`) : dérive dès le tick 1. La DLL n'a pas cette sonde pour le héros adopté (risque 7 confirmé). Les autres sondes du héros (`Slope_18c`…) ont déjà ce retard d'un tick à l'adoption ; à traiter ensemble.
8. **Étendue des cartes 159 et 160.** Tout ce qui est intégré par `RunOneKinematicTick` est divisé par deux en horizontal : ennemis, objets lancés, projectiles, plateformes. Le seul site d'intégration est ce tick (`FinalForceX` n'est écrit que dans `ScriptedMotion.cs` et le clonage du proxy). Les rebonds verticaux ne sont pas touchés. Le texte de Q-I2-1 doit le dire.
9. **Tests à inventorier par exécution à T0.** 40 fichiers de test mentionnent eau, nage ou `Slope_18c` (liste dans ma lecture : `C4WorldTests`, `LiftableAiMap392Tests`, les arcs de levier, `HeadlessIntroSimulation`, `HeroAttackTests`, etc.). La découverte n'en cite que trois. Je n'en ai trouvé aucun où le héros marche dans l'eau profonde sans bottes, mais seule une exécution avec sonde jetable le prouve. À contrôler aussi : un passager sans bottes porté au-dessus de cases d'eau profonde serait bloqué à chaque tick par le mur (la plateforme de la carte 72 flotte sur des cases qui ne sont pas de l'eau profonde ; vérifié, GP ≠ 8, donc `porter-plateforme-m72` n'est pas touché).
10. **Tests qui bougent, liste affinée.**
    - i2a : `HeroSwimArenaTests.WithoutTheBoots_TheHeroSwimsToo_AnAcceptedDeviation_O_E19_22` (s'inverse), docs `HeroSwimTests.cs:23`, `HeroRunTests.cs:24`, `PlayerManager.cs:48` et `:670`, `RunMontage.cs:63` (le montage pose le masque à la main, il doit recevoir le bit du mur pour les traces L0/L1), `CellsCollisionFieldTests` (nouveaux cas), `TestSavesTests` (`AllPresetNames` est figé aux lignes 103 et 121-122, donc tout préréglage ajouté le déplace et recoupe b2/c2b), `CarryLockSwimAndStatesTests.SwimScene` (le héros démarre dans l'eau sans marcher : doit passer, à vérifier).
    - i2b : aucun, sauf commentaire `ScriptedMotion.cs:630` ; `PickupAiDropWorldTests` règle la gravité à la main et n'a pas de `SlideEffectId`.
    - i2c : aucun test existant ne lit `TileAttributes`, hors la garde S2 ci-dessus.

## 4. Questions à l'auteur, reprises avec corrections (défaut « comme l'original »)

- **Q-I2-1 (découpage).** Même description, avec une correction au 2e morceau : sur les cartes 159 et 160, non seulement Alundra, mais aussi les ennemis, les objets lancés et les projectiles avancent deux fois moins vite en horizontal. (A, défaut) mur, forces, puis courants et case verrouillée après la blessure ; (B) un seul bloc après c2b.
- **Q-I2-2 (Alundra déjà dans l'eau sans bottes de nage).** Inchangée : coincée comme dans l'original, ou nage tant qu'elle y est (écart documenté). Les 76 arrivées de portails sur 3316 sont un décompte reproduit ; leur sens exact (tuile ou position) reste à confirmer.
- **Q-I2-3 (qui dérive).** Corrigée : 10 cartes et 63 entités posées sont sur une case de courant (56 dérivent au sol, 7 seulement en l'air), pas 13 cartes. (A, défaut) toutes, comme l'original ; (B) seulement Alundra et les entités nées en jeu, écart documenté.
- **Q-I2-4 (case verrouillée).** Corrigée : sur ces cases, Alundra ne se commande plus (ni marche, ni saut, ni attaque, ni plongée, ni objet du Rond) et l'objet qu'elle porte est aspiré puis détruit. Cases concernées : trois rangées de la cascade de la 416 (36, 39, 41), le tapis de la 125 (217 cases), une case des 7 Inoa en (5, 32), une en (5, 12) dans la 151. (A, défaut) avec les courants ; (B) plus tard (O-H-2). Avec (B), le tronçon verrouillé de la cascade se nagerait à contre-courant.
- **Q-I2-5 (bord de carte, O-E19-17).** Inchangée. (A, défaut, D-E19-25) hors i2.
- **Q-I2-6 (préréglages de recette).** Inchangée, avec deux faits : la berge de l'étang de la 14 existe déjà (`swim-m14`, case (40, 48, 1), eau profonde à partir de la rangée 49 : vérifié) ; `mur-m14` est le même sans les bottes. La berge de la cascade en (13, 34) est à hauteur 5 contre 4 pour l'eau : à vérifier en jeu.

## 5. Limites

- Aucun test C# lancé ; les listes de tests sont faites par lecture.
- Les données viennent de `data-extracted/` (copie du remaster) et de `alundra-project/Maps`.
- Les écritures de `+0x188` par déplacement calculé de la VM ne sont pas exclues formellement (balayage des données négatif).

## 6. Chemins

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2-critic/`

| Script (dans ce dossier) | Sortie |
|---|---|
| `c1_scan_imm.py` (balayage de tout immédiat, appelants `jal`) | `out_c1_imm188.txt` |
| `c3_belt_cells.py` (cases à courant d'une carte) | `out_c3_416_belt_cells.txt`, `out_c3_locked_cells.txt` |
| `c4_census.py` (recensement indépendant) | `out_c4_census.txt` |
| `c5_tables.py` (tables de dérive) | `out_c5_tables.txt` |
| `c6_slide.py` (`SlideEffectId` sur 483 cartes) | `out_c6_slide.txt` |
| `c7_datascan.py` (balayage des données pour 0x188) | `out_c7_datascan_188.txt` |
| `c8_hdr_readers.py` (lecteurs de l'en-tête) | `out_c8_hdr_readers.txt` |
| `c9_grid.py` (grilles eau profonde) | `out_c9_map416_grid.txt`, `out_c9_map14_grid.txt`, `out_c9_map72_grid.txt` |
| `c10_entities_on_belts.py` (entités posées sur courants) | `out_c10.txt` |
| `r_s5_belt_probe.py`, `r_s6_belt_probe2.py`, `r_s7_belt_wall_probe.py`, `r_s9_belt_target_push_probe.py` (rejeux des sondes) | `out_r_s5.txt`, `out_r_s6_belt_probe2.txt`, `out_r_s7_belt_wall_probe.txt`, `out_r_s9_belt_target_push_probe.txt` |
| `r_s8_portal_deep.py` (rejeu du recensement de portails) | `out_portal_deep.txt` |

Autres éléments : `bx.py` (aide de désassemblage), `oc/base/` (copie du harnais `e20-d`, identique à celle de la découverte), `head/` (sources de la DLL au HEAD).