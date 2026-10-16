# Rapport e20e — E20.e, l'herbe coupée (`CheckAndTriggerTileEffect`, `0x8003A7B0`)

Je n'ai pas pu écrire `report.md` : le harnais a refusé l'écriture d'un fichier de rapport. Ce texte est donc le rapport complet. Les scripts et les listings sont dans `$W` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/e20e/`.

Lecture seule respectée : rien n'est écrit dans le dépôt ni dans le binaire. Le `git status` montre `CasaEngineMonogame` modifié, et dans `alundra-datas-analyser` `MGUI` modifié plus `AlundraTools/log.txt` non suivi. Ce n'est pas de mon fait.

Légende :
- **FAIT** : lu dans `ALUN_CD.EXE` (France, capstone MIPS32 LE) ou mesuré dans les données.
- **INFÉRENCE** : déduit, à confirmer.
- **INCONNU** : non établi.

Les `fichier:ligne` de la DLL (`Alundra/Scripts/`) et du plan sont au HEAD `9502f75`.

---

## 1. Adresse et appelants

- **FAIT** — `0x8003A7B0` est bien CATE : 149 instructions, prologue `addiu sp,-0x60`, dernier `jr ra` à `0x8003A9D8`, fin `0x8003A9E0` (`$W/cate.txt`).
- **FAIT** — quatre appelants directs, pas d'autre. J'ai scanné tout le texte pour les `jal`, les `j` de queue et le mot `0x8003A7B0` dans l'image (`$W/callers.py`).

