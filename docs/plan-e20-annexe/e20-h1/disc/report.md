# E20.h1, les cinq objets instantanés : rapport de découverte (lecture seule)

`report.md` n'a pas pu être écrit : l'outil Write l'a refusé ("Subagents should return findings as text"). Ce message en tient lieu.

Dépôt lu au HEAD `e8087d74dc7fd8da661490be2bdacaf1e9e115b4`. Le dernier commit qui touche le code de la DLL est `15804ae3` (2026-10-17) ; les suivants sont des documents. Le binaire `ALUN_CD.EXE` (France) a bien le SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` : mes scripts (`oracle/bdis.py`) refusent tout autre fichier. Rien n'a été écrit dans le dépôt ni dans le binaire. `git status` est identique à celui du départ (deux sous-modules marqués ` m`).

Le dossier de sortie est `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h1/h1/`, appelé `H1/` ci-dessous. Les harnais des annexes sont copiés dans `H1/oracle/h4b_h/` et `H1/oracle/h4o/` ; aucun n'est lancé en place.

**Légende.**
- **FAIT [L]** : lu dans mes listings capstone (`H1/oracle/lst/`, retrouvables par l'adresse).
- **FAIT [V]** : vecteurs rejoués sur le vrai code depuis une mémoire vide.
- **FAIT [M]** : mesuré dans le jeu émulé (vrai `MovePlayer`, vraies passes).
- **FAIT [DLL]** : code lu (`fichier:ligne`).
- **INFÉRENCE** / **INCONNU**.

Le binaire tranche ; la décompilation se lit, ne se corrige pas.

---

## 0. Ce qui change le plan

1. **Les cinq objets sont instantanés au sens strict.**
   - Leur départ rend la constante 1 (`0x80034A38…`, puis `0x80034AB8`), quel que soit le résultat du soin.
   - Le répartiteur n'écrit alors ni l'objet en cours `[0x80127164]`, ni le compteur, ni la phase : `0x80034A88-0x80034A9C` ne s'exécutent que pour une réponse 0.
   - Aucun soin n'écrit l'animation du héros. FAIT [L][V][M].
   - Sur les 30 clés de monde (1 250 ticks), l'objet en cours, la phase, le compteur, l'animation et la position ne bougent jamais.
2. **Les départs ne regardent ni l'objet porté (`+0x2C`), ni l'animation (`+0x88`), ni la pente (`+0x18C`), ni l'objet en cours.**
   - Sur 2 649 vecteurs, les octets lus se réduisent aux PV et PM, à la position du héros, au compteur de l'objet et au générateur (`oracle/out/rw_classes_out.txt`). FAIT [V].
   - Les gardes sont celles du cadre : front du Rond, `[0x800DC4B8] == 0`, objet promu < `0x62`, pas un autre objet en cours, pas un trou de la table.
   - Mesuré : les cinq objets se boivent depuis exactement les 63 états où Rond démarre un objet. Les états de portage `05 06 07 0B 0C 2E 2F 30` en font partie (la bombe y est refusée par `+0x2C`, pas l'herbe). FAIT [M].
   - Les 17 états sans réaction sont `03 0D 1A 1B 1E 27 36 37 38 3D 43 45 47 48 4A 4C 4D`. Les sorties des cinq objets sont identiques à l'octet (`oracle/out/states_item_24.txt`, `_25`, `_26`, `_27`, `_29`).
3. **Les soins écrivent les PV de l'entité (`+0x14`), jamais les stats.**
   - Les PM passent par les fonctions des stats (`0x8004DEA4`, `0x8004DDDC`, `0x8004DEBC`).
   - Les stats suivent par le miroir de fin de `MovePlayer` au même tick (clé : `stats_hp == hero_hp` au tick 1). FAIT [V][M]. La DLL a ce modèle depuis E20.c2a.
4. **Cinq soins distincts, tous avec l'effet global 14.** Le nombre de tirages du générateur va de 0 à 16 (tableau §1.2). FAIT [L][V].
5. **Le second site de garde `0x8002BB94` n'est pas une potion de secours.**
   - C'est le geste Start + Select tenu 121 images : il consomme un Breuvage de soin s'il y en a un, puis met les PV du héros à 0 (mort). §1.6.
   - Il appartient à E18 (`e20-c2/c2-binary.md` §2.5 le disait déjà). h1 n'a rien à y porter. FAIT [L][V], recoupé par la décompilation (`GameEngine.cs:1522-1553`).
6. **Les fonctions de soin servent ailleurs** (§1.7) : l'eau 1 et l'eau 2 (`0x80035994`, `0x800359A8`), la mort et le "réessayer" d'E18, et le ramassage C255. Un soin ramassé que le sac ne prend pas est bu sur place (`0x80033F00`).
7. **Écarts relevés** (§1.8).
   - Le répartiteur décompilé envoie la Potion S sur le corps du Breuvage de soin.
   - La Potion L décompilée place ses anneaux avec le mauvais indice.
   - `h-g-deps.md` §2.1 compte 3 tirages par étincelle de la Potion S (c'est 2).
   - `test-means.md` donne à la Potion S l'effet "PV et PM" et au Breuvage une cinématique.
8. **Oracle prêt.**
   - 2 649 vecteurs (1 775 "jeu", 874 "synthétiques"), 0 échec au rejeu depuis une mémoire vide.
   - Couverture de 100 % des instructions des 14 fonctions.
   - Mon modèle : 0 écart. 141 mutants du modèle tous tués, 6 équivalents prouvés à part.
   - 30 clés de monde (R-EL-4, index écrits, `_pf`) à manifeste identique sur deux exécutions.
9. **Aucune question de comportement visible n'est ouverte pour h1.** Trois questions d'organisation avec défaut (§4.3). Aucun défaut avéré de l'original dans la tranche.

---

## 1. Faits du binaire

### 1.1 Du Rond au soin

| Étape | Adresse | Fait |
|---|---|---|
| `TryUseItem` | `0x8002ED64` | `[0x80126F2A] & 0x20` (mot "pressé", front) nul : rend 1. Front et `[0x800DC4B8] != 0` (n'importe quel bit) : `PlaySoundEffect(3)`, rend 1. Sinon rend la réponse du répartiteur. FAIT [L][V] (178 vecteurs). |
| Répartiteur | `0x8003499C` | `s0 = 0x8004E0F8()` (objet du Rond promu, −1 si le compte est 0). Refus (son 3, rend 1) si : `s0 >= 0x62` non signé (`0x800349B0`) ; `[0x80127164]` non nul et différent de `s0` (`0x800349C0-0x800349CC`) ; `s0 − 0x1F >= 0x14` non signé (`0x800349D4`) ; trou de la table. |
| Table de départ | `0x8002339C` | Les entrées sont listées ci-dessous. |
| Les départs | `0x80034680` (0x24), `0x800346F0` (0x25), `0x80034760` (0x26), `0x800347D4` (0x27), `0x80034870` (0x29) | Condition, puis soin (avec ses effets), puis `PlaySoundEffect(son)`, puis `UseItem(objet)` ; sinon son 3 ; rend 1. Le héros est l'entité en dur `0x80127D30`. FAIT [L][V]. |
| Consommation | `0x8004E5C4` | `UseItem(i)` : `i < 0` ou `i >= [0x8017638C]` (0x63) : message d'erreur, rend 0, rien écrit. Compte (halfword signé à `[0x8017631C] + 4i + 2`) nul : rend −1, rien écrit. Sinon compte − 1. |

Entrées de la table `0x8002339C` :
- `0x1F` → `0x800349FC`, `0x20` → `0x80034A0C`, `0x23` → `0x80034A1C`.
- `0x24` → `0x80034A2C`, `0x25` → `0x80034A3C`, `0x26` → `0x80034A4C`, `0x27` → `0x80034A5C`, `0x29` → `0x80034A6C`.
- `0x2B`-`0x32` → `0x80034A7C`.
- Trous (`0x80034AAC`, refus) : `0x21`, `0x22`, `0x28`, `0x2A`.

La réponse des cinq départs est écartée par le répartiteur (`addiu v0, zero, 1`). FAIT [L][V] (204 vecteurs).

L'objet reste équipé à 0 exemplaire. L'appui suivant est refusé (`0x8004E0F8` rend −1, son 3). Clé `herb_last_one_then_none` : tick 30, son 3, compte 0. FAIT [M].

### 1.2 Les cinq objets

Noms (`EntityNames.csv`) : I36 Herbe médicinale, I37 Potion L, I38 Extrait magique, I39 Breuvage de soin, I41 Potion S. Les noms vus en jeu viennent d'`Etc.yarn` via `test-means.md` : Herbes, Elixir de force, Elixir de magie, Essence magique, Fortifiant. Maxima de possession : Herbe 9, les quatre autres 1. Emplacements d'inventaire 12, 13, 15, 30, 14.

| Objet | Condition (signée) | Soin | Effets (effet 14, `0x8003BDD8(0, 14, anim, x, y, z)`) | Tirages | Son | Compte |
|---|---|---|---|---|---|---|
| **0x24 Herbe** (`0x80034680` → `0x80032E50`) | PV < PVmax (entité `+0x14`, `+0x18`, 32 bits) | `PV = min(PVmax, PV + max(5, PVmax/4))` | 1 effet, animation 0, Z + `0x100000`, `ForceZ 0x20000` | 0 | `0x31` | −1 |
| **0x25 Potion L** (`0x800346F0` → `0x80033274`) | idem | `PV = PVmax` | 8 anneaux, animation 0, Z + `0x80000` | 0 | `0x32` | −1 |
| **0x26 Extrait magique** (`0x80034760` → `0x800336F0`) | PM < PMmax (stats `+4`, `+6`, halfwords signés) | `PM = PMmax` | 8 anneaux, animation 2, Z + `0x80000` | 0 | `0x30` | −1 |
| **0x27 Breuvage de soin** (`0x800347D4` → `0x80033A2C`) | PV < PVmax ou PM < PMmax (la vie d'abord) | `PV = PVmax` et `PM = PMmax` | voir ci-dessous | 16 | `0x30` | −1 |
| **0x29 Potion S** (`0x80034870` → `0x800330FC`) | PV < PVmax | `PV = min(PVmax, PV + max(10, PVmax/2))` | 4 étincelles, animation 0, Z + `0x100000`, `ForceZ 0x20000` | 8 | `0x31` | −1 |

Effets du Breuvage de soin :
- 8 anneaux d'animations 2, 0, 2, 0, 2, 0, 2, 0 (Z + `0x80000`).
- Puis 4 fois : une étincelle d'animation 0 (`ForceZ 0x40000`) et une d'animation 2 (`ForceZ 0x20000`), Z + `0x100000`.

Un refus joue le son 3 sans rien consommer. L'ordre dans un départ est : soin (position relue à chaque création), puis son, puis consommation.

Les divisions suivent le C (vers zéro). Pour un `PVmax` négatif, le résultat est écarté par le minimum. La ligne `0x80032E68` (l'`addiu +3`) n'est donc atteignable que par un vecteur synthétique et sans effet observable.

**L'anneau** (L, Extrait, Breuvage ; `i = 0..7`), si l'effet est obtenu :
- `+0x3C += tabX[4i] << 11`, `+0x40 += tabY[4i] << 11` ;
- `ForceX = tabX[(8+4i)&31] × 448`, `ForceY = tabY[(8+4i)&31] × 448`, `ForceZ = 0x30000`.

Tables de demi-mots à 32 entrées, `0x80023654` (768·cos) et `0x80023694` (512·sin) : `lst/halfword_tables_80023654_80023694.txt`. Ce sont celles de `BurstTableX/Y` de la DLL (`Alundra/Rendering/Effects/NativeEffects.cs:78`, `:85`). Les constantes de la fiole de vie diffèrent (`<< 13` et `<< 9`, `NativeEffects.cs:42-44`) : on ne réutilise que les tables.

**L'étincelle**, si l'effet est obtenu :
- deux tirages : `r1 = s·0x7D2B89DD + 0xE06A02E7`, puis `r2` de même à partir de `r1` (état final du générateur `0x80098708` = `r2`) ;
- `ForceX = hi(r1 × 0x20001) − 0x10000`, `ForceY = hi(r2 × 0x30001) − 0x18000`.

Un effet refusé (réservoir plein) ne tire pas. Clé `spotion_pool_2_free` : 2 obtenus, 4 tirages. Clé `cureale_pool_5_free` : 5 anneaux, 0 tirage. `GameRandom.Next()` de la DLL est exactement ce pas.

### 1.3 L'effet créé

`0x8003BDD8` (`lst/createeffect_8003BDD8.txt`) prend un emplacement libre parmi 128, ou rend 0 s'il n'y en a pas. `GetFreeEffect` `0x8003B9C4` : base `0x80138608`, pas `0x80`, libre si `+0x68 == 0`.

`InitEffect` (`0x8003BB14`) recopie un gabarit de `0x80` octets (forces à 0), écrit la position en `+0x3C/+0x40/+0x44` et marque l'emplacement actif (`+0x68 = 2`). Les champs que les soins écrivent ensuite sont exactement ceux d'`EffectSlot` :
- `+0x3C/40/44` → `X/Y/Z` ;
- `+0x54/58/5C` → `ForceX/Y/Z`.

L'argument `a2` est l'animation (0 ou 2). L'effet 14 de `effects-global.json` a trois animations : 0 "Destroy", 1 "Loop", 2 "Destroy" (images différentes). FAIT [L]. Cela recoupe `EffectReservoir.Create(mapTable, effect, animation, x, y, z)` (`EffectReservoir.cs:101`).

### 1.4 Ce que les soins écrivent (FAIT [V])

- **Herbe, eau 1, Potion S** : `hero+0x14`, `effect+0x54/58/5C`, `rng`.
- **Potion L** : `hero+0x14`, `effect+0x3C/40/54/58/5C`.
- **Extrait** : `stats+4` et les effets.
- **Breuvage** : `hero+0x14`, `stats+4`, les effets, `rng`.
- Les départs ajoutent seulement `inventory[item].count`.
- Aucun soin n'écrit `stats+0`, `stats+2`, ni un autre champ du héros que `+0x14`.
- Ils n'appellent pas `0x8004B730` (le calage du HUD de l'opcode `0xC3`) : le HUD roule jusqu'aux nouvelles valeurs.

### 1.5 Les états (FAIT [M])

`oracle/states_herb.py` reprend la méthode de `e20-h3/critic/run/scen_allstates.py`. Pour les 80 états de `0x80022D3C`, au sol et en l'air, objet équipé, vie 3/10, un Rond : 63 états boivent, 0 refus, 17 sans réaction. Ces 17 sont `03` (course), `36` (chargement de carte) et les 15 états `Skip` de la nage.

Non mesuré : les états d'échelle et de grimpe dans leur contexte, et la nage (pente 4). INFÉRENCE seulement : les gestionnaires de nage `Float`, `FlipThenFloat` et `ItemOnly` appellent `TryUseItem` (`PlayerManager.cs:154-160`, `:669-679`, `:700`, `:726`).

### 1.6 Les deux sites de garde

Les deux sont dans la fonction par image `0x8002BAEC` (elle finit par le pas du générateur `0x8002BD0C-0x8002BD34`). Je l'ai rejouée entière depuis une mémoire vide (31 vecteurs, `oracle/vec_ss/`) :
- quatre appelés (`0x8002E38C`, `0x8002E058`, `0x8004B1D4`, `0x80055570`) sont remplacés par des crochets ;
- `GetNumberOfItem`, `UseItem` et `SetPlayerHp` sont réels ;
- mon modèle des deux blocs : 0 écart (`oracle/out/check_startselect_out.txt`).

**Start + Select (`0x8002BB64-0x8002BC4C`).**

*Conditions* (toutes à la fois) :
- `[0x800DC4B8] == 0` ;
- entité héros `+0x20 == 0` ;
- `[0x80127164] == 0` (aucun objet en cours) ;
- mot de maintien `[0x80126F28]` (halfword) exactement `0x900` (Start + Select, aucun autre bouton) ;
- `[0x800DC4B4] == 0` (`g_warpDelayFrames` de la décompilation) ;
- `[0x800C4980] == 0` (`g_globalTransitionState`).

*Comportement* :
- Le compteur `[0x800986F0]` monte de 1 par image ; toute condition fausse le remet à 0.
- À la 121ᵉ image (compteur lu `== 0x78`), si les PV du héros (`+0x14`) sont non nuls : si `GetNumberOfItem(0x27) != 0`, alors `UseItem(0x27)` (un Breuvage de soin de moins).
- Puis : compteur = 0, PV de l'entité = 0, `+0x1CC = 0x78`, `SetPlayerHp(0)` sur les stats, `[0x800A8284] = 0` (affichage des PV du HUD).
- PV déjà à 0 : rien, le compteur reste à `0x78`.

Il utilise donc bien un objet (le Breuvage, `0x27`) mais ne soigne pas : il tue. Le Breuvage est consommé avant la mort ; la branche de mort d'E18 (`0x800327F0`) peut ensuite en utiliser un second pour ressusciter.

INFÉRENCE sur l'intention (geste "abandonner") ; la décompilation donne la même suite.

**Pour h1** : la condition "aucun objet en cours" ne coupe jamais le geste tant que rien ne reste en cours, ce que les cinq objets ne font pas. La garde n'a de sens qu'avec h3 et h4.

**Le point O-H-3 du plan** est mal étiqueté ("la potion de secours") et mal assigné à h1 : il relève d'E18. La DLL ne porte pas le geste (`Alundra/Transitions/WarpDirector.cs:303`).

**Menu Start (`0x8002BC58-0x8002BD04`).** Après la décrémentation de `[0x800DC4B4]`, la demande `[0x800DC4C4] = 1` n'est écrite que si toutes ces conditions tiennent :
- `[0x800DC4B8] == 0`, `+0x20 == 0`, `[0x80127164] == 0` ;
- mot "pressé" `[0x80126F2A] & 0x803 != 0` ;
- `[0x800DC4B4] == 0` ;
- `[0x80126F28] & 0x100 == 0` ;
- `[0x800C4980] == 0` ;
- `0x80055570() == 0`.

C'est la garde que h0a pose dans `Alundra/UI/Inventory/InventoryDirector.cs:419-422`.

### 1.7 Qui d'autre appelle ces fonctions (FAIT [L], `xref.py`)

| Fonction | Appelants |
|---|---|
| `0x80032E50` (herbe) | `0x80033F5C` (ramassage, voir `0x80033F00`), `0x800346B0` (départ) |
| `0x80032F84` (eau 1) | `0x80033EA0` (butin `0x56`, fonction `0x80033DBC`), `0x800340BC` (fonction `0x80033F00`, entrée `0x56`), `0x800341FC` (fonction `0x80034108`, ids `0x45`-`0x56`), **`0x80035994` (pilote de l'eau, objet `0x2D`, compteur 30)** |
| `0x800330FC` (Potion S) | `0x80033FE0` (ramassage), `0x800348A0` (départ) |
| `0x80033274` (Potion L) | `0x80033F80` (ramassage), `0x80034720` (départ), **`0x800359A8` (pilote de l'eau 2)** |
| `0x800336F0` (Extrait) | `0x80033E60`, `0x8003406C`, `0x800341AC` (butin `0x52`), `0x80034790` (départ) |
| `0x80033A2C` (Breuvage) | `0x80033FBC` (ramassage), `0x80034828` (départ), `0x80041B14` (gestionnaire d'opcode : il appelle `0x80033A2C(héros, 0x27)` puis le son `0x31`). INFÉRENCE : opcode `0xBB`, le "réessayer" d'E18. |
| `0x800347D4` (départ du Breuvage) | la branche de mort (`c2-binary.md` §2.5, `0x80032838`) |
| `0x80033F00` `(héros, objet)` | `0x8007C598`, gestionnaire C255 (`0x8007C174`) |
| `0x80034108` `(objet)` | `0x8003FE94`. Elle ne traite que les ids `0x45`-`0x56` ; pour les objets `0x24`-`0x29` elle appelle `0x80033EC8` : ajout au sac sans boire. |
| `0x80033DBC` (effets du butin) | `0x8007C358` (C255) |

Détail de `0x80033F00` :
- Pour `0x24`, `0x25`, `0x27`, `0x29`, `0x80033EC8` essaie `AddOneItemIfUnlocked` (rend 1 si le compte a monté). Si le sac ne le prend pas, le soin est appliqué sur place.
- Table `0x800230D4` : `0x24` → `0x80032E50`, `0x25` → `0x80033274`, `0x27` → `0x80033A2C`, `0x29` → `0x800330FC`. L'objet `0x26` → `0x800335AC` (+1 PM, pas l'Extrait).
- Le graphe d'appels est FAIT ; la sémantique de la valeur de retour de C255 est INFÉRENCE.

Détail de `0x80033DBC` : `0x54` → `0x80032E2C` (+2 PV, sans effet), `0x55` → `0x80032EEC` (`+max(1, PVmax/4)`, un effet), `0x56` → `0x80032F84`, `0x52` → `0x800336F0`, `0x53` → `0x800333AC` (fiole de vie).

### 1.8 Écarts et corrections

**Décompilation** (`alundra-datas-analyser/…/Gameplay/PlayerManager.cs`, lue, non corrigée) :
- **D-H1-1.**
  - `UseItem` décompilé (`:1920-2004`) envoie `0x29` à `TryWarpWithExplosionEffect` (`:2299`), qui est le corps du Breuvage de soin (PV ou PM, son `0x30`).
  - Le binaire envoie `0x29` à `0x80034870` : PV seulement, `+max(10, PVmax/2)`, quatre étincelles, son `0x31`.
  - Les noms décompilés sont trompeurs : `PlayCutscene` est le départ du Breuvage.
  - La fonction de soin de la Potion S existe (`AddHugeHpAndSpawnEffect`, `:5021`) mais n'est appelée que par le ramassage (`:5115`).
- **D-H1-2.**
  - `RestoreHpAndCreateEffect` (Potion L, `:4918`) décale la position des anneaux avec l'indice de la force, `(8+4i)&31`.
  - Le binaire décale avec `4i` et pousse avec `(8+4i)&31`. Les vecteurs de `0x80033274` le tranchent.
  - L'Extrait et le Breuvage décompilés sont corrects.
- Les autres corps (`0x80032E50`, `0x800330FC`, `0x800336F0`, `0x80033A2C` et les départs `0x24`, `0x25`, `0x26`, `0x27`) concordent avec le binaire.

**Annexes et plan** :
- `e20-armes/h-g-deps.md` §2.1 : "Potion S : 4 étincelles à 3 tirages" est faux. C'est 2 tirages par étincelle, 8 au total. RÉFUTÉ [L][V][M]. Le reste du tableau est confirmé, mais il dit "0x24-0x29" alors que la table a deux trous (0x28 et 0x2A).
- `e20-armes/test-means.md` §7 :
  - "41 Fortifiant restaure PV et PM" est faux (Potion S : PV seulement).
  - "39 Essence magique : cinématique de résurrection" est faux. Le Breuvage soigne PV et PM sur place ; la résurrection de la mort appelle ce même départ.
  - "37 Elixir de force restaure les PV" est exact.
- Plan §1.30 D-H-3 : "h1 : les soins `0x24`-`0x29`" se lit `0x24, 0x25, 0x26, 0x27, 0x29`.
- Plan §2 O-H-3 : voir §1.6. Plan §1.26 (E20.e0c) : y ajouter le soin sur place du ramassage (§1.7).

---

## 2. L'oracle (`H1/oracle/`, `bash run_all.sh`, environ deux minutes)

### 2.1 Les vecteurs (`oracle/vec/`)

Format de `e20-h4b/bin/vec/README.txt`, avec en plus `stubs`, `calls`, `class`, `label` (voir `vec/README.txt`).

| Fichier | Fonction | Vecteurs |
|---|---|---|
| `vec_herb_start_80034680` / `lpotion_start_800346F0` / `elixir_start_80034760` / `cureale_start_800347D4` / `spotion_start_80034870` | les cinq départs | 75 / 75 / 47 / 189 / 75 |
| `vec_herb_heal_80032E50`, `water1_heal_80032F84`, `spotion_heal_800330FC`, `lpotion_heal_80033274`, `elixir_heal_800336F0`, `cureale_heal_80033A2C` | les six soins | 301 chacun |
| `vec_dispatch_8003499C` | le répartiteur (autres départs et cinq objets écartés par crochets) | 204 |
| `vec_tryuseitem_8002ED64` | `TryUseItem` | 178 |
| `vec_ss/vec_startselect_8002BAEC` | la fonction par image entière (§1.6) | 31 |

Total des 13 fichiers principaux : 2 649 vecteurs, dont 1 775 "jeu" et 874 "synthétiques". Les synthétiques sont des entrées que le jeu ne produit pas mais qui couvrent une instruction : PV ou PM négatifs ou énormes, argument d'objet hors table, compte 0, table d'inventaire trop petite, `a0` autre que le héros.

Contenu :
- `PVmax` ∈ {1…5, 8…12, 18…26, 30, 49, 50, 51}, autour des seuils 5 et 10 (19, 20, 21, 23, 24, 25) ;
- `PV` de `PVmax − 1` à 0 et au plafond ;
- 7 motifs de réservoir (tous obtenus, plein, premier refusé, dernier refusé, alternés dans les deux phases, au hasard) ;
- 8 graines (0, 1, `0xFFFFFFFF`, `0x80000000`, `0x7FFFFFFF`, `0x12345678` et les deux constantes du pas) plus des graines aléatoires ;
- des positions quelconques ;
- des comptes d'objet 0, 1, 2, 3, 5, 9, 99, `0x7FFF`.

Les crochets sont : le son `0x800490FC`, la création d'effet `0x8003BDD8` (qui rend un pointeur ou 0 et initialise l'objet comme `InitEffect`), et pour le répartiteur seul `0x8004E0F8` et les autres départs.

**Rejeu depuis une mémoire vide** (`replay_h1.py`) :
- Le vrai code MIPS tourne ; la lecture d'un octet non listé est une erreur.
- Les écritures, le retour et le journal des crochets doivent être reproduits à l'octet.
- Résultat : 2 649 sur 2 649, 0 échec.
- Deux générations donnent des fichiers identiques (`out/SHA1SUMS_vectors_keys.txt`).

**Couverture** (instructions des corps de fonction exécutées) :
- 100 % : `TryUseItem` 22/22, répartiteur 76/76, départs 28, 28, 29, 39, 28, soins 39, 94, 94, 78, 79, 194, `UseItem` 31/31.
- Avec les seuls vecteurs "jeu", il manque sept instructions, toutes inatteignables dans le jeu :
  - `0x80032E68` (arrondi d'un `PVmax` négatif) ;
  - le chemin d'erreur de `UseItem` pour un indice hors table (`0x8004E5EC-0x8004E5FC`) ;
  - le chemin "compte nul" de `UseItem` (`0x8004E62C`) : un départ n'est appelé que pour un objet de compte non nul.
- Balayage des délais de chargement R3000 (`loaddelay_scan.py`) : 0 danger dans les 20 corps, donc l'interpréteur et le matériel s'accordent.

### 2.2 Mon modèle et ses mutants (`model_h1.py`, `check_model.py`, `mutants_h1.py`)

Le modèle est écrit de ma lecture des listings, pas de l'émulation. Il ne peut lire que les octets que le vecteur liste. Résultat : 0 écart sur 2 649 vecteurs (retour, ensemble exact des octets écrits, journal des appels).

141 mutants (un changement textuel chacun) :
- minimums 5 et 10, diviseurs, plafond, signe ;
- tirages (deux par étincelle), multiplicateurs `0x20001` et `0x30001`, constantes du générateur ;
- décalage `<< 11`, facteur 448, indices des tables, nombres d'anneaux et d'étincelles, animations 2 et 0, décalages Z ;
- tous les sons et le son 3, consommation absente ou double, ordres ;
- `<` → `<=`, `et` → `ou`, bornes du répartiteur, trous un par un, masque du front, `TryUseItem`.

Verdict : 141 tués par une différence de valeur, 0 par le seul ensemble de lectures, 0 par plantage, 0 survivant.

Six mutants équivalents, prouvés à part et listés dans `out/mutants_out.txt` :
- lecture de la vie non signée ;
- division arrondie vers le bas d'un maximum négatif ;
- les trois bornes `0x62` du répartiteur (redondantes avec la borne de table `0x14`, seule la lecture du mot "en cours" change) ;
- l'ordre des deux tests du Breuvage.

Deux mutants reproduisent les écarts de la décompilation (D-H1-1, D-H1-2) : tués.

### 2.3 Les clés de monde (`oracle/keys/`, `keys_h1.py`, `keys/README.txt`)

**Banc.** Il suit R-EL-4 sous la forme FAITHFUL de `e20-h4d1/critic/c/h4o/fire2_key_c.py` :
- héros en (100, 100) px vers l'est, champ plat, 64 cases écrites avec leur index (`+0 = i`, `+0x48 = −1`) ;
- graine `0x12345678` posée avant le tick 1, Rond au tick 1, un tick par image ;
- vrais `MovePlayer`, `TryUseItem`, répartiteur, départs, soins, `UseItem` et passes.

**Préparation.** L'objet est donné par le vrai `AddOneItemIfUnlocked`, puis équipé. La vie est abaissée d'abord (`+0x14`), les PM sont posés dans les stats.

**Crochet d'effet.** Il est modifié par rapport à celui du banc. Il donne un objet distinct par création, initialisé comme `InitEffect`. Il peut refuser après K créations (réservoir plein).

**30 clés**, suffixe `_pf` (pas de fin d'image ajouté après l'enregistrement de `rng1`), 40 à 70 ticks chacune :

| Famille | Clés |
|---|---|
| Herbe | `hp3of10` (3 → 8), `hp8of10_cap` (→ 10), `hp10of10_refused`, `hp3of24` (+6), `hp3of25_odd` (+6), `hp3of40` (+10), `last_one_then_none`, `pool_full`, `circle_held_30` (un seul usage malgré 30 images de Rond tenu) |
| Potion L | `hp3of10`, `hp1of24`, `hp10of10_refused`, `pool_3_free` |
| Potion S | `hp3of10`, `hp3of30` (+15), `hp3of25_odd` (+12), `hp10of10_refused`, `pool_2_free`, `pool_full`, `two_uses` |
| Extrait | `mp1of4`, `mp0of4`, `mp1of3`, `mp4of4_refused`, `hp3_mp4_refused` (la vie basse ne compte pas) |
| Breuvage | `hp3_mp1`, `hp10_mp1` (PM seulement), `hp3_mp4` (PV seulement), `hp10_mp4_refused`, `pool_5_free` |

**Contenu d'une ligne de tick** :
- PV et PVmax de l'entité ; PV, PVmax, PM, PMmax des stats ;
- compte de l'objet, trois mots d'objet, animation du héros, position ;
- `rng0`, `rng1`, `rng2` (= le pas de fin d'image de `rng1`), `draws` (pas entre `rng0` et `rng1`) ;
- événements dans l'ordre du code : `["sound", id]`, `["effect", [0, 14, anim, x, y, z]]`, avec `refused` pour une création refusée ;
- par création, les champs laissés (`x3c y40 z44 fx54 fy58 fz5c`, signés).

**Contrôles.**
- 1 250 lignes, 0 où l'objet en cours, la phase, le compteur, l'animation ou la position bougent.
- La règle `_pf` (`rng0(t+1) = pas(rng1(t))`) est vérifiée sur chaque paire.
- Deux exécutions donnent des manifestes identiques (`keys/manifest.txt`, `manifest_run2.txt`, `out/keys_compare_out.txt`).
- L'alarme de PV bas (son 400 = `0x190`) précède le soin dans les clés où `PVmax ≥ 5·PV` : Herbe `hp3of24`, `hp3of25_odd`, `hp3of40` ; Potion L `hp1of24` ; Potion S `hp3of25_odd`, `hp3of30`, `two_uses`.

**Valeurs mesurées** :
- Herbe : 3/10 → 8 ; 3/24 → 9 ; 3/25 → 9 ; 3/40 → 13.
- Potion S : 3/10 → 10 ; 3/25 → 15 ; 3/30 → 18.
- Potion L : 1/24 → 24.

### 2.4 Reproduction

`bash oracle/run_all.sh` régénère les listings, les vecteurs, les clés et tous les contrôles dans `oracle/out/`. Lectures hors du dossier, en lecture seule :
- le binaire ;
- `data-extracted/data/map_alundra.json` ;
- `Alundra Remake/Alundra (France)/…/DATA/BALANCE.BIN`.

Python 3.14 avec capstone 5.0.x.

---

## 3. Le point d'atterrissage dans la DLL (HEAD `e8087d74`)

### 3.a Le modèle de PV d'E20.c2a, tel qu'il est
- **Où vivent les PV.**
  - Sur l'entité : `EntityScriptProxy.Hp` et `HpMax` (`Alundra/Entities/EntityScriptProxy.cs:89-90`).
  - `LifePulledFromStats` (`:97`, interne) marque un héros dont la vie a été reprise des stats.
- **Reprise à l'adoption.**
  - `WorldProxy.PullHeroLifeFromStats` (`Alundra/Maps/WorldProxy.cs:2018-2023`), appelée par `AdoptPlayerPawn` après `InitializeBalance`.
- **Miroir vers les stats.**
  - `PlayerManager.EndOfMovePlayer` (`Alundra/Hero/PlayerManager.cs:634-645`) : `SetPlayerHpMax(état, héros.HpMax)` puis `SetPlayerHp(état, ClampToShort(héros.Hp))`.
  - Seulement si `LifePulledFromStats` : un héros "nu" de test a `Hp = HpMax = 0`, l'Herbe y serait refusée (son 3).
- **Précédents d'écriture sur l'entité.**
  - `IncreaseHpMaxAndFill` (`:1875`) et `RestoreLifeAndMagic` (opcode `0xC3`, `:1910`).
  - `RestoreLifeAndMagic` a un repli sur les stats et appelle `SnapDisplayedLifeAndMagic` du HUD. Les potions ne doivent pas caler le HUD (§1.4).
- **Setters des stats.**
  - `SetPlayerHp` (`:1929`), `SetPlayerMpMax` (`:1951`, plafond 4), `SetPlayerMp` (`:1970`, borné à `[0, MpMax]`, identique à `0x8004DEBC`).
  - Il n'y a ni `GetPlayerMp`, ni `GetPlayerMpMax`, ni `DecreaseMp` : on lit `state.PlayerStats.Mp/MpMax` (`Alundra/Session/PlayerStats.cs:37`).

### 3.b L'inventaire
- `GetNumberOfItem` (`:2181`).
- `UseItem` (`:2263`) : identique à `0x8004E5C4` (hors table → 0 avec avertissement, compte 0 → −1, sinon −1 et rend le nouveau compte).
- `SetItemIdFromCurrentItemId` (`:2217`) : la promotion de `0x8004E0F8`.
- `ItemsCount = 99` (`:2033`).
- `GameState.NumberOfItems[2·objet + 1]` (`Alundra/Session/GameState.cs:204`).
- `ItemTables.ItemsProperties` : colonnes emplacement, remplacement, priorité, maximum, icône.

### 3.c Le son
- `IScriptHost.SoundPlayer?.PlaySfx(int)` (`Alundra/Audio/ISoundPlayer.cs:25`).
- Usage du héros : `player.ScriptHost?.SoundPlayer?.PlaySfx(NoAttackSoundId)` (`PlayerManager.cs:982`, constante 3 en `:946`).
- Un identifiant non résolu est ignoré en silence (`SoundPlayer.PlaySfx`).
- `0x31` est déjà utilisé (`NativeEffects.LifeVesselSfx`, `:15`). `0x30` et `0x32` sont à vérifier en T0 (sonde, pas un arrêt).

### 3.d Les effets
- **Modèle.** `NativeEffects.cs` : `CreateLifeVesselEffects` (`:31`), `CreateChargeBurst` (`:134`), `CreateChargeSpark` (`:105`). Point d'observation `CreatedForTests` (`:96`).
- **Mise en file.** Les héros mettent leurs créations en file : `stage.Emit(reservoir => NativeEffects.CreateXxx(reservoir, x, y, z))` avec la position lue tout de suite et le Z du binaire = `PosZ + 1` (ADR-0026 ; `PlayerManager.cs:1014`, `:1101`).
- **Création immédiate.** `EffectStage.CreateNow` (`EffectStage.cs:199`), utilisée par l'aura (E20.f1b).
- **Réservoir.** 128 emplacements ; `Create` rend `null` s'il est plein (`EffectReservoir.cs:101`).
- **À ajouter.**
  - Un créateur de l'herbe, un des étincelles (nombre, animation et `ForceZ` en paramètres), un des anneaux (animation en paramètre) et celui du Breuvage.
  - Rendre internes les tables d'anneaux `BurstTableX/Y`.

### 3.e Les cinq entrées dans les tables de h0a
Au HEAD rien n'existe :
- `TryUseItem` est un bouchon qui rend 1 (`PlayerManager.cs:932`), avec la couture `TryUseItemResultForTests` (`:937`).
- Le pilote est vide (`:1507`) et `CancelItemMode` est vide (`:740`).
- Les 13 sites d'appel sont aux lignes 432, 451, 575, 607, 617, 669, 675, 679, 700, 726, 756, 861, 915.

h0a (§1.30.1) pose la table de départ de `0x1F` à `0x32`, les mots `ItemInUse`/`ItemUsePhase`/`ItemUseCounter` sur le proxy (`EntityScriptProxy.cs`, région de `WeaponRecordPointer` en `:434`) et les coutures D-H-4.

h1 remplace cinq bouchons nommés : `0x24`, `0x25`, `0x26`, `0x27`, `0x29`. Les trous `0x21`, `0x22`, `0x28`, `0x2A` restent des refus du répartiteur.

Fichiers neufs proposés (noms libres, à passer à `tools/dll-structure/check_names.py` : un dossier = un espace de noms, pas de préfixe `Alundra`, noms uniques) :
- `Alundra/Hero/HeroHeals.cs` : les six soins, publics, réutilisés par h4f1/h4f2, E18 et E20.e0c.
- `Alundra/Hero/InstantItems.cs` : les cinq départs `(EntityScriptProxy, uint) → int`, toujours 1. Le départ du Breuvage est aussi l'entrée de la résurrection d'E18.

### 3.f Ce qui existe, ce qui manque

| Existe | Manque |
|---|---|
| PV de l'entité et miroir (c2a) ; `SetPlayerMp` ; `UseItem` ; `GetNumberOfItem` ; `PlaySfx` ; `EffectReservoir.Create` ; `EffectStage.Emit` et `CreateNow` ; `NativeEffects` et `CreatedForTests` ; `GameRandom.Next` ; tables d'anneaux (privées) ; `Give`, `SetItem`, `SetMp`, `Press` d'`ArcRun` (`AlundraArcSupport.cs:151-171`, `:454`) ; alarme de PV bas ; animation 2 de l'effet 14 dans les données | `TryUseItem` réel, la table, les mots d'objet, les coutures (h0a) ; les six soins et cinq départs ; les créateurs d'effets ; une aide de test `SetHp` (les tests de vie font `PlayerManager.SetPlayerHp` plus `Proxy.PullHeroLifeFromStats(Hero)` ou `Hero.Hp = n`, `AlundraHeroLifeTests.cs:121-123`, `:607`) ; un préréglage de recette ; l'adaptateur de vecteurs |

### 3.g L'adaptateur adresses → membres

À verser au README de l'annexe. Une adresse hors de la table arrête la tranche.

| Octets du vecteur | Membre de la DLL |
|---|---|
| `0x80127D30 + 0x14`, `+0x18` (et `0x801E4000 + …` pour `a0` autre) | `EntityScriptProxy.Hp`, `HpMax` (un second proxy pour `a0` ≠ héros) |
| `0x80127D30 + 0x114`, `+0x118`, `+0x11C` | `PosX`, `PosY`, `PosZ + 1` |
| `[0x80176318]` puis `+0`, `+2`, `+4`, `+6` | `PlayerStats.Hp`, `HpMax`, `Mp`, `MpMax` |
| `[0x8017631C] + 4·objet + 2`, `[0x8017638C]` | `NumberOfItems[2·objet + 1]`, `ItemsCount` (0x63 = 99) |
| `0x80098708` | `GameRandom.RandomSeed` (32 bits bas) |
| objets rendus par le crochet | `EffectSlot` créés : `+0x3C/40/44` → `X/Y/Z`, `+0x54/58/5C` → `ForceX/Y/Z` |
| tables `0x80023654`, `0x80023694` | `BurstTableX`, `BurstTableY` |
| `0x80127164`, `0x8012715C`, `0x80127160` (répartiteur) | `ItemInUse`, `ItemUseCounter`, `ItemUsePhase` de h0a |
| crochets `0x800490FC`, `0x8003BDD8`, `0x8004E0F8`, autres départs | `PlaySfx`, `reservoir.Create(false, 14, animation, x, y, z)`, `SetItemIdFromCurrentItemId`, bouchons de h0a |

### 3.h Les sauvegardes de test (`Alundra/SaveGames/TestSaves.cs`)
- Les préréglages d'armes portent déjà les potions (`TestKitItems`, `:68-77` : Herbe ×9 (36), Potion L (37), Extrait (38), Potion S (41)). Leur vie est 4/10 et leur magie 4/4 (`WeaponPreset`, `:94-98`).
- Manquent : le Breuvage de soin (39), un objet du Rond (`Preset.Item` est nul, "no Circle item", `AlundraTestSavesTests.cs:429`) et des PM bas. Avec 4/4, l'Extrait est refusé.
- Une recette de h1 demande donc un préréglage neuf (Q-H1-1).
- L'ordre des préréglages est épinglé (`AlundraTestSavesTests.cs:64-90`).

### 3.i Les tests qui bougent
- Aucun test existant n'appuie sur le Rond avec un objet de soin équipé.
  - Les seuls appuis (`AlundraHeroRunStatesTests.cs:476`, `AlundraHeroSwimBranchTests.cs:211`) sont sur des héros nus sans hôte (D-H-2 : `TryUseItem` rend 1 sans son).
  - `SetItem(36)` (`AlundraArcInventoryHelpersTests.cs:105`) et `ItemId = 0x24` (`AlundraInventoryDirectorTests.cs:668`) ne jouent aucun tick avec le Rond.
- **Liste prédite, à recompter en T0** :
  - aucun test existant pour le code ;
  - avec un préréglage neuf : `AlundraTestSavesTests.TheNinePresets_AreListed_InTheOrderOfTheTable` (nom et épingle) et les lignes de la théorie `EveryPresetName`.
- Les tests que h0a fait bouger (ceux qui ignorent le son 3) ne sont pas ceux de h1.

---

## 4. Plan proposé

À insérer comme §1.30.5, ou §1.32.

### 4.1 E20.h1, les cinq objets instantanés ⏳

**Prérequis.**
- h0a (table de départ, mots d'objet, couture `ItemStartForTests`, test du défaut).
- E20.c2a ✅.
- La fin du chantier de restructuration et Q-R-7.
- Ni g1a, ni g2a, ni f4 à f6, ni e0c.

**But.** Comme l'original : Rond avec l'un des cinq objets équipés soigne dans le même tick, avec ses effets, son son et un exemplaire de moins. Sinon le son 3. L'objet n'est jamais "en cours".

**Décisions.**
- **D-H1-1.**
  - `HeroHeals` : six soins statiques et publics. `Herb`, `Half(minimum)` (eau 1 : 5, Potion S : 10), `Full` (Potion L, eau 2), `Magic`, `Both`.
  - `InstantItems` : cinq départs, réponse 1.
  - Les créateurs d'effets vont dans `NativeEffects` (animation, nombre et `ForceZ` en paramètres).
- **D-H1-2.**
  - Le soin lit et écrit l'entité (`Hp`, `HpMax`). Pas de repli sur les stats, contrairement à l'opcode `0xC3`.
  - Un héros qui n'a pas repris sa vie a 0/0 : "PV < PVmax" est faux.
  - Les PM passent par `SetPlayerMp`.
- **D-H1-3.**
  - Les effets sont mis en file (`Emit`, position et Z = `PosZ + 1` lus tout de suite), comme la rafale et l'étincelle de la charge (f1a).
  - Les tirages ont lieu quand la scène applique l'opération. C'est l'écart d'ADR-0048, déjà accepté : invisible sauf si une touche tire dans le même tick.
- **D-H1-4.** Pas de calage du HUD.
- **D-H1-5.** Le soin sur place du ramassage (`0x80033F00`) revient à E20.e0c.
- **D-H1-6.** Le geste Start + Select revient à E18 (O-H-3 est refermé là).
- **D-H1-7.** Les vecteurs se rejouent par un adaptateur adresses → membres (§3.g). Une adresse sans membre arrête la tranche (règle de D-H4B1-3).

**Périmètre.**
- `Alundra/Hero/HeroHeals.cs`, `Alundra/Hero/InstantItems.cs` (neufs).
- `Alundra/Rendering/Effects/NativeEffects.cs`.
- `Alundra/Hero/PlayerManager.cs` : cinq entrées de la table de h0a, phase B.
- `Alundra/SaveGames/TestSaves.cs` : un préréglage, selon Q-H1-1.
- `Alundra.Tests/`.
- L'annexe `docs/plan-e20-annexe/e20-h1/` (= `H1/` sans caches).
- Ce plan, l'index des ADR, un ADR.

**Hors périmètre.**
- Le cadre (h0a).
- Le geste Start + Select et la garde "objet en cours" qu'il porte (E18).
- Le ramassage sac plein (e0c).
- La résurrection (E18).
- Les tomes (h4).
- Les sons eux-mêmes.
- Le moteur, le convertisseur et les données.

**Partage des phases.**
- **Phase A** (T1) : fonctions, créateurs et tests, inertes en production. Les tests de monde passent par `ItemStartForTests` (D-H-4) et sont verts.
- **Phase B** (T2) : les cinq entrées. Les tests de production (p), sans couture, sont rouges avant.
- Changements visibles en phase B : Rond boit, les cœurs et les points de magie montent, anneaux et étincelles, un exemplaire de moins.

#### Tâches

- **T0, annexe, oracle, sondes, liste prédite** (aucun code).
  - (a) Rejouer `bash run_all.sh` depuis une copie : sorties identiques à celles versées (SHA-1).
  - (b) Verser la table §3.g au README.
  - (c) Sondes de la DLL, prédiction écrite avant ; une sonde contredite est un arrêt :
    - P1 : au banc des tests de monde, un tick sans tirage : le générateur lu après l'image égale `rng0(t+1)` de la clé et le nombre de pas vaut `draws + 1` (règle du bloc du §1.20, point 3).
    - P2 : le Z d'un effet créé au repos vaut `PosZ + 1 + 0x100000` ou `+ 0x80000` (clés `z44`).
    - P3 : un réservoir rempli de 128 − K effets longs donne K créations puis `null` (clés `free_slots`).
    - P4 : le miroir donne le même PVmax aux stats et à l'entité, y compris 24 et 30.
    - P5 : les sons `0x30`, `0x31`, `0x32` sont résolus par la banque.
  - (d) La liste prédite des tests qui bougent (§3.i).
  - (e) h0a est faite : la table, les trois coutures et `ItemInUse` existent.
  - (f) Les noms passent `check_names.py`.
- **T1, phase A, tests puis code.** Chaque cas "sans effet" est accolé à un positif.
  - (v1) Un test par soin (six) : le fichier de vecteurs entier, classes "jeu" et "synthétique", via l'adaptateur. Un écart sur un vecteur le rend rouge.
  - (v2) Un test par départ (cinq).
  - (r-herbe), (r-potionL), (r-extrait), (r-breuvage), (r-potionS) :
    - le positif, avec des montants calculés à la main (Herbe : `PVmax` ∈ {10, 19, 20, 23, 24, 25, 40, 50} à `PVmax − 1` et à 3 ; Potion S : 10, 25, 30 ; plafond ; son ; compte −1 ; l'objet reste équipé à 0 exemplaire) ;
    - accolé au refus (PV ou PM au maximum : son 3, compte et valeurs inchangés) ;
    - (r-breuvage) : les quatre combinaisons PV/PM.
  - (r-nu) Un héros sans reprise de vie : refus.
  - (r-hud) Aucun calage du HUD.
  - (e1) Les effets de chaque objet par `CreatedForTests` : nombre, animation, Z, positions et forces des anneaux calculés à la main depuis les deux tables.
  - (e2) Réservoir à K emplacements libres : K créations, tirages = 2 par étincelle obtenue.
  - (g1) Tirages 0, 0, 0, 16, 8 et état final du générateur égal à `rng1` de la clé.
  - (w1…w30) Un test de monde par clé : `ArcRun` de la carte 14, champ plat, entités de la carte retirées, `Give`, `SetItem`, `SetMp`, vie abaissée, graine posée avant la première image, Rond par `Press`, un tick par image, par la couture de départ de h0a. Le générateur égale la clé `_pf` après chaque image ; les événements égalent la clé (sons dans l'ordre, effets en ensemble).
  - Puis le code.
- **T2, phase B.**
  - Les cinq entrées de la table de h0a, et le préréglage de recette (Q-H1-1).
  - (p1…p5) Un test de production par objet : mêmes clés, sans couture.
  - (p6) Le préréglage se construit et ses épingles tiennent.
  - (p7) La table : les cinq entrées sont les cinq départs, les onze autres (`0x1F`, `0x20`, `0x23`, `0x2B`-`0x32`) restent des bouchons silencieux, les quatre trous (`0x21`, `0x22`, `0x28`, `0x2A`) refusent avec le son 3.
- **T3, ADR, plan, verifiers frais** (deux : l'un sur les vecteurs et l'adaptateur, l'autre sur les clés et la production).
  - Mutants du C# par script, au moins 40, tous tués, repris des 141 du modèle.
  - Familles : minimums 5 et 10, diviseurs, plafond, sons, consommation, `<` → `<=`, `et`/`ou` du Breuvage, deux tirages par étincelle, étincelle sans effet obtenu, indices d'anneau, décalage 11, facteur 448, animation 2 et 0, Z, écriture des stats au lieu de l'entité, calage du HUD, ordre soin/son/consommation.

#### Acceptation
- **Oracle** : vecteurs et clés rejoués à l'identique.
- **DLL** :
  - (v), (r), (e), (g), (w) verts en phase A ; (p1) à (p5) et (p7) rouges avant la phase B, verts après.
  - Suite en Release (copie neutralisée) et en Debug, zéro échec.
  - Tests déplacés ⊂ liste prédite ; `cmp` de la DLL ; traces inchangées ; moteur, convertisseur et données sans diff.
- **Preuve de retrait.**
  - Rendre les cinq entrées de la phase B à leurs bouchons rend rouges exactement (p1) à (p5) et (p7). (v), (r), (e), (g), (w) et (p6) restent verts.
  - Retirer l'appel de `UseItem` d'un départ rend rouges exactement (v2), (r) et (w) de cet objet, et son (p). Il en va de même pour `PlaySfx`.
  - Retirer un effet d'un soin rend rouges (v1) de ce soin, (e1), et les (w) et (p) des objets qui l'appellent.
- **Recette de l'auteur** (non bloquante) : avec le préréglage, Rond puis l'objet voulu : cœurs ou points de magie qui montent, son, étincelles, exemplaire de moins. Rond à pleine vie : le son de refus et rien de consommé.

#### Arrêts, budget, retour arrière
- **Arrêts.**
  - Un test hors de la liste prédite bouge.
  - Un vecteur ou une clé diffère, d'un octet ou d'un tick (aucune tolérance).
  - Une sonde P1 à P4 est contredite.
  - Une adresse de vecteur n'a pas de membre dans la DLL.
  - h0a n'a pas les trois coutures.
  - Un besoin de toucher au moteur.
  - Un nom refusé par `check_names.py`.
- **Budget.** Un `executor` par phase, deux tentatives. `plan-verifier` avant, deux verifiers frais après.
- **Retour arrière.** Rendre les cinq entrées à leurs bouchons ; la phase A reste inerte.

### 4.2 À corriger au plan et aux annexes en même temps
- O-H-3 : à E18, pas à h1.
- D-H-3 : lire `0x24, 0x25, 0x26, 0x27, 0x29`.
- §1.26 (E20.e0c) : ajouter le soin sur place du ramassage (§1.7).
- `h-g-deps.md` §2.1 : 2 tirages par étincelle.
- `test-means.md` §7 : rangées 39 et 41.
- Enveloppe : la ligne "h1 non détaillée".

### 4.3 Questions pour l'auteur
Défauts retenus en mode autonome.

- **Q-H1-1, un préréglage de recette ?**
  - Aujourd'hui les sauvegardes d'armes ont 4/4 de magie, aucun objet du Rond et pas de Breuvage de soin : on ne peut rien tester des potions.
  - **(A, défaut)** un seul préréglage `heal-m14` : carte 14, vie 3/10, magie 1/4, l'Herbe équipée, les cinq objets dans le sac ; on change d'objet par le menu. Coût : un préréglage, deux tests déplacés.
  - **(B)** cinq préréglages, un par objet, rien à changer au menu. Coût : cinq lignes et plus d'épingles.
  - **(C)** aucun : la recette passe par la console des sauvegardes de test. Coût : aucun code, un pas de plus pour toi.
- **Q-H1-2, le geste Start + Select.**
  - Ce que fait le binaire : tenir Start + Select 2 secondes consomme un Breuvage de soin s'il y en a un et tue Alundra ("abandonner").
  - **(A, défaut)** à E18 avec le game over. Rien ne change en jeu d'ici là : aujourd'hui le geste ne fait rien.
  - **(B)** le porter dans une tranche h0b avant E18. Alundra mourrait (0 cœur, elle garde la main, E20.c2a) sans écran de fin. Coût : une tranche de plus et un comportement incomplet.
- **Q-H1-3, un soin ramassé quand le sac est plein.**
  - L'original le boit sur place : une Herbe ramassée avec 9 Herbes rend des cœurs.
  - **(A, défaut)** à E20.e0c, qui porte le ramassage (C255). Rien de visible tant que le butin n'est pas porté.
  - **(B)** l'ajouter à h1 pour le coffre (opcode `0x82`). Coût : toucher `EventProgramRunner` et ses épingles, hors du cadre de h1.
- Les deux écarts relevés (§1.8) sont des défauts de la décompilation, pas de l'original : le binaire tranche et la règle "corriger les défauts avérés" n'a rien à corriger ici.

---

## 5. Limites et inconnues

- **INCONNU.**
  - Le contenu sonore de `0x30`, `0x31`, `0x32` (non lu).
  - Le rôle exact de `0x800335AC`, `0x8003382C`, `0x8003FE94` (appelant de `0x80034108`) et de l'opcode de `0x80041B14` (noms décompilés ou hypothèses seulement).
  - Le comportement de l'effet 14 après sa création (programme de l'effet, rendu) : la DLL l'exécute par son réservoir, non mesuré ici.
- **INFÉRENCE.**
  - La nage, les états d'échelle et de grimpe (§1.5).
  - Le rôle de C255 dans `0x8007C598` (§1.7).
  - L'intention du geste Start + Select (§1.6).
  - Le lien de `0x80041B14` avec l'opcode `0xBB`.
- **Non couvert par l'oracle.**
  - Les tirages d'autres entités dans le même tick que le soin (ordre des tirages, D-H1-3).
  - Plus d'une image par tick : les clés `_pf` ne valent qu'à un tick par image.
- **Crochet d'effet.** Le crochet de `0x8003BDD8` remplace l'allocation réelle : `0x8003B9C4` et `0x8003BB14` sont lus, pas exécutés. Le modèle du réservoir de la DLL est celui d'E19.g (`docs/plan-e19-g1g3-annexe/fxpool.py`), non revérifié ici.

## 6. Fichiers (`H1/`)

- `SHA1SUMS`.
- `oracle/` :
  - `run_all.sh`
  - `bdis.py`, `xref.py`, `tbl.py`, `make_listings.py` (mes listings dans `lst/`)
  - `h1rig.py`, `gen_h1.py`, `replay_h1.py`, `model_h1.py`, `check_model.py`, `mutants_h1.py` (vecteurs, rejeu, modèle, mutants)
  - `gen_startselect.py`, `check_startselect.py` (les sites de garde)
  - `keys_h1.py`, `keys_README.txt` (clés)
  - `states_herb.py` (les 80 états)
  - `loaddelay_scan.py`, `rw_classes.py`
  - `vec/`, `vec_ss/`, `keys/`, `lst/`
  - `out/` : toutes les sorties
  - `h4b_h/`, `h4o/` : copies des bancs des annexes (lecture seule du binaire)
- `report.md` : refusé par l'outil, ce message en tient lieu.