# Découverte E20.h4c1 (terre 1, objet 0x2B), lecture seule, HEAD `023b33ce`

Le Write de `report.md` et de `check.md` dans le dossier de sortie a été refusé par l'outil (« les sous-agents rendent leurs résultats en texte »). Le rapport complet est donc ce message ; `check.md` en est la section 7.

Légende : **FAIT** (vu dans le code, les données ou le binaire, avec fichier:ligne ou adresse), **INFÉRENCE** (déduit, de quoi dit), **INCONNU**.

- Binaire : `ALUN_CD.EXE` (France), SHA-1 `a705f8da…` vérifié.
- Dépôt : branche `chantier/restructuration-dll-r3`. `git diff --stat f81304b HEAD -- Alundra Alundra.Tests` est vide : le code lu est celui de `e20-h3/state-f81304b`. Rien n'a été écrit hors du dossier de sortie.
- Abréviations : `h4/` = `docs/plan-e20-annexe/e20-h4/` ; `h4b/` = `…/e20-h4b/` ; `plan:N` = `docs/plan-e20-heros.md:N`.
- Dossier de sortie : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4c1/` (appelé `OUT/`).

## 0. Verdict

1. **Le rapport h4 tient pour le comportement du binaire.** Le pilote, E3, la table, la garde et l'annulation neutre sont relus par moi au désassemblage (capstone). Les vecteurs de h4 rejoués avec une graine neuve donnent 0 écart.
2. **Les clés de monde de h4 ne sont pas utilisables telles quelles.**
   - Concernées : `trace_summaries.txt` et `targets_inv0/30.txt`.
   - Cause : le harnais de h4 (`spellworld.make`, `world.py`) n'écrit pas le mot d'index de case (`+0`) que le monde réel écrit à l'initialisation (`0x8003B2BC`, boucle de 64 cases). `InitializeEntity` relève le plus haut emplacement à `[case+0]` (`0x80039D24-0x80039D3C`).
   - Conséquence : sans index, une entité née dans une case jamais utilisée ne participe pas au tick de sa naissance. La DLL y participe (ADR-0065).
   - Effet mesuré : 14 rochers sur 32 (les cases 1 à 14, premières utilisations) ont leur impact et leur mort un tick trop tard dans les clés de h4. Le compte de touches d'une cible change (3 devient 4).
   - Les clés sont refaites (section 3) avec la machine `pworld` d'`e20-f4f6` : index écrits, E3 réel.
3. **Prérequis manquants au tableau du plan** (plan:8123 : « h4a, f6a »).
   - **f5 (D-F5-5)** : sans elle, un rocher fait un pas XY de trop au tick d'E3.
   - **f4 (D-F4-7, `NativeSpriteTypeFilterForTests`)** : pour allumer E3 sans allumer les D des Beannoïdes. Absent au HEAD.
   - Ils précèdent h dans l'ordre ferme (plan:5751), mais le tableau doit les nommer. Ajouter aussi h0a (tables), la fin de la restructuration et Q-R-7 (fichier neuf).
4. **La terre 1 crée des entités à direction non cardinale** : 28 des 32 directions, alors que plan:5771 dit O-E0A5-4 « sans objet ». La rangée d'animation de départ dépend de l'ancienne valeur de la case dans le binaire, mais vaut toujours 0 dans la DLL. L'effet est limité à la boîte d'impact 23×23×15, qui n'existe qu'en rangée 0. C'est un écart à documenter (Q-H4C1-3).
5. **L'inscription d'E3 (Q-H4-8) se règle sans registre cumulatif.** Une liste de types `{16}` dans un seul `Register`, que h4d2 étendra. Effet visible en plus : 4 rochers statiques de la carte 388 (Hauteur 20) tomberont puis disparaîtront comme dans l'original.
6. **La terre 1 n'a aucune brique de h4b** : ni requête de cibles, ni poussée, ni caméra, ni tremblement, ni tirage, ni effet de sortie. Seules exceptions : l'effet 6 de la mort en eau profonde et les étincelles des touches. C'est la tranche la plus simple de la famille.

## 1. Faits du binaire (relus par moi)

Listings : `h4/work/lst/drv_80035320_ann.txt`, `E3_8007eda0.txt`, `driver_8003634C.txt`. Mes relectures : `OUT/lst_init_80039D04.txt`, `OUT/lst_updanim_80038AB4.txt`.

**1.1 Appel et libération** (`0x8003634C`, FAIT)
- `[0x80127164] == 0` rend 1.
- Sinon `idx = item − 0x1F`. Si `idx ≥ 0x14`, le résultat par défaut est 1.
- Sinon saut par `0x800235F4[idx]` : `0x2B` donne `0x800363D4`, soit `jal 0x80035320` avec `a0 = item`.
- Après l'appel, `[0x8012715C]` est incrémenté si ≤ 0x7FFFFFFE (`0x80036484-0x8003649C`), donc après le pilote.
- Si le pilote rend non nul, `[0x80127164]` est remis à 0. Le résultat est rendu.
- Appelants : `0x80031CC4` (tick libre) et `0x80031C6C` (blessure).

**1.2 Pilote `0x80035320`** (FAIT, table lue à `0x8002343C`). Phase = `[0x80127160]`.
- **Phase 0** :
  - La garde `0x800351B8` passe d'abord ; si elle rend non nul, le pilote rend cette valeur.
  - Tant que `compteur < 20` : rien.
  - Sinon boucle depuis l'entrée 0. Pour chaque entrée dont `tick == compteur`, appel de `SpawnWarpEntity(a0 = héros, a1 = 0, a2 = 0x10, a3 = PosX héros ; pile : PosY héros, PosZ héros, (héros+0x8C + delta) & 31)`.
  - Résultat non nul : `ForceZ(+0xB8) = (s16 vitesseZ) << 8`. Résultat nul : écriture sautée.
  - La boucle continue tant que `compteur ≥ tick de l'entrée suivante`. À la sentinelle `0xFFFF` (`0x800234FC`), la phase passe à 1 : au compteur 95, tick de la dernière naissance.
  - Rend 0.