| Appelant | Fonction | Contexte (FAIT) |
|---|---|---|
| `0x8002FDC0` | `CreatePlayerAnimationEffects` `0x8002FB14`, cas `0x8002FDBC` | `a0 = 0x80127D30` (le héros). Premier aiguillage sur `état-4` (`+0x88`, table `0x800228AC`) : 15 états y mènent. Ce sont `0x04, 0x10, 0x12, 0x14, 0x16, 0x18, 0x19, 0x3F, 0x40, 0x41, 0x42, 0x44, 0x46, 0x49, 0x4B`. Ni `0x43`, ni l'arc, ni les baguettes. Atteint dans les deux modes (le test du mode vient après), à chaque tick de ces états, sans barrière « une fois par coup ». |
| `0x8007A960` | C61 `0x8007A958` (vague de l'épée, sprites 27 à 29) | Enveloppe de 8 mots : `CATE(a0)` et rien d'autre (`$W/c61.txt`). |
| `0x8007C0E4` | C94 `0x8007C0D8` (flèche chargée, sprite 7) | `CATE(a0)` en tête. Puis, si `(+0x1C & 3) == 0`, `CreateEffectEntity(0, 0x19, +0x98, +0x114, +0x118, +0x11C+0x80000)` avec `ForceX/Y = -(+0xC4/+0xC8) >> 1` (`$W/c94.txt`). |
| `0x8007F2D4` | E12 `0x8007F27C` (bombe, sprite 2) | Branche « sinon » du test `(+0x88==1 && +0xAC==1) \|\| (+0x184 & 4)`, dont la branche « alors » fait `DestroyEntity(e,-1)`. Puis `+0x88=1`, `+0x6C \|= 0x40`, `&= ~0x30` (`$W/e12.txt`). |

---

## 2. Pseudo-code fidèle du binaire

Champs de l'entité `e` (`a0`) :
- `+0x1D4` : pointeur de boîte de la frame (nul = pas de boîte).
- `+0x1FC/+0x200/+0x204` : boîte de coup en monde, X, Y, Z en 16.16. Elle est écrite par `0x80038064` à la fin du pas de chaque entité (appel `0x800384BC`) et à la création (`0x80039F30`). C'est `Pos + décalage`, seulement si `+0x1D4 != 0`.
- `+0x214/+0x218/+0x21C` : largeur, profondeur, hauteur, `(n<<16)-1`.

Globaux : `0x80127168` table `s16[1248]` ; `[0x800DD86C]` pointeur de carte ; `[0x800E4334]` pointeur de l'en-tête de carte.

```
void CheckAndTriggerTileEffect(Entity* e)                         // 0x8003A7B0
{
  if (e->+0x1D4 == 0) return;                                     // 7E0-7E8
  bx=e->+0x1FC; by=e->+0x200; bz=e->+0x204; bw=e->+0x214; bd=e->+0x218; bh=e->+0x21C;
  colA = T[(s16)(bx >> 16)];       // lh 0x1FE(e) : demi-mot haut signé 16 bits   7F0-80C
  colB = T[(bx + bw) >> 16];       // indice 32 bits, sans borne                  810-838
  rowA = by >> 20;  rowB = (by + bd) >> 20;                       // 83C-868
  for (col,row) in [(colA,rowA),(colB,rowA),(colA,rowB),(colB,rowB)] {   // 874-9A8
     c = clamp(col,0,0x33);  r = clamp(row,0,0x3B);               // 874-8C4
     cell = [0x800DD86C] + 0x604 + r*0x1A0 + c*8;                 // 8C8-8E8 grille FIXE 52 colonnes x 8 octets
     flags = u16[cell];                                           // 8EC  octet0 = Walkability, octet1 = GroundProperty
     if (!(flags & 2)) continue;                                  // 8F4-8F8 SEUL bit testé
     z0 = u8[cell+3] << 20;  zc = z0 + 0x80000;                   // 900-90C hauteur (x16 px) + 8 px
     if (zc < bz) continue;  if (bz + bh < zc) continue;          // 910-920, slt signés
     u16[cell] = flags & 0xFFFD;  u16[cell+6] = 0xFFFF;           // 924-938 efface le bit 1 ; WallTilesOffset = -1
     X = col*0x180000 + 0xC0000;  Y = row*0x100000 + 0x80000;    // 93C-96C col,row NON bornés ; centre de case 24x16 px
     CreateEffectEntity(0, u8[[0x800E4334]+9], 0, X, Y, z0);      // 974  Y et z0 sur la pile, z0 SANS le +0x80000
     SpawnRandomLoot(0xFF, X, Y, z0);                             // 988  0x80032C7C
     PlaySoundEffect(0x1F);                                       // 990-994
  }
}
```

**Vérification par émulation (FAIT).** `$W/emu_cate.py` exécute les vraies instructions de `0x8003A7B0-0x8003A9DC`. C'est un interpréteur MIPS-I minimal sur mémoire synthétique, avec les trois appelés remplacés par des enregistreurs. Résultat : **4 000 vecteurs, 0 écart avec ce pseudo-code, 460 cases coupées**. Les bornes de Z `0x7FFFF`, `0x80000` et `0x80001` sont tirées exprès. Sortie dans `$W/emu_cate_vs_pseudocode.txt`.

Autres faits sur CATE :
- **FAIT** — la table `T` est bâtie à `0x8003B190-0x8003B1D4` : `T[p] = p/24` pour `p` dans `0..1247`. Hors de cette plage, c'est de la BSS non définie.
- **FAIT** — CATE ne lit ni n'écrit aucun flag de jeu et ne teste aucune classe ni statut. Toute entité qui a une boîte et passe par ces 4 sites coupe.
- **FAIT** — la case coupée garde sa hauteur, sa pente, sa `GroundProperty` et son `TileId` de sol (`+4`). Seuls le bit `0x02` de l'octet 0 et le mot `+6` changent. Une case coupée ne peut plus l'être, donc elle ne coupe qu'une fois même si plusieurs coins la touchent.

### 2.1 Qui lit le bit `0x02`

- **FAIT** — seuls deux sites le testent :
  - CATE, à `0x8003A8F4`.
  - L'opcode d'événement `0x9E`, à `0x80040BB4` (test à `0x80040C1C`) : compte les cases du rectangle avec `Walkability & 2` et compare à `v5`. L'annexe E19 le note « jamais cité ; 0 site » (`docs/plan-e19-sm-annexe/opcodes.tsv:160`, non revérifié ici).
  - Base de ce constat : les 13 fonctions qui chargent `[0x800DD86C]` et les 13 lecteurs des pointeurs de tuiles en cache `+0x148..+0x154`.
- **FAIT** — le masque de marche des entités est `0x40 | 0x01 (si flags&8) | 0x1000 (si flags&1)`. Il est construit à `0x800373E4-0x80037430`, `0x800374BC-0x80037518` et `0x8003AA68-0x8003AB14`. Le bit `0x02` n'y est pas. **Couper l'herbe ne change aucune collision ni aucune hauteur.**
- **INFÉRENCE** — mon balayage cherche `andi 2/3/0xFFFD` près des lectures. Une lecture par un autre chemin n'est pas exclue, mais aucune fonction de collision ou de rendu n'en montre.
- **FAIT** — pas de persistance : la carte est relue du disque à chaque chargement (cohérent avec D-E7-5).
- **FAIT** — il n'existe pas d'autre « effet de tuile » du même type. Les opcodes `0x54/0x55/0x85` (E7) écrivent les mêmes champs. `0x9E` lit.

---

## 3. Les appelés

### 3.1 `CreateEffectEntity` `0x8003BDD8` (70 appelants)

- **FAIT** — signature `(a0 = drapeau, a1 = indice de sprite d'effet, a2 = animation, a3 = X, [sp+0x10] = Y, [sp+0x14] = Z)`.
  - `0x8003B9C4` cherche le premier des 128 emplacements d'effet de `0x80138608` (pas `0x80`) dont `+0x68 == 0`. S'il n'y en a aucun, elle retourne 0 et CATE ignore le retour : butin et son suivent quand même.
  - `0x8003BB14` initialise : recopie du gabarit `0x8013C608` (0x80 octets), `+0x30=-1`, `+0x34=0`, `+0x68=2`, `+0x6C=(drapeau!=0)`, `+0x6D=!…`, `+0x6E/+0x70` indice et animation avec leurs compléments à un en `+0x6F/+0x71`, `+0x3C/+0x40/+0x44 = X/Y/Z`.
  - **Ni nombre aléatoire, ni son.**
- **FAIT** — appel de CATE : `(0, octet 9 de l'en-tête de carte, 0, X, Y, z0)`. Drapeau 0 = table globale, indice = octet 9, animation 0.
- **FAIT** — le décompilé nomme cet octet `BalanceLevel`. Son propre commentaire dit « AnimDeleteWall. NOM NON PROUVÉ, ce n'est PAS le niveau de balance » (`alundra-datas-analyser/.../DatasBin/GameMapInfo.cs:14`).
- **FAIT (données)** — l'octet 9 vaut 2 sur les 483 cartes. C'est `Info.BalanceLevel` dans les `map_N.json`, et la propriété `BalanceLevel` des 483 `.tileMap` de l'export.
- **FAIT (données)** — l'effet global 2 est exporté dans `Data/effects/effects-global.json` : 1 animation, 9 images de 5 ticks (45 ticks), fin `Destroy`, 9 jeux d'images 16×16.

### 3.2 Le butin : `0x80032C7C` (un seul appelant, CATE `0x8003A988`)

```
int SpawnRandomLoot(code=0xFF, X, Y, Z)                          // 0x80032C7C
  item = ChooseRandomlyAnItem(code);               // 0x80032968
  if (item == 0) return 0;
  if (item in {0x26,0x51,0x52} && MpMax == 0) return 0;   // 0x80032A00 / 0x8004DDDC lit [0x80176318]+6
  e = SpawnWarpEntity(0, 0, item+0x1E, X, Y, Z, 0);       // 0x80039F58 : parent nul, page globale, direction 0
  if (e == null) return 0;
  e->+0xB8 = 0xA0000;            // ForceZ : poussée de 10 px (lui dans le delay slot du bnez de 0x80032CE4)
  e->+0x274 = 1;  e->+0x6C &= ~0x80;
  e->+0x278 = (itemTable[item].byte4 != 0) ? 600 : -1;    // table 0x800C5F7C, 8 octets/objet
  e->+0x27C = 0;  e->+0x280 = 0;  e->+0x284 = 0xA0000;   // ItemState 0 ; AIValues[0..1]=0 ; AIValues[3]=10
  PlaySoundEffect(0x54);  return 1;
```

- **FAIT** — `ChooseRandomlyAnItem` fait **un** pas du générateur par maillon de chaîne :
  - Le pas est `s = s*0x7D2B89DD + 0xE06A02E7`, sur le mot `0x80098708` (valeur initiale `0xB017C93D`).
  - Puis `code = table[(code&0x7F)*16 + (s>>28)]` (table `0x80028E0C`, déjà portée par `AlundraLootRoll`).
  - Pour `0xFF`, la ligne `0x7F` vaut 14 zéros, `0xFD`, `0xFE`. La ligne `0x7D` donne `0x45×9, 0x46×5, 0x47×2`. La ligne `0x7E` donne `0x54×12, 0x55×4`.
- **Distribution exacte** (`$W/loot_distribution.txt`) :
  - rien : 224/256 (87,5 %) ;
  - `0x45` : 9/256 ;
  - `0x46` : 5/256 ;
  - `0x47` : 2/256 ;
  - `0x54` : 12/256 ;
  - `0x55` : 4/256.
- **Tirages par case coupée** : 1 mot (224/256) ou 2 mots (32/256). L'objet créé tire à sa création selon son `Contents`. Il vaut 0 pour les sprites 99, 100, 101, 114 et 115 (`Data/sprite-records.json`), donc 0 mot de plus.
- **FAIT (données)** — ces cinq sprites (page globale, `item+0x1E`) existent dans l'export avec `ProgramTick = 255` (C255).
- **INFÉRENCE** (nommage) — 99, 100, 101 sont I69, I70, I71 (1, 5, 10 Gildas) ; 114, 115 sont I84, I85 (petit cœur, cœur moyen). Le recensement `docs/plan-e14-annexe/e14-roster/handlers.tsv` donne explicitement 100 et 115 ; le reste vient du motif `item+30`.
- **FAIT** — l'octet 4 de la table `0x800C5F7C` est non nul pour 14 objets : `0x24-0x27, 0x29, 0x45-0x48, 0x51, 0x52, 0x54-0x56`. L'argent et les cœurs du butin durent donc 600 ticks.
- **FAIT** — `0x80032B90` (lâcher à la mort) n'est **pas** appelé par CATE.
  - Même corps, mais l'objet vient de `e->+0x3C`, la position de `+0x114/+0x118/+0x11C`, et `+0x280 = e->+0x40`.
  - Appelants : `0x8003A5B0`, `0x8003A6D0`, `0x800670F8`.
- **FAIT, lu en tête seulement (à reprendre par e0c-T0)** — gestionnaire C255 `0x8007C174` (381 mots) :
  - `+0x274 == 2` signifie ramassé.
  - Rebond : `+0x284 = +0x284*12>>4`, puis `+0xB8 = +0x284`.
  - Décompte de `+0x278`, avec `+0x1CC = 0x78` à 120 ticks et destruction à 0.
  - Ramassage par recouvrement de la boîte du héros (`0x8007C488-0x8007C598`), puis `0x80033F00`.
  - A255 est à `0x80061BD4` (80 mots).

---

## 4. Écarts entre la décompilation et le binaire

Références : `GameEngine.cs:2441-2527`, `EffectManager.cs:443-490`, `GameEngine.cs:844-870`.

| # | Décompilation | Binaire | Effet |
|---|---|---|---|
| D1 | garde `Breakpoint.TriggerBreak(); return;` si `X>>16` ou `(X+W)>>16 >= 1248` | aucune garde ; lit la table hors bornes (valeur **INCONNUE**) | ajout |
| D2 | colonnes `(X>>16)/24` | `T[(s16)(X>>16)]`, `T[(X+W)>>16]` | équivalent dans `0 ≤ p < 1248` |
| D3 | `rowB = ((Y+D)>>20)/16`, soit deux divisions par 16 ; `rowA = (Y>>16)/16` | `rowA = Y>>20`, `rowB = (Y+D)>>20` | **bogue du décompilé** : les coins sud testent la mauvaise rangée |
| D4 | l'effet et `RandomlySpawnItem` reçoivent `(Height<<20)+0x80000` | ils reçoivent `Height<<20` | **8 px de trop** dans le décompilé |
| D5 | indice d'effet nommé `BalanceLevel`, commentaire `//C?` | octet 9 de l'en-tête, même champ | nom seulement |
| D6 | `MapTiles[y*Map.Width+x]` | grille fixe de 52 colonnes | équivalent : les 483 cartes font 52×60 |
| D7 | `ChooseRandomlyAnItem` : `Random.Next()%17` puis un second `Random.Next()` (le premier est écrasé) | **un** pas par maillon | le décompilé avance le générateur **deux fois** par maillon |
| D8 | liste des 15 animations du héros | identique à la table `0x800228AC` | aucun |

Mesure (`$W/emu_cate.py 4000 decomp`, sortie `$W/emu_cate_out.txt`) : sur 4 000 vecteurs, le décompilé transcrit diffère du binaire dans **414** cas. 264 viennent du seul Z, 150 des rangées (D3).

---

## 5. Ce que la DLL a déjà

**Bouchons nommés de CATE**
- `AlundraWaveSwordAi.CheckAndTriggerTileEffect` : `AlundraWaveSwordAi.cs:34-37`, appelé par `Tick` à `:31`, journal `StubCallsForTests` à `:22-24`.
- `AlundraPlayerAnimationEffects.PlayerGrassCut` : `:214`, appelé à `:61-63` par `Create`. `Create` est appelé aux deux modes à `AlundraPlayerManager.cs:324` et `:337`.
- `IsGrassCutState` (`:191`) est correct : il égale la table lue.
- C94 et E12 ne sont pas enregistrés (E20.f4 et E20.h3).

**Boîte**
- `HasFrameBox` et `EvaluateFrameBox` : `AlundraEntityScriptProxy.HitPass.cs:31` et `:192-240`.
- `StoreHitBox` : `:85-97`, appelé à la création (`:76`) et en fin de pas (`AlundraEntityScriptProxy.cs:1597`). Il correspond à `0x80038064`.
- `HitBoxX/Y/Z` et `CollisionWidth/Depth/Height` : `AlundraEntityScriptProxy.cs:476-484`.
- Les boîtes viennent des `collision_keyframes` exportés (`AlundraFrameBoxTimeline.cs`).

**Convention de Z et cases**
- La Z de la DLL est celle du binaire moins 1 (ADR-0026). `IsWall` (`AlundraPlayerAnimationEffects.cs:167-175`) ajoute déjà `+1` à `HitBoxZ`.
- `AlundraCellsCollisionField.TryReadBinaryCell` : `:384-397`.
- `BinaryColumn` et `BinaryRow` : `AlundraPlayerAnimationEffects.cs:181-184`.

**Magasin de cases et visuel (E7)**
- `AlundraCellStore` (`AlundraCellStore.cs:58`) :
  - tableaux aliasés avec le champ de collision (`:78-90`) ;
  - pile de tuiles de mur dense ;
  - `GetWallTileStack` (`:176`), `CopyCellRectangle` (`:211`), `SetCellBits` et `ClearCellBits` (`:316`, `:323`, bornes `0x33/0x3B` en dur `:339-340`) ;
  - événement `CellsMutated` (`:76`).
- `AlundraCellVisualSync` (`:58`) :
  - `ProcessCellWalls` (`:327-426`) retire les tuiles de l'overlay quand la pile est détruite (`:337-354`) ;
  - `FlushPendingOverlayReconstruction` (`:211`), appelé à `AlundraWorldProxy.cs:2616`, reconstruit l'overlay entier une fois par image ;
  - installation : `AlundraWorldProxy.cs:1185-1240`.
- Passerelle `IEntityWorldContext.CellMutator` (`IEntityWorldContext.cs:105`). Son interface `IAlundraCellMutator` (`AlundraCellStore.cs:18-35`) n'a que trois opérations.

**Effets et son**
- `AlundraEffectBank` : `AlundraEffectBank.cs:99`, `TryGetTable` à `:119`. Il lit `effects-global.json`, et la table 2 y est.
- `AlundraEffectReservoir.Create(mapTable, effect, animation, x, y, z)` : `:221`. C'est `CreateEffectEntity` mode 0, 128 emplacements.
- `AlundraEffectStage` : `Emit` à `:190`, `CreateNow` à `:197` (ADR-0057, réservé au héros). Modèles : `AlundraNativeEffects` (`:9`).
- Accès depuis un natif : `IEntityWorldContext.EffectStage` (`:199`) et `IAlundraScriptHost.EffectStage` (`:145`).
- Son : `ScriptHost?.SoundPlayer?.PlaySfx(id)` (modèle `AlundraPlayerManager.cs:969, 1004, 1071`). Les sons 31 et 84 sont dans `Sounds/sfx-manifest.json`.

**Butin (e0b2 ✅)**
- `AlundraLootRoll.Choose` (`AlundraLootRoll.cs:51`) est `ChooseRandomlyAnItem` exact.
- `AlundraRandom.Next` : `AlundraRandom.cs:52`.
- Tirage à la création : `RollLootAtCreation` (`AlundraWorldProxy.cs:3429`).
- Apparition : `IEntityWorldContext.SpawnEntityBySpriteIndex` (`IEntityWorldContext.cs:70`, implémentée `AlundraWorldProxy.cs:3448` et `:3555`). Une constante du binaire se passe moins 1 (ADR-0063).
- Champs de l'objet présents sur le proxy : `ForceZ` (`:325`), `Bytes` (`:1214`), `DelayOrAngleOrEntityId` (`:1215`), `ItemState` (`:1216`), `AIValues` (`:1217`), `ContentsItemId` et `ContentsGameFlag` (`:93-94`).

**e0c non fait**
- `AlundraWorldProxy.DestroyEntity` : « (1) loot: E20.e0 » à `:3269`, « (4) release of the attached active effect (+0x1B8): E20.e » à `:3273`.
- Aucun gestionnaire C255 ni A255. Un lâcher créerait des objets inertes et permanents (`plan-e20-heros.md:4789`).

**Tests qui épinglent les bouchons** (à faire évoluer)
- `AlundraFlailBounceTests.cs:375, 395, 502, 507, 757`.
- `AlundraWaveSwordLifeTests.cs:45, 122, 141`.
- `AlundraWaveSwordWiringTests.cs:43`.
- `AlundraWaveSwordSupport.cs`.

---

## 6. Ce qui manque

1. **Une primitive de coupe dans `AlundraCellStore`.**
   - Aucune opération ne met `WallTilesOffset` à -1 ni ne détruit la pile. `ClearCellBits(col,row,0x02,0)` efface le bit avec les mêmes bornes que le binaire, mais ne touche pas la pile.
   - Il faut une opération qui fasse les trois écritures puis déclenche `CellsMutated`.
   - Les natifs n'ont pas accès au magasin. `IAlundraScriptHost` n'expose ni `CellMutator` ni `SpawnEntityBySpriteIndex`. Seul `IEntityWorldContext` les a, et le héros passe par `host is IEntityWorldContext` (`AlundraPlayerManager.cs:1203-1206`).
   - `AlundraPlayerAnimationEffects.Create(player, mode)` ne reçoit pas l'hôte : il lit `player.ScriptHost`.
2. **Le port de CATE** (§2) et son câblage aux deux sites existants.
3. **La lecture de l'octet 9.** Il est exporté (`BalanceLevel` = 2 sur 483 cartes) mais lu par personne dans la DLL. Seul un commentaire de `AlundraBalanceTables.cs:151` le cite.
4. **Données : rien ne manque pour CATE.**
   - Le binaire ne remplace aucune tuile : la pile est supprimée et le sol `+4` reste.
   - Les attributs de case sont exportés (`AlundraCells`).
   - Les 18 342 tuiles de pile visibles des cases coupables sont toutes dans `AlundraWallPlacements`, donc retirables par E7.
   - L'effet global 2, les sons 31 et 84 et les sprites d'objets sont exportés.
   - **INCONNU** : que les sprites 27-29 (vague), 7 (flèche chargée) et 2 (bombe) aient des boîtes de frame exportées. Sinon la fonction rend la main au premier test, comme le ferait le binaire si ses données n'en ont pas.
5. **Le butin** : C255 et A255, l'effet du ramassage, `0x80032C7C`, `0x80032B90`, le veto `MpMax`.
6. **Le coût de l'overlay.** Jusqu'à 5 037 entrées (sol plus murs) sont reconstruites par image où une coupe arrive. La carte 16 en a 1 816 + 2 360 pour 266 cases coupables. La conception d'E7 comptait environ 1 360 (`$W/overlay_sizes.txt`). À mesurer, non bloquant.

---

## 7. Données : cartes avec des cases coupables

**Méthode** (réexécutable, lecture seule) :
- `$W/census_cuttable.py` lit les 483 `data-extracted/data/map_N.json` et compte les `Map.MapTiles[i]` avec `Walkability & 2`.
- `$W/census_export.py` refait le comptage indépendamment sur l'export (`alundra-project/Maps/*/*/tilemap/*.tileMap`, propriétés `AlundraCells` et `AlundraWallPlacements`).
- Les deux donnent le même résultat. La table par carte est dans `$W/census_cuttable.tsv`.

**Résultats**
- **9 089 cases dans 165 des 483 cartes**, soit le chiffre du plan (§0.2.6). Les 318 autres cartes n'en ont aucune, dont la 389 (l'intro).
- Toutes les cartes font 52×60. L'octet 9 vaut 2 partout.
- 8 989 cases ont une pile de tuiles de mur (18 342 tuiles).
- 100 n'en ont pas : la coupe est alors invisible (effet, butin, son, rien ne disparaît).
- Aucune n'a aussi le bit `0x40`.
- L'export compte 163 881 piles au total, dont 8 989 coupables (5,5 %). Les autres murs, arbres et rochers ne le sont pas.
- Forme des piles coupables :
  - 8 piles de 1 tuile ;
  - **8 630 de 2 tuiles** (id et id+10, un buisson de 2 tuiles de haut) ;
  - 332 de 3 tuiles ;
  - 19 de 4 à 10 tuiles.
