ATTENTION : `report.md` n'a pas pu être écrit. L'outil Write l'a refusé (« Subagents should return findings as text, not write report files »). Je ne l'ai pas contourné par Bash, donc **le rapport est ci-dessous en entier**. Scripts, sorties et `SHA1SUMS.txt` (282 fichiers) sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g2c1-disc/`, sorties dans `out/`.

# RAPPORT T0 (lecture seule), reprise de E20.g2c1 (C4)

## 0. Provenance
- HEAD `73c05530`. Je n'ai rien écrit dans le dépôt. `git status` ne montre que des fichiers antérieurs et `Alundra.Tests/Probes/G3T0ProbeTests.cs`, qui est l'autre tranche (E20.g3).
- Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8da…5b6445` vérifié. Capstone 5.0.7.
- Aucun build, aucun test, aucune sonde de la DLL, aucune commande git qui écrit.
- Rejeu du T0 : `s1_flat`, `s2_wall`, `s6_touch`, `s7_stack` sont identiques à l'octet à ceux du dépôt (`cmp`).
- FAIT = lu (binaire, code, export, plan). MESURÉ = rejoué sur le vrai code du binaire (harnais : manettes, champ de cases, placement). INFÉRENCE = déduit, non exécuté.
- Positions des enregistrements : pixel X = `XPos*12+12`, pixel Y = `YPos*8+8`, Z = `Height*8`.

## 1. Synthèse