- **Phase 1** : garde, puis `compteur < 100` rend 0. Sinon `hero+0x88 = 0x34` (`[0x80127DB8]`), phase 2, rend 0.
- **Phase 2** (sans garde) : `hero+0x88 == 0x34` rend 0, sinon rend 1.
- **Table** : 32 entrées de 6 octets (u16 tick, s16 vitesse Z, s16 delta), deux par tick, ticks 20, 25 … 95.
  - Ticks 20-55 : vitesse Z 1024, donc `ForceZ = 0x40000` (4 px).
  - Ticks 60-95 : vitesse Z 1280, donc `0x50000` (5 px).
  - Deltas par tick, identiques dans les deux moitiés : (3,−6), (0,6), (3,2), (8,14), (3,10), (16,22), (3,18), (24,30).
- Le Z passé est le `PosZ` du héros sans décalage (la création ajoute le 1 brut). Le héros n'est pas déplacé.
- **Calendrier** (FAIT, oracle) : Rond au tick 1, premier appel du pilote au tick 2 (compteur 0). Naissances aux ticks 22, 27 … 97. Phase 1 au tick 97, `0x34` au tick 102, héros libre au 128, objet libéré au 129.

**1.3 Garde `0x800351B8`** (FAIT)
- `hero+0x18C == 4`, ou `(hero+0x88 − 0x32) ≥ 2` non signé : annulation `0x800350C0` puis rend 1.
- Sinon rend 0.
- Appelée aux phases 0 et 1 seulement.

**1.4 Annulation** (FAIT)
- `0x800350C0` : table `0x800233EC`, `0x2B` donne `0x800351A0` = `[0x80127164] = 0`. Elle est neutre.
- Interruption par l'animation `0x31` posée en tête du tick T :
  - La garde annule au tick T. Le compteur ne bouge plus et il n'y a plus de naissance.
  - Les rochers déjà nés vivent et meurent seuls.
  - Objet libéré à T exactement (clés `earth1_interrupt_{30,60,98,101}`).
- Nuance de `h4/check.md` §2.5 : en jeu, la blessure pose `0x31` après le dernier appel du pilote, donc l'annulation vient au tick suivant. La clé suit `interrupt_check.py` (poke en tête de tick).

**1.5 E3 `0x8007EDA0`** (FAIT)
- `TargetAnim(+0x88) == 1 && AnimEnd(+0xAC) == 1` : `DestroyEntity(self, −1)`.
- Sinon `+0x88 = 1` et `Flags = (Flags | 0x40) & ~0x30` (`addiu −0x31`). Idempotent, rejoué à chaque tick du statut 3.
- Vecteurs : `OUT/vec/vec_e3.json` (224 vecteurs, 25/25 instructions) ; 2 000 vecteurs de `h4_vec_handlers.py` rejoués, 0 écart.

**1.6 Le sprite 16** (FAIT ; `alundra-project/Data/sprite-records.json` clé `alundra_16`, `data-extracted/data/map_alundra.json`, `.anim2d` de `alundra-project/Entities/Magie de terre Niv.1/`)
- **En-tête** :
  - `MoreFlags 20`, `CanPickup 33`, `FlagsPortraitShadowType 19` donnent `Flags = 0x132114`.
  - Bits présents : `HitsClassA` (0x4), `DeactivateOnImpact` (0x10), `Gravity` (0x100), `NoObstacleSlide` (0x2000), ombre 3, `DestroyOnSlidingSlope` (0x100000).
  - Bits absents : `DeactivateOnHit` (0x20) et `Collidable`.
  - Programmes : A 0, C 0, D 0, **E 3**.
  - `BreakEffect 0` et `Contents 0` : `DestroyEntity(e, −1)` ne crée aucun effet et le butin ne tire pas.
  - Corps (−10, −7, 0), 21×15×16.
- **Animations** (délai `130 & 0x7F` = 2 ticks par image) :

  | Animation | Images | Vitesse | Particularités | Fin |
  |---|---|---|---|---|
  | 0 | 12 (24 ticks) | 512 | accélération 64, `AnimFlags` 0x40 | `Chain` vers 2 |
  | 1 | 6 (12 ticks) | 0 | `Sfx 46` | `Hold` |
  | 2 | 4 | 512 | | boucle |