- Décalage 2 pour 8 702 piles, 3 pour 281, 0 ou 1 pour 6.
- 129 piles distinctes, 213 ids de tuile. Les 12 plus fréquentes couvrent 6 451 cases, par exemple `(12895, 12905)` : 1 793 cases sur 34 cartes.
- Hauteur (octet 3) de 0 à 29. Par exemple 373 cases à 0, 2 325 à 1, 1 011 à 2, 2 054 à 3, 893 à 10.
- Cartes les plus riches :
  - 16 : 266 cases ;
  - 14 et 393 : 179 ;
  - 3 : 165 ;
  - 20 : 162 ;
  - 479 : 158 ;
  - 5, 83 et 410 : 156 ;
  - 6 et 335 : 148 ;
  - 331 : 134.
- Candidate de recette : la 14 (`Overworld 3,1`, 179 cases dont 177 avec pile).

---

## 8. Questions ouvertes

**Pour l'auteur ou la session principale**
- **Q-E1** — Ordre. Le plan exige E20.e0c avant E20.e. On peut livrer la coupe d'abord avec un butin « tirage seul » (`Choose(0xFF)` sans apparition) qui garde le générateur fidèle (1 ou 2 mots par case) sans rien faire tomber. Recommandation : suivre le plan.
- **Q-E2** — Moment de création de l'effet et du son : file de la scène (`Emit`, ADR-0046) ou à l'instant (`CreateNow`, réservé au héros par ADR-0057) ? Le butin apparaît à l'instant et tire dans le générateur, donc l'ordre relatif des tirages avec les effets mis en file (étincelles) peut différer. Le binaire n'a pas de file.
- **Q-E3** — Octet 9 : lire la propriété exportée `BalanceLevel` (mal nommée, vaut 2 partout) ou figer l'effet global 2 ? Recommandation : lire, avec repli sur 2.
- **Q-E4** — Les 100 cases sans pile (cartes 2, 5, 6, 7, 8, 11, 12, 13, 14, 15, 16, 24 et d'autres jusqu'à la 481 ; liste complète dans `$W/census_cuttable.tsv`) : garder le comportement du binaire, coupe invisible avec butin. Oui par défaut.
- **Q-E5** — Aucune persistance dans le binaire : l'herbe repousse à chaque entrée de carte (D-E7-5). À confirmer comme voulu.