| Q | Réponse | Preuve | Défaut proposé |
|---|---|---|---|
| Q1 `+0x18C` | Nature du sol sous les 4 coins : min de `(mot&0xE00)>>9`, 4 = eau profonde. Écrit par un seul `sw` (`0x80038348`, `UpdateEntityTiles` `0x80038064`), pour toute entité à Gravité. Seuls 8 troncs posés par la carte en ont besoin. La DLL a déjà l'équivalent pour les entités marquées (`Slope_18c`, f6a). | FAIT, MESURÉ | Alimenter le seul type 373 (jeu de types lu par `TickMotion`). |
| Q2 43 troncs | Jamais C4 (31 programmes, 0 `40 02 00`, 0 `41`). Leur A **efface `Collidable` (0x80)** : non soulevables. | FAIT ; non soulevables = INFÉRENCE solide | Rien à porter, un témoin. |
| Q3 coins / O-G-2 | **Deux fonctions** (trois avec l'écrivain des pointeurs `0x800370C4`). C4 lit 16 bits par coin sans qualification. `UpdateEntityTiles` lit 32 bits qualifiés par niveau et range OU/ET. La DLL n'a aucun lecteur par coin. La mort dans un trou se fait par la phase de choix, **à l'atterrissage**. | FAIT, MESURÉ | Accesseur neuf « mot des 4 coins » pour C4. O-G-2 : option (question G-1). |
| Q4 `+0x80` | = `SpriteProgramIndexes[4]` (octet 0x16 de l'en-tête, code E). 2 pour 290/293/373, 0 pour 363/367/368/369. La DLL l'expose déjà. | FAIT | Rien à ajouter. |
| Q5 sm1 | sm1 est READY mais **non exécutée** (aucun `case 0x60/0x61` au HEAD). La recette 96 dépend de sm1 pour la **réaction des plaques**, pas pour lever, jeter ou empiler. | FAIT | Ordre du plan gardé ; g2c1 se clôt sans sm1 ; recette « plaques » différée. |
| Q6 support mobile | **Oui** depuis D5b. Cas réel du corpus : la pierre de la 444 sur une trappe qui descend. | FAIT (code, ADR-0088) | Test de monde + recette sur la 444, pas de code. |
| Q7 inventaire | Voir §7. Le verrou `+0x278` **existe déjà**. L'ensemble câblé n'existe pas au HEAD. Aucun besoin du moteur. | FAIT | — |
| Q8 sondes | Cartes de sonde au §8, valeurs rejouées tick par tick. J'ajoute P5 (troncs flottants). | MESURÉ | — |
| Q9 | Faits qui changent le plan (§9) : blocs de glace permutés en C4, deux objets permutés en C64 et C96, mort dans les trous. Une question de jeu (G-1). | FAIT, MESURÉ | §9 |

## 2. Q1 : `+0x18C`
**FAIT, binaire**
- Écrivain unique : `0x80038348` dans `UpdateEntityTiles` (`0x80038064-0x80038360`). Il s'exécute pour chaque entité de la liste physique (statuts 2 et 3, `+0x20==0`), en fin de passe (`0x800384BC`).
- Balayage de l'image entière avec `scan_field.py`, puis `scan_field2.py` pour les accès par pointeur pré-décalé. Il exclut les copies de structure.
- Gravité (`+0x6C&0x100`) claire : `+0x180 = +0x184 = +0x18C = 0`.
- Sinon, pour chacun des 4 coins (pointeurs `+0x148..+0x154`, hauteurs `+0x158..+0x164`) :
  - Un coin est « au niveau » si `+0x158[i]+1 == +0x1E0`. Il range alors son mot 32 bits et `t1 = min(t1, mot&0xE00)`.
  - Un coin hors niveau range 0 et remet `t1` à 0 (`0x80038154`), sans retour.
  - `+0x180` = OU des mots, `+0x184` = ET, `+0x18C = t1>>9`, `+0x190` = ancienne valeur.
- Lecteurs de l'entité elle-même :
  - Phase de choix `0x80038778` (accès `-0x9c($s0)`, `s0=base+0x228`) : `(flags&0x100000) && +0x18C==4` donne `DestroyEntity(e,6)`. Tonneau, pierre et objets ont `0x100000` ; **le tronc (0x2323E4) non**.
  - Ombre `0x80038FA0` (cosmétique).
  - C4 `0x80066BA8` et `0x80066BD0` : tronc `0x175` seul (anim 0 et pente 4 donne anim 5 ; anim 5 et pente autre que 4 donne anim 0).
  - E9 `0x8007EF84`, `0x8007F0F0` et E17 `0x8007F578` (entités nées en jeu), puis le héros.

**DLL au HEAD**
- `Slope_18c` et `UpdateGroundSlope()` (`EntityScriptProxy.cs:2201`) sont nourris pour les entités marquées `HasLogicalZ` (`TickMotion` `:1824-1827`, f6a, ADR-0087).
- `PutInHands` pose le marqueur (`PlayerManager.cs:1316`). La mort dans l'eau d'un objet jeté marche donc déjà par `EntityScriptProxy.cs:1936`.
- Manque : le tronc posé par la carte (marqueur absent, « entité placée garde 0 », `:2186-2192`).
- **MESURÉ** (`q8_p5.py`, `out/q8_p5.txt`) : les 8 troncs sur l'eau sont à l'anim 5, pente 4, dès le **tick 1** dans le binaire. Ce sont la carte 70 recs 9, 10, 11, 12, 21 et la carte 73 recs 11, 12, 36. Le tronc natif hors eau (70 rec 8) reste à l'anim 0.
- Aucune sauvegarde de `tools/Saves/` n'arrive sur la 70 ni la 73.

**Options**
- (a) défaut : jeu `{373}` lu par `TickMotion` (`HasLogicalZ || LiftableAi.FeedsGroundSlope(SpriteType)`). Aucun effet de bord, retrait = vider le jeu.
- (b) toutes les entités à Gravité. Mes recensements :
  - 14 types de pages de cartes portent `0x100000` : 2 277 placements.
  - Au repos (6 ticks, vraies cases), le binaire ne détruit ni ne change aucun des **2 033 placements natifs** testés (1 976 C23 + 57 des sprites 134 et 174). Le T0 en avait 171 inertes plus les 8 troncs.
  - Le « 444 instances » de la doc d'`UpdateGroundSlope` n'est ni reproduit ni réfuté.
  - (b) ne change donc visiblement que les 8 troncs. Elle ne couvre pas les 12 sprites globaux.
- (c) rien : les 8 troncs ne flottent pas.

## 3. Q3 : coins et O-G-2
**Trois fonctions (FAIT)**
1. Écrivain `0x800370C4` : coins `(PosX+[+0x1E4], PosY+[+0x1E8])` et `(+[+0x1F0], +[+0x1F4])`. Il range pointeur et hauteur (écriture `0x800371D8`, case = `TILES+0x604+rangée*416+colonne*8`). Appelé par les essais de pas (`0x80037604`, `0x8003768C`, `0x8003782C`).
2. Lecteur C4 : boucles `lw` pointeur puis `lhu` (16 bits bas) en `0x80066ADC-0x80066B04` (bloqué au sol : un coin à `(mot&0x1001)==0x1001` donne statut 3) et `0x80066B4C-0x80066B74` (libre : anim 0 seulement si aucun coin à 0x1001).
3. Lecteur `UpdateEntityTiles` : 32 bits, qualifié, OU et ET.

**Ce ne sont pas la même fonction.**
- Un OU peut valoir `0x1001` sans qu'aucun coin ne le vaille. Un ET exige les quatre coins.
- **MESURÉ** : en vol au-dessus d'une bande 0x1001 (T+19 à T+21), les 4 mots valent `0x1001` alors que `+0x184` reste 0 jusqu'à l'atterrissage (T+22).

**Où pointent les pointeurs (MESURÉ)**
- `q3_corners.py` rappelle le vrai `0x800370C4` en fin de tick et restaure les 8 mots.
- Sur 3 scénarios et 183 ticks, dont les ticks bloqués, **les pointeurs désignent toujours les 4 coins de la position finale du tick** (183 sur 183).
- Ils sont périmés tant que l'objet est porté, sans conséquence.
- La DLL peut donc échantillonner à la position courante dans `C4Tick`. Les coins sont X-10 et X+10, Y-7 et Y+7. Ils sont identiques à `UpdateGroundSlope` : `Width=(21<<16)-1`, `ModX=-10px` (`EntitySpawnFactory.cs:850-865`).

**DLL au HEAD**
- Existent : `CellsCollisionField.SampleRawWalkability` (`:226`) et `SampleGroundProperty` (`:202`), et `UpdateVramFlags` (`EntityScriptProxy.cs:2274`).
- `UpdateVramFlags` ne sert que le **héros** (D-T-12). `TerrainProbe.ProbeVramFlagsCorner` qualifie par hauteur, il ne convient donc pas à C4.
- `UpdateEnemyGroundHeights` (`:2399`) ne stocke que des hauteurs.
- Aucun lecteur de `0x1001` (commentaires seulement).
- `SlideAlongWall` (`ScriptedMotion.cs:853`) est un autre glissement, sans rapport.
- E2 lit `CombinedVramFlagsAND & 4` (`LiftableAi.cs:71`) : branche morte hors héros. La phase de choix (`EntityScriptProxy.cs:1941`, `OR & 0x8004`) est vivante mais jamais nourrie hors héros.

**O-G-2, mort dans un trou (MESURÉ, `q3_pit.py`)**
- Tonneau, tronc et objet étoile jetés sur des cases `wk 0x04` ou `gp 0x80` : `+0x184` vaut 4 ou `0x8000` **à l'atterrissage** (T+22). Le statut passe à **4** à T+23, sans passer par 3, avec l'effet 10. Libération à T+24.
- Le mécanisme est la phase de choix `DestroyOnVramFlags` (`0x800387A0-0x800387C8`), **pas E2**.
- Le plan disait « pendant le vol » : c'est faux, c'est à l'atterrissage.
- Pour la DLL : `if (HasLogicalZ) { UpdateGroundSlope(); UpdateVramFlags(); }`.
- Types à Gravité portant `0x200000` : les 7 C4, les 6 C23, la torche, les sprites 134 et 174 (le pilier, posé par la carte donc non marqué).
- Les caisses et jarres jetées dans un trou disparaîtraient aussi (fidèle).

**Variante MESURÉE : bloqué au sol sur une bande** (bande 0x1001 colonnes 6 à 9, mur `0x1041` en colonne 8)
- Tonneau et tronc : statut 3 à T+25, anim 2 à T+26, drapeaux `0x3323E4→0x3323C4` (tronc `0x2323E4→0x2323C4`), statut 4 à T+28, libérés T+29, effet 10.
- Objet étoile (E0 en ligne) : statut 3 à T+26, 4 à T+27, libéré T+28.
- Mur en colonne 9 : tout décalé de +8.

## 4. Q2 : les 43 troncs scriptés (FAIT, `q2_logs.py`)

| Carte | Troncs | Programmes A | Programmes C |
|---|---|---|---|
| 72 | 6 | 140, 154 | 140, 154, 155 |
| 73 | 21 | 143, 150 | 143, 144, 145, 148, 150, 151, 158 |
| 79 | 5 | 142, 143, 148 | 142, 143, 148 |
| 140 | 2 | 157 | 157, 158 |
| 158 | 9 | 136, 137, 148, 161 | 136, 137, 148, 161 |

- Le programme exécuté est `table[indice&0x7F]` (`EventProgramRunner.cs:355-361`). Les 13 autres troncs (cartes 70 et 73) sont natifs.
- Aucune des 30 cartes de la chaîne n'en porte (recalculé : 0 C4 et 0 C70 ; 219 C23 sur 18 cartes, `q9_chain.py`).

**Ce que font les programmes**
- A, une fois au chargement : `62` pose et `63` efface des drapeaux de l'entité elle-même (type de recherche `0x80`).
  - Flags finals calculés (`q2_flags.py`) : `0x232302` (5), `0x232306` (36), `0x236306` (2, NoRiders).
  - **`Collidable` est effacé sur les 43.** Gravité et genre 1 restent. `HitsClassB` est posé, donc le tronc blesse le héros (INFÉRENCE : le héros porte ClassB).
- C, six formes :

| Forme | Nombre |
|---|---|
| `1A 07 ; 37 ; 08 ; 49` | 6 |
| `1A 07 ; 1E ; 08 ; 1E ; 08 ; 49` | 4 |
| `0B ; 0A ; 49` | 4 |
| `00 07 4A 0A 49` | 2 |
| `1A 07 ; 00 ; 24 ; 0A ; 49` | 1 |
| `1A 06 ; FF` (immobile) | 2 |

  `49` relance le programme. `FF` le laisse arrêté (l'état n'est pas effacé, `EventProgramRunner.cs:480-487`).
- **Aucun** des 31 programmes (12 A et 19 C) ne contient `40 02 00` ou `41`. Aucun autre programme des cinq cartes ne touche leurs drapeaux (`q2_flagops.py`).

**Soulevables ? Non.**
- `FindEntityCollisionCandidate` `0x80036F34` exige un sujet collidable (`andi 0x80` en `0x80036F74`) et `+0x28==0`, et ne parcourt que la liste `0x80127C28`, comptée par `0x80127D2C`.
- Cette liste est reconstruite chaque tick (`0x800384F4-0x800385D0`) à partir de `(+0x6C&0x80)`, `(+0xB4&0x80)==0`, `+0x28==0`.
- Un tronc non collidable n'est donc ni contact du héros, ni support (`CheckRidingEntities` `0x800364C8` parcourt la même liste).
- Réponse à la question A4 du T0 : on ne les soulève pas.

## 5. Q4 : `+0x80`
- `InitializeEntity` `0x80039D04` copie les octets 0x13 à 0x17 de l'en-tête dans `+0x70/+0x78/+0x7C/+0x80/+0x84`, et 0 dans `+0x74`. Ce sont les codes natifs des slots A, B, C, D, E, F.
- `+0x80` est le code E (`ProgramDeactivate`).
- C4 le lit en `0x800669BC`, seulement au code de lâcher -1 (blessure, nage, mort ; `UpdatePlayerCarriedEntity` mode 2) : `+0x88=3` et, si `+0x80==2`, drapeaux `(f|0x10)&~0x80`.
- Pour les codes 1 à 3, C4 ne touche **pas** aux drapeaux (C23 écrit toujours `(f|0x30)&~0x80`).
- DLL : `SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate]`, posé à la naissance (`WorldProxy.cs:2009`, `EntitySpawnFactory.cs:779`), écrit par `0x41` (`EventProgramRunner.cs:865-873`), lu par le dispatcheur (`:296`, `:307`).
- C4Tick lira `== 2`. Au HEAD seul le mode 2 de nage atteint ce code (`CarryRules.UpdateCarried` met `Flags2=-1`). La blessure et la mort sont des bouchons (E20.c2b, E18).

## 6. Q5 et Q6
**Q5, sm1**
- `docs/plan-e19-opcodes.md` §1.4.2 : « READY à la seconde relecture, à réviser (D-E19-115, D-E19-122) ». Il n'y a que des commits de plan (`564bef34`, `89731b86`, `c81ada50`).
- Au HEAD, `EventProgramRunner.cs` n'a ni `case 0x60`, `0x61`, `0x3C`, `0x3D`, et sm0 n'a pas de code non plus.
- Décisions : Q-sm-13 A / D-E19-112, sm1 « juste après sm0 », plaques insolubles jusqu'à g2c. Exception de la 226 : D-E19-122.
- La carte 27 lit `RidingEntity` par `0x60/0x61` (`C[2]` boucle à 4 phases, `C[26..29]` attentes, `C[36]` pile de quatre) :
  - Sans sm1 : plaques inertes, 17 attentes bouclent, la récompense part seule dès la mort des momies.
  - Avec sm1 sans g2c1 : le passage reste fermé (coût accepté).
- Lever, jeter, empiler et mesurer `RidingEntity` ne dépendent pas de sm1 (P4 le mesure). La réaction des plaques en dépend (187 sites, cartes 27, 29, 30, 113, 400).
- Défaut :
  - Garder l'ordre du plan.
  - Clore g2c1 sur la recette `66-crypte-de-lars.sav` (carte 22, tonneaux natifs recs 21 et 33) et sur les tests de monde des plaques.
  - Différer la partie « plaques » de 96 (27) et 91 (29), non bloquant.
- Sauvegardes qui arrivent sur une carte de la famille : 22 (66), 27 (96), 29 (91), 52, 53, 86, 93, 116, 140, 148, 344, 423, 425, 426, 444 (`300-mont-torla`). Aucune sur 70, 73, 113, 400.

**Q6, support mobile : oui (FAIT)**
- `EntitySupport.UpdateRidingEntities` (`EntitySupport.cs:288-296`) calcule `RidingEntity` de tout collidable avec Gravité et sans `NoRiders`, à l'étape des listes (`WorldProxy.cs:3819`). Les 7 types C4 y répondent : `0x3323E4`, `0x332384`, `0x2323E4`.
- `ScriptedMotion.cs:649-660` additionne la force prévue de **chaque porteur de la chaîne** pour **tout** passager. La restriction « héros seul » du T0 (`:677`) n'existe plus.
- La force Z est copiée sur 3 chemins (`CopyCarrierForceZ`).
- ADR-0088 : « a crate, a barrel, an NPC or a marked object on a moving platform follows it ».
- Tests existants dans `PassengerPlatformFollowTests` (`CrateA`, `NpcA`, `MarkedB_AMarkedBarrel…`, `PileZA`, `PileXYA…`) : passagers montés à la main à la boîte du tonneau (21x15x16). Rien sur un enregistrement C4 réel.
- Cas réel du corpus : pierre de la carte 444 (rec 25, (972, 376, 64) px) sur une trappe qui descend (annexe D5b `report.md:116`). Sauvegarde `300-mont-torla`.
- Aucun cas horizontal C4 dans le corpus. L'oracle S10 donne la valeur (X de 300,0 à 451,9, identique au support).

## 7. Q7 : inventaire

| Élément | Existe | Manque | Moteur ? |
|---|---|---|---|
| `C4Tick` | Moitié portée = `LiftableAi.Tick` (`LiftableAi.cs:102-132`), `ReleaseAnimation` (`:89`), `ReleasedFlags` (`:99`) | Moitié libre (157 mots) ; branche -1 avec `+0x80==2` ; drapeaux inchangés pour 1 à 3. Modèle et 1 244 vecteurs déjà faits (`c4_goldens.json`, SHA-256 `59167009…`). | non |
| Lâcher partagé C23/C4 | Les tables `0x800270C0`, `0x80027400`, `0x800281DC` sont identiques aux indices 0 à 3 (0, 3, 1, 4) | Factoriser `Tick` en lâcher plus politique de drapeaux **sans changer C23** (épingles `LiftableAiTests.cs:121`, `LiftableBreakTests.cs:152`) | non |
| Clés `(C,4)` | `Register` (`NativeSpriteRegistry.cs:54`, clé = (slot, code) unique). `(C,4)` est libre. 14 clés au HEAD. | Inscrire **290, 293, 363, 367, 368, 369, 373 et 304** (§9.1). Liste `C4Keys` séparée. Réécrire 4 épingles par dérivation (`WaveSwordLifeTests.cs:185`, `ChargedArrowAiTests.cs:237`, `LiftableAiRegistrationTests.cs:75`, `NativeSpriteRegistryTests.cs:58`). | non |
| E2 des types C4 | `BreakSpriteTypes` contient 290, 293, 373 (`LiftableAi.cs:53`). E0 en ligne (`EventProgramRunner.cs:336`) pour 363 à 369. | rien | non |
| Verrou `+0x278` | **Existe** : `EntityScriptProxy.DelayOrAngleOrEntityId` (`:1350`), cloné (`:2947`). Les autres écrivains (`PickupAi.cs:90,141`, `LootDrop.cs:73`, `SaveBook.cs`) sont sur d'autres entités. | Un nom local | non |
| Lecteur des coins | `SampleRawWalkability`, `SampleGroundProperty`, géométrie de `UpdateGroundSlope` | `TerrainProbe.SampleTileWordCorner` (non qualifié) plus « un coin vaut 0x1001 » | non |
| Ensemble câblé 7 vers 14 | **N'existe pas** : `rg WiredLiftable` ne trouve que 4 lignes du plan. Prévu : `ProductionWiredLiftableTypes` et `WiredLiftableTypesForTests` (D-G-8, §1.24.4 phase B), modèle `ProductionWiredWeaponTypes` (`PlayerManager.cs:1241`, seam `:1245`). `PlayerTryAction` reste un bouchon. | g1b le crée (7 types C23). g2c1 y ajoute 290, 293, 363, 367, 368, 369, 373. | non |
| Clôture D-G-8 | « un type câblé a tous ses codes C, D, E enregistrés » | À étendre aux codes atteints par permutation `41` (304 vers C4/E0). Le code D 0 n'a rien à enregistrer. | non |
| Effet 9 du rebond | `EffectReservoir.Create(bool mapTable, int effect, int anim, x, y, z)` (`:101`). Modèle `NativeEffects.cs:274` (`z+0x80000`). L'effet 9 existe dans `effects-global.json` (29 effets). | Un assistant (table 0, effet 9, anim 0, Z+`0x80000`, repère DLL = binaire moins 1) | non |
| Son `0x18` | `SoundPlayer.PlaySfx` (`:119`), `sfx_0024.wav` présent | l'appel | non |

- Commentaire à corriger : `LiftableAi.cs:50` inverse tonneau et pierre. Le roster dit 290 = tonneau (index 34) et 293 = pierre (index 37).
- Doc inversée aussi dans `EntityFlags.cs` (Gravity) : « l'échantillonnage ne tourne que quand le bit est **clair** ». Le binaire échantillonne quand il est **posé**.
- **Besoin du moteur : aucun trouvé.** Seule P1 pourrait en révéler un : si le pas bloqué du tick du lancer ne lève pas `ForceAdjusted`, c'est un écart de `ScriptedMotion` ou du contrôleur, à consigner et non à contourner.

## 8. Q8 : sondes P1 à P4 (+P5)
**Montage commun**
- Copie de `BreakRun` (`Alundra.Tests/Ai/Objects/BreakRun.cs`) : monde réel vidé sauf le héros, champ synthétique `CutBench.Cell(Walkability, Height, Stack, GroundProperty)` (`CutBench.cs:49`), natifs coupés, héros (109,100) face à l'est.
- **Carré au tick 1 = T**, avec ou sans Droite tenue. L'oracle T=61 se lit « T+k » ; le test B3 de g2b a déjà ce repère.
- L'objet est le **vrai enregistrement**, né par `SpawnEntityByRecordId` (sans marqueur avant `PutInHands`), puis `PutInHands` sur la tête du héros :
  - tonneau 290 : carte 22 (`Lars' Crypt-22`) rec 21, (888, 464, 32) ;
  - pierre 293 : carte 24 rec 24, (852, 856, 32) ;
  - objet 363 : carte 27 (`Ancient Shrine-27`) rec 5, (816, 520, 32) ;
  - tronc 373 : carte 70 (`Coastal Cave-70`) rec 8, hors eau ; sur l'eau : recs 9, 10, 11, 12, 21 (P5) ;
  - plaque 108 : posée par l'interface, sans script (la rec 25 de la 27 est scriptée). Boîte 48x32x16, décalage (-24,-16,-8), drapeaux `0x60C0`.
- Lâcher de sonde : 6 lignes dans le test jetable (moitié portée de C4). `Flags2` donne l'anim par `ReleaseAnimation`, `Flags2=0`, liens vidés, **drapeaux inchangés**. Le C23 existant ne convient pas (7 types, `|0x30`).
- Z DLL = binaire moins 1 unité brute (ADR-0026) : comparer par dérivation.
- Arrêt : une valeur de la DLL qui diffère à un tick listé.

**P1 : `ForceAdjusted` contre un mur** (colonne 5 `Cell(0x40,0)`, MESURÉ `q8_p1.py`)

Tonneau, pierre et tronc en marche (identiques) :

| Tick | X | Z | `ForceAdjusted` | direction | verrou `+0x278` | Journal |
|---|---|---|---|---|---|---|
| T | 109,0 | 36,0 | **1** | 24 | 0 | son 26 |
| T+1 | 106,0 | 40,0 | **0** | **8** | **1** | effet 9 en (109,0 ; 100,0 ; 44,0) |
| T+2 | 103,0 | 43,5 | 0 | 8 | 1 | |
| T+3 | 100,0 | 46,5 | 0 | 8 | 1 | |
| T+6 | 91,0 | 52,5 | 0 | 8 | 1 | |

- Immobile : anim 3 et X = 109,0 / 107,5 / 106,0 / 104,5 / 103,0 / 101,5 / 100,0.
- Objets (boîte plus étroite) :
  - T : X=110,0 et `ForceAdjusted` **0**.
  - T+1 : `ForceAdjusted` 1.
  - T+2 : X=107,0, direction 8, verrou 1, effet 9 en (110,0 ; 100,0 ; 47,5).
- Mur en colonne 6 : aucun rebond (le test `ForceZ>0` échoue au sommet). Le tonneau en marche s'arrête à X=133,0 avec `ForceAdjusted` 1 aux échantillons T+9 à T+21 ; les objets à 134,0.
- P1 décide : si `ForceAdjusted(T)=0` pour le tonneau, le rebond ne part jamais.

**P2 : mots des 4 coins** (bande colonnes 6 à 9 `Cell(0x01,0,null,0x10)`, MESURÉ `q3_corners.py`)
- Ordre (x1y1, x2y1, x1y2, x2y2).

| Tick | X | Coins (colonne, rangée) | Mots | Anim |
|---|---|---|---|---|
| T+19 à T+21 | 169 à 175 (vol) | (6,5)(7,5)(6,6)(7,6) | 1001 ×4 | 1 |
| T+22 | 178,0 (sol) | (7,5)(7,5)(7,6)(7,6) | 1001 ×4 | 1 |
| T+40 | 232,0 | (9,5)(10,5)(9,6)(10,6) | **1001, 0000, 1001, 0000** | 1 |
| T+46 | 250,0 | (10,5)(10,5)(10,6)(10,6) | 0000 ×4 | 1 |
| T+47 | 250,0 | | | **0** |

- La glissade fait 3 px par tick de T+22 à T+46, sans casse (statut 2).
- P2b (bande plus mur) : valeurs du §3.
- P2c (eau `0x0800`) : tonneau et objets statut 4 à T+23, effet attaché 6, libres T+24 ; tronc anim 5 à T+23.

**P3 : `HitCounter` à T+21** (MESURÉ, `s6_touch` rejoué)
- Montage : test B3 de g2b (`LiftableBreakWorldTests.cs:140-180`, `targetX:165`) avec les index 34, 37 et 117.
- Attendu :
  - Rien avant T+21.
  - T+21 : `HitCounter` 0 vers 1, PV de la cible 9 vers 0, une étincelle.
  - T+22 : statut 3, anim 2, drapeaux `0x3323E4→0x3323C4` (tonneau et pierre) ou `0x2323E4→0x2323C4` (tronc).
  - T+24 : statut 4. T+25 : libéré. Même chronologie que la caisse.
- Objets (AttackCount 0, sans `0x20`) : aucune touche, atterrissage en 178,0 à T+22, anim 0 à T+23, drapeaux `0x332384` inchangés.

**P4 : `RidingEntity` sur une plaque** (plaque en (178,100), MESURÉ `q8_p4.py`, tonneau et objet identiques)

| Tick | X | Z | sol | `RidingEntity` |
|---|---|---|---|---|
| T+19 | 169,0 | 17,0 | 0 | aucun |
| T+20 | 172,0 | 16,0 | **1** | **aucun encore** |
| T+21 | 172,0 | 16,0 | 1 | **la plaque** (anim 0) |
| T+22 à T+50 | 172,0 | 16,0 | 1 | la plaque |

- Le support apparaît un tick après l'atterrissage.
- Pile de deux (S7) : le second objet atterrit à X=163,0, Z=30,0 (16+14), support = le premier, qui garde la plaque.
- Assertion DLL : `RidingEntity == plate.LogicContextEntity` à T+21, avec `otherModZ+Depth+1 == moddedPosZ` (`EntitySupport.cs`), `IsOnGround` 1 à T+20.

**P5 (ajoutée)** : troncs sur l'eau, vraies cases de 70 et 73. Anim cible 5, pente 4, sol 1, statut 2, dès le tick 1. Rouge avant le jeu `{373}`, vert après.

## 9. Q9 : ce qui change le plan de g2c1
**9.1 Blocs de glace permutés en C4 (FAIT, `q9_swaps.py`)**
- 4 des 15 blocs (sprite 48, type 304, en-tête C23/E2, `MoreFlags 0xC4`) exécutent à leur chargement `41 02 04` (C=4) et `41 04 00` (E=0). Ce sont la carte 103 recs 7 et 22, la 105 rec 2 et la 108 rec 6.
- La clé `(C,4)` doit inclure **304**, sinon un bloc soulevé n'est jamais relâché. C'est une exigence de clôture, pas une option.
- Jetés, ils ne reçoivent pas `|0x30` : pas de `0x10` ni `0x20` (INFÉRENCE sur les drapeaux). Ils restent posés comme des pierres.
- Le T0 disait « ne sont pas C4 » : vrai de l'en-tête, faux à l'exécution pour 4 exemplaires.

**9.2 Deux objets permutés vers des natifs non portés (FAIT)**
- Carte 113 rec 11 (tonneau) : `41 02 40`, soit C64 (`0x8007ADDC`, copie la hauteur du héros et inverse ses forces). Il reste soulevable mais C64 ne gère pas le lâcher : il resterait collé aux mains, comme dans l'original. La rec 10 reste C4.
- Carte 473 rec 0 (pierre) : l'A efface Collidable et Gravité et pose C96 (`0x800647B0`, projectile qui rebondit). Non soulevable.

**9.3 Qui exécute C4 après le chargement**
- Sur les 191 placements :
  - 130 natifs.
  - 12 qui reviennent au natif par `40 02 00` (6 tonneaux, 2 pierres, 4 objets).
  - 1 A seul (113 rec 10).
  - 2 (343 recs 18 et 19) dont l'A efface Gravité et genre sous un test `33` non élucidé.
  - 43 troncs jamais C4.
  - 1 tonneau (426 rec 27) dont le C ne rend jamais la main.
  - 2 permutés en C64 et C96.
- S'y ajoutent les 4 blocs de glace.

**9.4 Question à l'auteur G-1 : les trous et sols de sortie**
- *L'original* : un tonneau, une pierre, un tronc ou un objet jeté sur une case de trou ou de sol de sortie de zone **disparaît à l'atterrissage avec l'effet de casse** (les caisses et jarres aussi).
- *La DLL au HEAD* : les indicateurs ne sont nourris que pour le héros, l'objet resterait posé sur la case.
- *Option A (défaut)* : porter, dans g2c1 phase B (un appel de plus au crochet de f6a, un test de monde, borné aux objets passés dans les mains). Coût faible, réversible.
- *Option B* : ne pas porter (écart O-G-2 gardé). Les puzzles à trous seraient plus faciles que dans l'original.

**9.5 Écarts avec le T0 et le plan**
1. `+0x278` existe déjà (le T0 et l'esquisse disaient « nouveau »).
2. La mort dans un trou est à l'atterrissage, par la phase de choix (le plan disait « pendant le vol » et « E2 »).
3. Les 43 troncs sont non soulevables (le T0 laissait la question ouverte).
4. 4 blocs de glace sont C4, 2 objets sont C64 et C96.
5. sm1 n'est pas exécutée.
6. La restriction « héros seul » de `ScriptedMotion.cs:677` a disparu (D5b).
7. Le « 444 instances » n'est pas reproduit.
8. `LiftableAi.cs:50` inverse les noms tonneau et pierre.
9. La doc de `EntityFlags.Gravity` est inversée.

## 10. Non établi
- Le sens en jeu des cases `0x1001` (fosses, bords, murs invisibles) : mécanisme prouvé, lecture visuelle non.
- P1 sur la DLL : rien n'a été lancé.
- Non soulevables des 43 : déduit de `Collidable` effacé et de la liste des collidables, sans exécuter leurs programmes. Le sens de `HitsClassB` sur eux est une INFÉRENCE.
- C64 et C96 : lus seulement en tête. Le test `33` de la 343 n'est pas élucidé. Le C[155] de la 426 (`62 5B 78 19`) n'est pas lu en détail.
- Le chiffre « 444 » : ni reproduit, ni réfuté. Les 12 sprites globaux à `0x100000` ne sont pas recensés.
- Aucune mesure de la DLL ni du moteur, aucune recette.

## 11. Reproduction
- Depuis le dossier : `python -B -I -X utf8 <script>`. Les scripts qui importent capstone ajoutent à `sys.path` `C:\Users\casad\AppData\Roaming\Python\Python314\site-packages`.
- Un fichier nommé `dis.py` masquait le module standard : renommé `mdis.py`.
- Scripts : `scan_field.py`, `scan_field2.py`, `scan_imm.py`, `mdis.py`, `findcode.py`, `q2_logs.py`, `q2_flags.py`, `q2_flagops.py`, `q3_corners.py`, `q3_pit.py`, `q8_p1.py`, `q8_p4.py`, `q8_p4b.py`, `q8_p5.py`, `q9_swaps.py`, `q9_chain.py`, `q1_flags_census.py`, `q1_rest_c23.py`, `q1_rest_134_174.py`, plus les scripts du T0 copiés inchangés.
- `work/` est une copie de `docs/plan-e20-annexe/e20-g1a/work/`.
- Sorties : `out/` (dont `q8_*.txt`, `q3_*.txt`, `q9_*.txt`, `q2_*.txt`, `scan*.txt`, `r_*.txt`), plus `q1_rest_c23.txt` (`{'inert': 1976}`) et `q1_rest_134_174.txt` (`{'inert': 57}`).