- **Fiche d'équilibrage 16** (`BALANCE.BIN.json`) : niveau 255, PV 0, toutes réponses `0x80`, une attaque de type 8, attribut 8, puissance 32, sans puissance d'équipement.
- **Boîtes** :
  - 42×28×11 (décalage −21, −14, 0) dans toutes les animations et directions.
  - **La boîte 23×23×15 (−12, −12, 0) n'existe que dans l'animation 1, direction 0 (« down »), à l'image 6** (0,2 s, soit le tick 10 de l'animation).
  - Les directions 1 à 3 gardent 42×28×11 jusqu'au bout.
  - Le rapport h4 §5 (« + 23×23×15 à l'impact ») omet cette restriction.
- **Placements statiques** : 4 placements du sprite 16 sur la carte 388, Hauteur 20, programmes 0 (`map_388.json`). Les 15 placements de page de carte d'E3 (cartes 382, 386, 388, 432, 451) sont dans `h4b/state-c221a76/check/check.md` §1.1.

**1.7 Vie d'un rocher** (FAIT, oracle ; clé refaite `OUT/keys/earth1_flat_east.json`, table `OUT/keys_earth1_flat_east_rocks.txt`)
- **Naissance et vol** :
  - Position du héros, Z brut 2 (= 1 dans la DLL), `ForceZ` 0x40000 ou 0x50000.
  - Le rocher avance et monte dès son tick de naissance.
  - Pas XY par tick = `OffsetXList/OffsetYList[direction] × 512`. Exemple direction 27 : 327 168 et 145 408, soit 4,99 et 2,22 px.
  - Z : `F −= 0x8000` avant le pas (gravité 128 de la carte 14). Sommet à 14 px (groupe 1) ou 22,5 px (groupe 2).
  - Atterrissage strict (`Z + F < sol`) au 16ᵉ pas (groupe 1) ou au 20ᵉ (groupe 2).
- **Atterrissage au tick L** :
  - Le dernier pas XY a lieu à L.
  - À L+1 : événements, `DeactivateOnImpact`, statut 3, E3 passe à l'animation 1 avec le son 46. La vitesse vaut 0, donc aucun pas XY.
  - Fin d'animation (`+0xAC = 1`) à L+13, mort (statut 4) à L+14, case libre au tick suivant.
- **Totaux** : 14 rochers vivants au plus ; 32 sons 46 aux ticks d'E3 ; aucun tirage ni effet sur terrain plat. Distances d'atterrissage jusqu'à ±120 px par composante (table dans l'annexe).
- **Bord et mur** :
  - Le bord ouest (x = 10) arrête en l'air 3 rochers sur 32 (directions 6, 8, 10). E3 vient par `ForceAdjusted`, un tick avant l'atterrissage normal, et le rocher tombe droit.
  - Mur en colonne 9 : 1 rocher stoppé en l'air (E3 au tick 86 au lieu de 87).
- **Eau profonde** (case `0x08` sous tout le pied, colonnes 7 à 9) :
  - 14 rochers sur 32 y atterrissent. `Slope_18c = 4` à L.
  - Statut 4 à L+1 par le choix du programme (`DestroyEntity(e, 6)`, effet attaché 6), sans E3 ni son 46 (18 sons).
- **Touches** (un Beannoïde inerte de la carte 14) :
  - 3 étincelles et 9 tirages par touche, `+0x1D0 = 25` sur la cible.
  - Le rocher traverse et continue.
  - Exemples : cible (132,104) touchée aux ticks 22, 52, 82 ; (84,104) à 22, 47, 72, 97 ; (108,136) à 39 ; (180,104) à 39 et 84.

**1.8 Rangée d'animation** (FAIT, `0x80038AB4`)
- Nouvelle rangée `+0x98` = `table[0x800237F4 + (ancien +0x98)·32 + ((dir + 2) & 0x1C)]`.
- `InitializeEntity` n'écrit pas `+0x98`, seulement `+0x88`, `+0x8C`, `+0x94` (`0x80039DB8-0x80039DC4`). L'ancienne valeur est celle de la case.
- Les rangées 0 et 1 partagent une ligne, les rangées 2 et 3 une autre. Les colonnes 1, 3, 5, 7 donnent 0 ou 1 avec une case de classe A, 2 ou 3 avec une case de classe B. Cela concerne 16 rochers sur 32.
- La DLL part toujours de 0 : `AnimationDirection` n'est assigné qu'en `FrameSyncPasses.cs:152` et au clonage. Elle applique la même table (`FrameSyncPasses.cs:502-507`).
- INFÉRENCE : visible seulement par la boîte des 2 derniers ticks de l'impact. Les images sont identiques dans les 4 directions.

## 2. Écarts à corriger avant d'écrire la tranche

| # | Point | Où | Correction |
|---|---|---|---|
| C1 | Harnais h4 sans index de case | `h4/work/oracle/spellworld.py` `make`, `world.py` `build_world`. `e20-f4f6/bin/src/pworld.py:98-99` l'écrit | Clés décalées d'un tick pour 14 rochers sur 32. Refaites (section 3) |
| C2 | « 42×28×11 + 23×23×15 à l'impact » | `h4/report.md` §5 | 23×23×15 : animation 1, direction 0, image 6 seulement |
| C3 | O-E0A5-4 « sans objet » | plan:5771 | Pas pour la terre 1 : écart à épingler |
| C4 | Prérequis de h4c1 | plan:8123 | Ajouter f5, f4, h0a, restructuration et Q-R-7 |
| C5 | `interrupt_check.py` n'a pas la 0x2B | `h4/work/results/interrupt_check.txt` | Cas ajouté (§1.4) |
| C6 | E3 « hors rochers » | `h4/report.md` §4 | E3 sert aussi 23, 150-153 et 15 types de carte (`h4b/…/check.md` §1.1) : Q-H4C1-1 |
| C7 | Pas par image du générateur | harnais (`0x8002BD0C`) | Non modélisé. Sans effet ici : aucun tirage hors touches |