**Pour la tranche (T0, pas de décision)**
- **Q-E6** — Repère de Z. L'inférence est `HitBoxZ+1` dans les deux comparaisons, comme `IsWall`. La Z de la case passe telle quelle à l'effet et moins 1 à `SpawnEntityBySpriteIndex` (ADR-0063). À confirmer sur la machine MIPS, car l'égalité exacte `HitBoxZ_DLL == zc` est atteignable.
- **Q-E7** — Colonnes hors table (`X < 0` ou `X+W ≥ 1248 px`). Le binaire lit de la mémoire non définie ; la DLL borne (`BinaryColumn`). Écart à documenter. La position de l'effet utilise la colonne non bornée (INCONNU hors table).
- **Q-E8** — Participation au tick de l'objet né d'un appel du héros (dans `MovePlayer`) ou d'une vague (bloc des entités). Les règles d'e0a3b et e0a5 existent ; à mesurer une fois câblé.
- **Q-E9** — Renommer « BalanceLevel » en « octet 9 de l'en-tête (effet de coupe) » dans la doc de format et le convertisseur. Hors code.

---

## 9. Tâches proposées, en ordre de dépendance

**Prérequis déjà ✅** : E20.c1a/c1b (boîtes), E20.e0a1-e0a5 (apparition par indice), E20.e0b1/e0b2 (générateur et tirage à la création), E20.e0k1-e0k3 (gravité des entités nées par l'interface), E7 (magasin et visuel), E20.f2 (squelette de `CreatePlayerAnimationEffects`), E20.f3c (C61 en production).

**E20.e0c — butin et objet ramassable** (aucune section détaillée n'existe encore dans le plan)
1. **e0c-T0, découverte** (annexe et oracle sur la machine MIPS, comme e0b).
   - À lire : `0x80032C7C`, `0x80032B90`, C255 `0x8007C174`, A255 `0x80061BD4`, `0x80033F00`/`0x80033DBC`/`0x80032B28`, table `0x800C5F7C`.
   - Vecteurs attendus : champs écrits, rebond, clignotement à 120, durée 600, effet du ramassage (argent, cœurs), son `0x54`.
   - Dépend de : rien.
2. **e0c-T1, `AlundraLootDrop` pur.**
   - `SpawnRandomLoot(code,x,y,z)` : `Choose`, veto `MpMax`, `SpawnEntityBySpriteIndex(null,false,item+0x1E,x,y,z-1,0)`, écritures, son `0x54`.
   - `SpawnContents(entity)` pour `0x80032B90`.
   - Dépend de : T0.
3. **e0c-T2, C255 et A255 natifs.**
   - Ramassage, rebond, durée de vie, clignotement, effet argent et PV.
   - Dépend de : T0, T1. Question PV/argent liée à E20.h1.
4. **e0c-T3, brancher le bouchon `DestroyEntity` (1)** (`AlundraWorldProxy.cs:3269`) sur `SpawnContents`. Peut attendre E14 ou E20.g2.

**E20.e — herbe coupée**
5. **e-T0, annexe et oracle de CATE.**
   - Verser ce rapport dans `docs/plan-e20-annexe/e20-e/`.
   - Rejouer le §2 sur la machine MIPS du dépôt (pas mon interpréteur), avec les vrais appelés : effet, butin, son.
   - Vecteurs : boîtes, cases, bornes de Z, états du générateur.
   - Parallèle à e0c-T0.
6. **e-T1, primitive de coupe dans le magasin.**
   - Opération `CutCell(col,row)` : bit `0x02` effacé, `WallTilesOffset=-1`, pile détruite, `CellsMutated`. Exposée aux natifs.
   - Tests : l'overlay perd les 2 tuiles d'un buisson de la 14, collision et navigation inchangées, une seule reconstruction par image.
7. **e-T2, `AlundraTileEffect.Trigger` pur.**
   - Le pseudo-code du §2 : 4 coins, bornes, règle de Z en repère DLL, position non bornée.
   - Effet global (octet 9, Q-E3), butin (e0c-T1), son `0x1F`.
   - Ordre : effacement de la case, effet, butin, son.
   - Dépend de : e-T0, e-T1, e0c-T1.
8. **e-T3, câblage et retrait des bouchons.**
   - `PlayerGrassCut` (deux modes) et `AlundraWaveSwordAi.CheckAndTriggerTileEffect` appellent `Trigger`.
   - Mise à jour des tests `AlundraFlailBounceTests` et `AlundraWave*`.
   - C94 (E20.f4) et E12 (E20.h3) n'ont que l'appel à ajouter.
9. **e-T4, arcs sur cartes réelles.**
   - Coup d'épée sur un buisson de la 14, vague sur un buisson, parité du générateur (1 ou 2 mots par case).
   - ADR pour le repère de Z, le moment de l'effet et l'octet 9.
   - Dépend de : e-T2, e-T3 ; e0c-T2 pour le ramassage.
10. **e-T5, recette.** Sauvegarde de test `herbe-m14` : héros près d'un buisson, épée équipée. L'auteur coupe, voit l'effet, l'objet monter, tomber, puis le ramasse.

**À part (non analysé ici).** La ligne d'enveloppe E20.e contient aussi les pas, la poussière de marche et de course, l'atterrissage et l'eau de `CreatePlayerAnimationEffects`. Les bouchons correspondants sont à `AlundraPlayerAnimationEffects.cs:204-211`. Les sites `CreateEffectEntity` sont `0x8002FC4C, 0x8002FF30, 0x8003001C, 0x800300C8, 0x800302A4, 0x80030308` et les sons `0x8002FB9C, 0x8002FE58, 0x8002FEA4, 0x80030260`. Il reste aussi la libération de l'effet actif `+0x1B8` de `DestroyEntity`. Je propose une tranche **E20.e2** avec son propre T0.

---

## 10. Fichiers de travail (`$W`)

- **Listings :**
  - `cate.txt`, `loot_80032968.txt`, `loot_80032c7c.txt`, `loot_80032b90.txt`
  - `f_CreateEffectEntity.txt`, `f_8003b9c4.txt`, `f_8003bb14.txt`, `f_hitbox_update.txt`, `f_8004dddc.txt`, `f_800490fc.txt`
  - `hero_grass_region.txt`, `c61.txt`, `c94.txt`, `e12.txt`, `c255.txt`
- **Scripts :**
  - `dis2.py` (désassembleur étiqueté), `callers.py`
  - `emu_cate.py` (émulation et comparaison : `python -B emu_cate.py 4000`, ou `4000 decomp`)
  - `loot_distribution.py`, `census_cuttable.py`, `census_export.py`, `census_shapes.py`, `overlay_sizes.py`
  - copies de `exelib.py` et `mipsdis.py`
- **Sorties :**
  - `emu_cate_vs_pseudocode.txt`, `emu_cate_out.txt`, `loot_distribution.txt`
  - `census_cuttable.tsv` et `.json`, `census_export.json`, `census_summary.txt`, `overlay_sizes.txt`