## 3. Clés d'oracle

### 3.1 Celles de h4

| Clé | Contenu | Verdict |
|---|---|---|
| `trace_cast.py 0x2B` + 9 lignes de `trace_summaries.txt` | héros (anim, objet, phase, compteur), naissances et vies | Rejouée : **9 lignes identiques** (`OUT/replay_trace_cast_2b.txt`). Décalée d'un tick pour 14 rochers : à refaire |
| `trace_targets.py` → `targets_inv0.txt` (6 cibles de sprite 25) | touches 4,2,3,3,3,3 aux ticks 25,33,36,28,40,32 | Cibles et résolveur de dégâts ≠ Beannoïde de la DLL. Avec l'index : ticks 24,32,35,27,39,31 et touches **4,2,3,3,3,4** : à refaire (`OUT/h4copy/oracle/c1_h4_targets_cmp.py`) |
| `interrupt_check.py` | 0x2C, 0x2D, 0x2F, 0x30, 0x32 | Pas de 0x2B : cas ajouté |
| `h4_vec_drivers.py`, `h4_vec_handlers.py` | pilote et E3, vecteurs aléatoires non versés | Rejoués (graine 20261018, 3 000 et 2 000 vecteurs) : 0 écart. Fichiers déterministes produits (3.3) |

- Aucune clé de h4 ne convient telle quelle au banc « champ plat synthétique, héros (100,100) à l'est, graine `0x12345678`, aucune autre entité ». Seule la partie héros est identique dans les deux harnais.
- Graine et position viennent de `oracle.setup` et `pworld.setup`.
- Le générateur : la terre 1 ne tire pas, donc la règle `_pf` d'f4 (plan:5726) ne s'applique pas. Pour les touches, le nombre de pas de la DLL pendant le tick doit égaler `draws + 1` (plan:5735).
- D-H-7 : sans objet, la terre 1 n'attaque que la classe A.
- Toutes les clés lancent le vrai `MovePlayer`, le vrai pilote et le vrai `SpawnWarpEntity` depuis le tick 1.

### 3.2 Clés refaites (`OUT/keys/`, 19 JSON)

Le manifeste est identique sur deux exécutions complètes.

- **Machine** : `e20-f4f6/bin/src` copié dans `OUT/fcopy/`. Index de case écrits, sprite 16 installé, E3 réel avec C60/C61/C94/E1/E9/E10/E17 réels. `SpawnWarpEntity`, passe de coups et pilote réels ; effets et sons journalisés.
- **Héros** : objet 0x2B possédé et objet du Rond, PM 4/4, Rond au tick 1.
- **Contenu d'un tick** : héros, objet (en cours, phase, compteur), rochers (statut, X, Y, Z, forces, animations, direction, rangée, `+0xAC`, drapeaux, `+0x13C`, pente, boîte, touche, `+0x1D0`), sons non nuls, effets, tirages.

| Fichier | Scénario | Contenu clé | Banc DLL |
|---|---|---|---|
| `earth1_flat_east` | héros (100,100) à l'est, champ plat | 32 rochers, 32 sons 46, 0 tirage, 0 effet, objet libéré au 129 | oui |
| `earth1_flat_{south,west,north}` | directions 0, 8, 16 | autres orientations | oui, relire les bords |
| `earth1_flat_east_x200` | héros (200,100) | aucun rocher au bord ouest | oui |
| `earth1_wall_col9` | mur colonne 9 | 1 rocher stoppé en l'air (E3 au 86) | oui, mur posé comme `AlundraMapEdgeWorldTests.cs:43-50` |
| `earth1_deepwater_cols7_9` | `gp 0x08` colonnes 7-9 | 14 morts à L+1 avec effet 6, 18 sons 46 | oui si l'aide pose la propriété de sol (f6a (s1) la demande déjà) |
| `earth1_vs_beannoid_{132_104,108_136,180_104,108_56,60_104,132_136,204_104,84_104}` | un Beannoïde inerte à ces pixels | tick de touche, tireur, 9 tirages | `HitArena` + filtre {16} (D-F4-7). La cible reste inerte : un Beannoïde natif (PV 9, dégât 32) mourrait en un coup |
| `earth1_interrupt_{30,60,98,101}` | `0x31` en tête du tick T | objet libéré à T, plus de naissance, rochers finissent seuls | oui |

### 3.3 Vecteurs (`OUT/vec/`, sur le vrai code)

- `vec_driver_2B.json` : 689 entrées.
  - Entrée : phase, compteur, animation, direction, X/Y/Z du héros, pente, « la naissance rend une entité ».
  - Sortie : retour, phase, compteur, objet, animation, appels de naissance (parent, table, type, X, Y, Z, direction), `ForceZ` écrits.
  - Couverture du pilote 104/108 : les 4 mots manquants sont les phases négatives ou > 2. Garde 19/19.
- `vec_e3.json` : 224 entrées (animation cible 0/1/2/5, fin d'animation 0/1/2/−1, 14 drapeaux dont `0x132114`).
- Limite : en phase 0, un compteur ≥ 0xFFFF fait lire la table au-delà de la sentinelle (boucle sans borne). Inatteignable, non épinglé.

## 4. Atterrissage dans la DLL (code au HEAD)

**Existe déjà**
- **Naissance par indice** :
  - `WorldProxy.SpawnEntityBySpriteIndex` `Alundra/Maps/WorldProxy.cs:3488`. Z+1 en `:3549`, `InitializeBalance` `:3554`, tirage de butin `:3566`. Marqueurs `JoinsBirthTick` et `HasLogicalZ` posés en dernier `:3572-3574`. Peut rendre `null`.
  - Interface : `Alundra/Maps/IEntityWorldContext.cs:81` (défaut `null`).
  - Modèle d'appel depuis le héros : `Alundra/Hero/PlayerManager.cs:1216-1218`.
- **Direction et mouvement** :
  - `EntitySpawnFactory.cs:834-836` pose `TargetDirection = direction` sans normalisation : une direction 0..31 passe.
  - Mouvement : `ScriptedMotion.cs:626-634` (`OffsetXList/OffsetYList[TargetDirection] × vitesse`, 32 entrées, `Entities/AnimationTables.cs:71,82`).
  - Clé de vitesse au HEAD : `ScriptedMotion.cs:171-176` (`JoinsBirthTick && CurrentAnimationId == ~0u ? Target : Current`).
- **Z logique** : `RunLogicalZPreXyStep` `ScriptedMotion.cs:442-540` et `RunLogicalZPostXyStep` `:542`. Bord de carte `ClampStepToMapEdge` `:741`. Gravité de carte posée à la création `EntitySpawnFactory.cs:707`.
- **Statuts** :
  - `EntityScriptProxy.PickEventTrigger` `…/EntityScriptProxy.cs:1692-1780`.
  - `DeactivateOnImpact` vers statut 3 et programme E `:1716-1726`.
  - `DestroyOnSlidingSlope` → `DestroyEntity(this, 6)` `:1708`, seulement en statut 2 et seulement si `Slope_18c == 4`. Cette valeur n'est écrite aujourd'hui que par le héros : c'est f6a.
  - En statut 3, E se rejoue à chaque tick `:1758-1760`.
- **Fin d'animation** : `Hold` pose `ForceResetAnimationFlag = 1` (`EntitySpawnFactory.cs:308-325`), remis à 0 à toute bascule (`FrameSyncPasses.cs:164-166`). C'est le `+0xAC` d'E3.
- **Destruction** : `IScriptHost.DestroyEntity(entity, effectId)` vers `WorldProxy.cs:3301` (`FlagToDestroy`). `−1` lit `BreakEffect`, qui vaut 0 pour le sprite 16 : aucun effet. Forme à un argument `:3329`. Recyclage `:3265`.
- **Registre** : `NativeSpriteRegistry.cs:36-43` (7 clés) ; `Register` `:54-65` (doublon : exception `:62`) ; `RunSpriteEvent` `Events/EventProgramRunner.cs:264-324`. Le registre n'est consulté que si le programme scripté vaut 0. E0, E1 et E10 sont en ligne, pas E3.
- **Passe de coups** : `Combat/HitPass.cs`, `NativeEffects.CreateHitSparks` (3 étincelles, 9 tirages). Dégâts `Combat/Damage.cs:24`, appliqués par le D de la cible : seulement le Beannoïde 335 et le Muruta 347 en ont un.
- **Pas de caméra, de tremblement, de requête ni de poussée** pour la terre 1.

**À ajouter** (les noms sont une INFÉRENCE à contrôler par `tools/dll-structure/check_names.py`, Q-R-7)
- `Alundra/Ai/Items/EarthSpellAi.cs`, sur le modèle de `WaveSwordAi.cs:14-41` :
  - La table de 32 triplets, comparée mot à mot à l'exécutable en test (modèle `AlundraHeroRunTablesTests.cs:42`).
  - `Driver(player)`, avec la garde partagée avec h4a.
  - `ImpactDeactivate(entity)` : E3, réutilisable par h4d2.
  - `SpriteTypes = {16}` et `Register(registry)` = `Register(ProgramEDeactivate, 3, SpriteTypes, …)`.
- **Phase B, trois entrées 0x2B dans les tables de h0a.** Elles n'existent pas encore : `PlayerManager.cs:932` (`TryUseItem`) est un bouchon, le pilote est vide `:1507-1509`, l'annulation `:740-742` est vide.
  - Départ : la fonction de départ des tomes de h4a (aujourd'hui seulement par `ItemStartForTests`, plan:8147).
  - Pilote : `EarthSpellAi.Driver`.
  - Annulation : un no-op nommé (neutre).
  - Plus l'appel `EarthSpellAi.Register` dans `CreateForProduction`.
  - Les lignes exactes des tables de h0a sont à relire à l'exécution.
- **Naissance** : `world.SpawnEntityBySpriteIndex(player, false, 16, player.PosX, player.PosY, player.PosZ, (int)((player.TargetDirection + delta) & 31))`.
  - Retour nul : pas d'écriture de `ForceZ`.
  - Sinon `spawned.ForceZ = vitesseZ << 8` (`EntityScriptProxy.cs:338`).
  - Pas de `ResyncControllerFromFlags` nécessaire pour E3 (INFÉRENCE : il ne lit que `Gravity` et `ClassA/B`, `:670-686`).

**Tests qui bougent**
- **Deux épingles du registre** : `AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`, titre de `:50` « Seven ») et `AlundraWaveSwordLifeTests.cs:181-183` (`3 + 3 + 1`). +1 clé `(E, 3)`, réécrites par dérivation (plan:6630-6636).
- **Ne bougent pas** :
  - S1 et S5 (`AlundraWeaponSpawnSeamTests.cs:83-107`, `:238-258`) : la terre 1 n'ajoute rien à `ProductionWiredWeaponTypes` (`PlayerManager.cs:1226`).
  - Les tests de caméra : aucun lien.
  - `AlundraLogicalZWorldTests` Z4 et Z7 (bombe, type 2).
  - Les tests qui font naître tous les préfabs sans tick (`AlundraBirthBoxTests.cs:131`, `AlundraSpawnBySpriteIndex*Tests`).
  - Aucun test ne charge la carte 388.
- **Épingles de sauvegardes de test** (`AlundraTestSavesTests.cs:88-91` et `:429-433`) : ne bougent que si la recette ajoute un préréglage ou un objet du Rond (Q-H4C1-4).
- La liste prédite est recomptée au HEAD de départ (T0).

**Bancs**
- `ArcRun` (`AlundraArcSupport.cs:94`) : `Give` `:151`, `Equip` `:161`, `SetMp` `:164`, `SetItem` `:171`, `Press` `:454`.
- `HitArena` (`AlundraHitArena.cs:27`, natifs coupés `:70`).
- Champ synthétique `FlatCells.Create` + réflexion (`AlundraMapEdgeWorldTests.cs:43-50`, `AlundraJumpTestSupport.cs:451-515`).
- Retrait des entités de la carte (`AlundraLootRollWorldTests.cs:74-78`, plan:5778-5786).

## 5. Tâches proposées

**But.** La terre 1 jette 32 rochers en 76 ticks, deux par tick de 22 à 97. Ils montent, retombent, s'écrasent avec le son 46, traversent les ennemis en les blessant (type 8, puissance 32) et disparaissent. L'objet est libéré au tick 129. Une interruption n'arrête que les naissances. L'eau profonde détruit les rochers qui y tombent.

**Périmètre.**
- Dans : `Alundra/Ai/Items/EarthSpellAi.cs` (neuf), `NativeSpriteRegistry.cs`, les trois entrées 0x2B de h0a, `Alundra.Tests/`, l'annexe `docs/plan-e20-annexe/e20-h4c1/`, le plan, l'index des ADR, un ADR.
- Hors périmètre :
  - La fonction de départ, les PM, la piste XA, les états `0x32`-`0x34`, la garde de pente (h4a).
  - Les autres tomes.
  - E3 pour le type 23 et les types de carte (h4d2, E14.b0).
  - Le butin.

**Phases.**
- **Phase A** (inerte en production) : pilote, table, E3, `Register`.
  - Tests unitaires sur un registre neuf.
  - Tests de monde par les coutures D-H-4 (`ItemStartForTests` = départ de h4a, `ItemDriverForTests` et `ItemCancelForTests` posés par le test).
  - E3 inscrit seulement si absent, par le `IDisposable` de `Register` (plan:5757).
- **Phase B** : les trois entrées et l'appel d'inscription. Changement visible : le sort.

- ⏳ **T0 — Annexe, oracle, liste prédite** (aucun code)
  - (a) Rejouer depuis une copie `trace_cast.py 0x2b` (9 lignes identiques), `h4_vec_drivers.py` et `h4_vec_handlers.py` (0 écart).
  - (b) Verser dans `docs/plan-e20-annexe/e20-h4c1/` les clés de 3.2 et les vecteurs de 3.3, avec leurs scripts (`c1_pworld.py`, `c1_make_keys.py`, `c1_vec.py`, `c1_cast.py`), le manifeste (deux exécutions identiques) et un README qui explique l'écart C1.
  - (c) Sondes de la DLL, prédiction écrite avant :
    - P1 : pas XY au tick d'E3 d'un rocher né par l'interface. Attendu : aucun (D-F5-5 faite).
    - P2 : rangée d'animation de rochers nés en case recyclée. Attendu : 0 ou 1.
    - P3 : boîte 23×23×15 présente en rangée 0 aux ticks 10-11 de l'animation 1, absente ailleurs.
    - P4 : `Slope_18c` d'un rocher posé sur une case `0x08`.
    - P5 : le bord ouest.
    - P6 : `AnimationSoundOf(1) == 46` joué au tick d'E3.
    - Un écart d'une sonde est un arrêt.
  - (d) Liste prédite recomptée (section 4).
- ⏳ **T1 — Tests puis code (phase A).** Chaque cas « sans effet » est accolé à un positif.
  - (t1) Table mot à mot contre l'exécutable : 32 triplets et la sentinelle `0xFFFF`.
  - (d1) Pilote contre `vec_driver_2B.json` (689 vecteurs, un seul test).
  - (d2) La naissance rend `null` : aucune écriture, aucune exception.
  - (d3) Garde : pente 4 ou animation hors 0x32/0x33 donne annulation neutre, libération et retour 1 ; 0x33 ne fait rien.
  - (d4) Phase 2 : rend 0 tant que l'animation vaut 0x34.
  - (e1) E3 contre `vec_e3.json` (224 vecteurs, un test).
  - (e2) Inscription : `(E, 3)` pour le type 16 seul (pas 17 ni 23, accolé), sur un registre neuf.
  - (c1) L'annulation neutre laisse les rochers et libère l'objet.
  - (w1) Le sort entier, clé `earth1_flat_east`, à un tick par image :
    - naissances, positions, Z, forces, statut, animation, drapeaux, fin d'animation ;
    - sons 46 aux ticks d'E3, objet libéré au 129 ;
    - aucun pas XY au tick d'E3 ;
    - aucun tirage ni effet.
  - (w2) Les trois autres orientations et `x200`.
  - (w3) Mur et bord ouest : E3 en l'air.
  - (w4) Eau profonde : mort à L+1 avec l'effet 6, sans son 46.
  - (w5) Touches contre le Beannoïde inerte : tick, tireur, 9 tirages, `+0x1D0 = 25`, le rocher continue.
  - (w6) Interruptions 30, 60, 98, 101.
  - (w7) Écarts documentés épinglés par un test nommé (rangée de départ, Q-H4C1-3).
- ⏳ **T2 — Phase B et tests de production**, rouges avant :
  - (p1) Rond avec le tome 43 (objets 43 et 44 possédés : le niveau 2 passe d'abord, plan:8174), PM 4/4, héros (100,100) à l'est. Le type 16 naît au tick 22 et la clé de (w1) tient par le chemin de production.
  - (p2) Table de cases pleine : aucune entité, aucune exception.
  - (p3) Les 4 rochers de la carte 388 tombent et meurent (Q-H4C1-2).
  - Les deux épingles du registre sont réécrites par dérivation.
- ⏳ **T3 — ADR, plan, deux verifiers frais.** Mutants, au moins 18, tous tués :
  - vitesse Z du groupe 2 à 1024 ; frontière 60 passée à 65 ;
  - une seule naissance par tick ; ticks 20 passé à 25 ou 95 passé à 90 ;
  - delta de signe contraire ou permuté ; base `TargetDirection` remplacée par `BoxDirection` ; masque `& 31` retiré ;
  - `<< 8` passé à `<< 7` ; parent nul ; écriture de `ForceZ` sur retour nul ;
  - seuil de phase 99 ou 101 ; garde qui accepte `0x34` ou ignore la pente ;
  - phase 2 qui ne finit pas ou finit tout de suite ; compteur incrémenté avant le pilote ;
  - E3 sans `|= 0x40`, avec `~0x10`, sans le test de fin d'animation, ou avec l'effet 6 ;
  - E3 inscrit aussi pour 17 ;
  - clé de vitesse revenue à `CurrentAnimationId` (tué par (w1) au tick d'E3) ;
  - annulation non neutre (tue les rochers).

**Acceptation.**
- *Oracle* : rejeux de T0 (a) identiques ; manifeste identique sur deux exécutions.
- *DLL* :
  - (t1), (d1)-(d4), (e1), (e2), (c1), (w1)-(w7) verts en phase A ; (p1)-(p3) rouges avant la phase B.
  - Suite en Release (copie neutralisée) et en Debug, zéro échec.
  - Tests déplacés ⊂ liste prédite (les deux épingles).
  - `cmp` de la DLL ; traces inchangées ; moteur, convertisseur et données sans diff.
- *Preuve de retrait* :
  - Retirer l'appel d'inscription rend rouges exactement (p3) et les épingles.
  - Retirer le corps d'E3 rend rouges (e1), (w1), (w2), (w5), (w6), (p1).
  - Retirer l'entrée du pilote rend rouges (p1) et (p2).
  - L'annulation neutre est un témoin (équivalent au défaut de D-H-5) : (c1) est vert avant et après.
- *Recette de l'auteur* (non bloquante) : un préréglage avec le tome 43 comme objet du Rond, carte 14. Rond, 32 rochers autour du héros pendant environ 1,5 s, son d'impact, héros libre au bout d'environ 2,5 s ; un Beannoïde touché meurt.

**Arrêts.**
- Un test hors de la liste prédite bouge.
- Un tick de (w1) diffère de la clé (aucune tolérance).
- Une sonde P1 à P6 contredit son attendu.
- Un besoin de toucher au moteur.
- Un nom refusé par `check_names.py`.
- Un octet de table différent de l'exécutable.
- f4, f5, f6a ou h4a non faites.

**Budget.** Un `executor` par phase, deux tentatives ; `plan-verifier` avant, deux verifiers frais après.
**Retour arrière.** Phase B : retirer les trois entrées et l'appel d'inscription. La phase A reste inerte.

## 6. Questions pour l'auteur (termes concrets)

- **Q-H4C1-1 (Q-H4-8)** : E3 est le même code pour 22 sprites.
  - (A, défaut) On l'inscrit pour les seuls rochers (type 16). La boule du feu 1 (h4d2) et les ennemis (E14.b0) allongeront la même liste. Coût : h4d2 réédite la liste.
  - (B) On l'inscrit maintenant pour tous : 16, 23, 150-153 et les 15 sprites de carte des cartes 382, 386, 388, 432, 451. Ces sprites se comportent comme l'original dès qu'on charge ces cartes. Coût : des tests de ces cartes et un registre à fusionner par type (E14.b0) avant d'être sûrs.
- **Q-H4C1-2** : avec (A), les 4 rochers posés dans la carte 388 (hauteur 20, probablement l'arène de Zorgia et hors de l'histoire actuelle : INFÉRENCE) tombent puis disparaissent à l'arrivée, comme dans l'original, au lieu de rester figés. Accepter (défaut), ou les laisser inertes au prix d'un test et d'une exception propres à la 388 ?
- **Q-H4C1-3** (O-E0A5-4) : un rocher né dans une case recyclée prend, dans l'original, une orientation d'image qui dépend de l'objet qui occupait la case. La DLL part toujours de « bas ». Visible seulement par les 2 derniers ticks de l'impact : zone de touche 23×23 au lieu de 42×28, et seulement en orientation « bas ». Documenter et épingler par un test (défaut), ou porter une mémoire de la dernière orientation par case (moteur, 64 entiers) ?
- **Q-H4C1-4** : aucune sauvegarde de test ne pose l'objet du Rond.
  - (A, défaut) Un nouveau préréglage « tome de terre, carte 14 » : déplace l'épingle de la liste des préréglages (`AlundraTestSavesTests.cs:88-91`).
  - (B) Donner l'objet 43 à un préréglage existant : déplace `:429-433`.
  - (C) Poser l'objet par `SetItem` depuis un lanceur de scratch, sans toucher au dépôt.
- **Q-H4-7 (rappel)** : quels coffres de tomes sont sur la chaîne actuelle (tome de terre niveau 1 en coffre à la carte 25, niveau 2 aux cartes 17 et 439) ? INCONNU. La tranche n'en dépend pas.

## 7. Vérifications effectuées (`check.md`)

- **Relu au désassemblage (capstone, copies du harnais)** :
  - Fonctions : `0x8003634C`, `0x800363D4`, `0x80035320`, `0x800351B8`, `0x800350C0`, `0x800351A0`, `0x8007EDA0`.
  - `0x80039D04-0x80039F58` : index de case, écritures de `+0x88`, `+0x8C`, `+0x94`.
  - `0x80038AB4` : rangée d'animation.
  - `0x8003B24C` : l'index de case écrit en `0x8003B2BC`.
  - Tables lues dans l'exécutable : `0x8002343C`, `0x800235F4`, `0x800233EC`, `0x8002339C`.
- **Rejoué** :
  - `trace_cast.py 0x2b` : identique aux 9 lignes de `trace_summaries.txt`.
  - Vecteurs h4 (pilote 0x2B, 3 000 vecteurs ; E3, 2 000) : 0 écart.
  - 19 clés refaites : deux exécutions complètes, manifeste identique.
  - Comparaison harnais h4 contre harnais à index : 14 rochers sur 32 décalés (`OUT/cmp_pworld.py` et `OUT/h4copy/oracle/c1_cast.py`).
  - Équivalence `pworld` contre `spellworld`+index : 32 rochers sur 32 identiques, sur le statut, X, Y, Z, forces, animation, rangée et `+0xAC`.
  - Dépôt non modifié (`git status` inchangé).
- **Non fait ou INCONNU** :
  - Aucune exécution de la DLL (lecture seule, ni build ni test).
  - Comportement à 2 et 3 ticks par image : l'écart d'ADR-0066 s'applique, le son et la boîte arrivent en fin d'image.
  - Sons des effets (effet 6).
  - Disponibilité des tomes en jeu.

### Fichiers (tous sous `OUT/`)

- **Clés et vecteurs** :
  - `keys/` (19 JSON et `manifest.txt`) ; `keys_earth1_flat_east_rocks.txt` ; `manifest_run1.txt`.
  - `vec/vec_driver_2B.json` ; `vec/vec_e3.json`.
- **Sorties brutes** : `c1_idx.json`, `c1_noidx.json`, `c1_pworld.json`, `c1_targets_pworld.json`, `replay_trace_cast_2b.txt`.
- **Listings du binaire relus** : `lst_init_80039D04.txt`, `lst_updanim_80038AB4.txt`.
- **Scripts et copies de harnais** :
  - `h4copy/` : copie du harnais h4. Mes scripts : `oracle/c1_cast.py`, `c1_probe_passes.py`, `c1_vec.py`, `c1_h4_targets_cmp.py`, `dasm_tool.py`.
  - `fcopy/` : copie du harnais `e20-f4f6`. Mes scripts : `c1_pworld.py`, `c1_make_keys.py`, `c1_targets.py`, `c1_interrupt.py`, `c1_wall.py`.
  - `rocks_table.py`, `cmp_pworld.py`.
- **Rejeu** : depuis une copie, `python -B`. `c1_make_keys.py OUTDIR` dure environ 4 minutes.