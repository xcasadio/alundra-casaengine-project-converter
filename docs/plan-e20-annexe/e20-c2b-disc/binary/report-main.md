# Rapport binaire pour E20.c2b : la blessure du héros (`ALUN_CD.EXE` France)

## 0. Cadre et conventions

- **Lecture seule.** Je n'ai rien écrit dans le dépôt, rien commité, aucune construction et aucun test `dotnet`.
  - Le binaire est lu avec capstone, via le `bx.py` copié dans mon dossier.
  - Le code de la DLL n'a été lu que par `git show` et `git grep` sur `HEAD`.
  - Les `docs/` et `data-extracted/data/*.json` sont lus directement.
- **Étiquettes.** [FAIT] est une adresse décodée. [MESURÉ] est une sortie du vrai code dans l'émulateur. [INFÉRENCE] est une déduction dont la base est donnée.
- **Provenance par rapport à `docs/plan-e20-annexe/e20-c2/`.**
  - (R) : relu à l'identique.
  - (R+) : relu et complété.
  - (N) : lu à neuf, ou correction.
- **Oracle rejoué.** J'ai copié les sources de `e20-c2/oracle` dans mon dossier et je les ai relancées de là, sans écrire dans `docs/`.
- Racine de mes fichiers : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/c2b-disc/binary/`, notée `<B>/`.

## 1. Synthèse

1. L'entrée `0x80031A68` a exactement deux appelants (F6), tous deux dans `MovePlayer`. Elle ne connaît que deux déclencheurs : `TouchingEntity` (+0x224), posé par la passe de coups, et le mot de case dangereuse (+0x180).
2. Il n'existe qu'**un seul** bloc de blessure, `0x80031C58-0x80031C9C`, partagé par les deux branches. `0x80031C64` n'est pas une entrée « case dangereuse » : c'est le `jal 0x8002F884` (lâcher l'objet porté, mode 2) de ce bloc (F7).
3. Le recul n'est pas une vitesse posée par l'entrée. C'est la vitesse de l'animation `0x31` (256), appliquée par la physique le long de `+0x8C`, puis sa décroissance sous `0x1C`.
4. J'ai mesuré un recul total de **−2 168 832 brut = −33,09375 px**. `c2-binary.md` §2.3 donne −2 138 112, soit −32,625 px : son total omet le dernier tick, T+15 (−30 720) (F20).
5. Deux sources de hurt-lock distinctes existent :
   - `+0x1D0 = 25` est écrit par la passe de coups, et c'est son **seul** écrivain dans l'image (F31).
   - `+0x1CC = 0x78` est écrit par le héros et par 21 sites en tout (F30).
6. Les 28 animations du héros qui portent le bit `0x40` de `AnimFlags` le rendent intouchable par la passe de coups : sauts, chutes, morts, saisie, et les quatre animations de blessure (F34).
7. Le mot de case dangereuse `+0x180` n'est calculé que si `Flags & 0x100` est posé (F41). `MovePlayer` pose ce bit à chaque tick libre, juste avant l'entrée.
8. Des états de blessure sont aussi posés hors de l'entrée : un gestionnaire natif (C89) force `0x31` (F42).
9. Le HUD baisse les cœurs sans aucun son. Le son 8 est joué à la remontée des PV, le son 9 aux PM, et le son 23 vient de l'animation (F36, F37).

## 2. L'entrée `0x80031A68` (listing complet : `<B>/dis_core.txt`)

**F1 [FAIT][R+]** La fonction fait 58 instructions (`0x80031A68-0x80031B4C`). Elle n'écrit que `hero.+0x14` (PV).

```
int DamageEntry():                              // s1 = &hero (0x80127D30), a0 = -1 chargé dans le delay slot de 0x80031A8C
  t = hero.+0x224                               // 0x80031A80  TouchingEntity
  if t != 0:                                    // 0x80031A88
      ComputeNewHp(hero)                        // jal 0x8003A374 @0x80031A90, retour ignoré, écrit hero.+0x14
      return GetDirection(hero.+0x114 - t.+0x114, hero.+0x118 - t.+0x118)   // jal 0x8002E814 @0x80031AAC (j 0x80031B34)
  if hero.+0x1CC != 0           return -1       // 0x80031AC4
  if [0x80127000] >= 3          return -1       // slti 3 @0x80031AD8 (niveau des bottes)
  n = (hero.+0x180 & 0x180) >> 7 ; if n == 0 return -1     // 0x80031AEC-0x80031AF4
  cost = (s16)[0x80028B8C + 2n]                 // octets 00 00 01 00 02 00 03 00 → 0,1,2,3
  hero.+0x14 = (cost < hero.+0x14) ? hero.+0x14 - cost : 0 // slt signé 0x80031B14 ; sw 0x80031B28
  return (hero.+0x8C + 0x10) & 0x1F             // 0x80031B24-0x80031B30
```

**F2 [FAIT][R+]** La branche « toucheur » ne teste ni `+0x1CC`, ni `+0x1D0`, ni les bottes. Elle ne teste pas non plus l'état du héros ni `AnimFlags`. La protection vient donc entièrement de la passe de coups, qui saute une cible dont `+0x1CC` ou `+0x1D0` est non nul (F40).
- Le résultat de `ComputeNewHp` (« PV tombés à 0 ») est jeté. Une réponse d'armure `0x80` ou `0xC0` donne donc une blessure sans perte de PV.

**F3 [FAIT][R+]** La branche « case dangereuse » ne touche ni la fiche d'armure, ni `+0x88`, ni `+0x1CC`. L'armure est ignorée : seul le coût de la table compte.
- Le plancher est 0, avec comparaison signée.
- La valeur renvoyée est la direction du héros retournée de 180°.

**F4 [FAIT][N]** `-1` signifie « rien ». `0` est une direction valide (bas).
- Les trois sorties `-1` de la branche hazard (`0x80031AC4`, `0x80031ADC`, `0x80031AF4`) rendent toutes `move $v0,$a0`, avec `a0 = -1`.

**F5 [FAIT][MESURÉ][R+]** J'ai rejoué `GetDirection` (`0x8002E814`) dans l'émulateur sur des différences brutes en 16.16 (`<B>/s12_getdir.out`). Les différences sont (héros − toucheur), 0 = bas, 8 = gauche, 16 = haut, 24 = droite.
- Axes : (+1,0) donne 24, (−1,0) donne 8, (0,+1) donne 0, (0,−1) donne 16.
- Diagonales : (+1,+1) donne 28, (−1,+1) donne 4, (+1,−1) donne 20, (−1,−1) donne 12.
- **(0,0) donne 24** : un recouvrement exact repousse vers la droite.

## 3. Appelants

**F6 [FAIT][R+]** Scan de toute l'image (`0x80020000-0x800CA800`, `<B>/s1_callers.out`) : `0x80031A68` n'a que deux `jal`, en `0x80031C14` et `0x80031C44`.
- Aucun `j` en queue d'appel, aucun mot de données qui la pointe, aucune matérialisation `lui/addiu` de son adresse.
- `MovePlayer` (`0x80031B50`) n'a qu'un `jal`, en `0x800386EC`. C'est la **première** action de la passe d'événements `0x800386D0`, avant toute autre entité.
- Cette passe est appelée par le répartiteur `0x8003B388` (jal `0x8003B3A8`), une fois par tick, sauf si `[0x800DC4B8] & 0x48`.
- Le répartiteur n'a lui-même qu'un appelant : la mise à jour de frame `0x8002E058`, jal en `0x8002E108`.

**F7 [FAIT][N]** `0x80031C64` est le `jal 0x8002F884` du bloc de blessure, avec `a0 = 2` posé par le delay slot de `0x80031C50` (mode 2 de `UpdatePlayerCarriedEntity`).
- Ce bloc est atteint pour tout retour `≠ -1`, toucheur ou case dangereuse. Il n'y a donc **pas** de « site de blessure par case dangereuse » distinct.
- Le plan (D-G-4, O-G-4) et le commentaire de la DLL (`Alundra/Hero/PlayerManager.cs:1411`) nomment ce même site « blessure ».
- Il y a aussi un seul appel du pilote d'objet (`0x8003634C`) dans ce bloc, en `0x80031C6C`.

## 4. `MovePlayer` autour de l'entrée (ordre vérifié à neuf)

**F8 [FAIT][R+]**
1. `0x80031B70` : `jal 0x8004E030` avec `[0x80127108] = 0` dans le delay slot. Puis `[0x8012710C] = octet [0x80028C14 + résultat]`. C'est la fiche d'arme (E20.b, hors blessure).
2. `0x80031B98` : `s2 = hero.+0x18C`. C'est la pente lue **avant** `CheckAndExecuteWarp`, c'est-à-dire celle du tick précédent. Elle servira au choix `0x3A`.
3. `0x80031B9C` : `jal 0x8002F120` (`CheckAndExecuteWarp`).
4. `0x80031BA4` : si `hero.+0x20 != 0`, alors `[0x80126FFC] = 0`, `[0x80127110] = 0`, puis FIN. **Aucune entrée.**
5. `0x80031BC8` : si `[0x800DC4B8] & 0x34` (branche verrouillée) :
   - `0x8002FB14(1)`, `[0x80126FFC] = 0`.
   - `0x8002F884(1)`, avec `[0x80127110] = 0` dans le delay slot.
   - `0x800350C0()`.
   - Si `[0x800DC4B8] != 0x20` (comparaison du mot entier, `0x80031C0C`), FIN.
   - Sinon `d = 0x80031A68()` ; si `d != -1`, `hero.+0x1CC = 0x78` ; FIN.
6. Branche libre :
   - `0x80031C30` : `hero.Flags(+0x6C) |= 0x100`, puis `0x8002FB14(0)`.
   - `0x80031C44` : `d = 0x80031A68()` ; `s4 = -1` est chargé dans le delay slot.
7. Si `d != -1`, bloc de blessure `0x80031C58-0x80031C9C` (F17), puis FIN.
8. Sinon, `0x80031CA0` : si `hero.+0x14 == 0`, aller à la mort `0x800327A4` (F38). Si `hero.+0x188 & 0x80`, aller à la case de sortie `0x80032768`. Sinon `0x8003634C()`, puis la direction du pad et l'aiguillage de pente. Une pente `≥ 8` va directement à FIN.
9. FIN `0x80032914` : `UpdateItemEffectState`, `SetPlayerHpMax(hero.+0x18)`, `SetPlayerHp(hero.+0x14)`. Toutes les sorties y passent.

**F9 [FAIT][R+] — ce qui supprime l'entrée.**

| Condition | Adresse | Conséquence |
|---|---|---|
| `[0x800DC4B8] & 0x48` | `0x8003B394/98` | `MovePlayer` n'est pas appelé, seule la passe des listes tourne |
| `hero.+0x20 != 0` | `0x80031BAC` | pas d'entrée. Le héros n'est pas non plus dans la liste active (`0x80038560`), donc il n'est pas une cible |
| `ctl & 0x34`, mot ≠ `0x20` exactement (`0x24`, `0x30`, `0x10`, `0x04`, `0xA0`) | `0x80031C0C` | pas d'entrée. Le coup est perdu : `+0x224` est vidé par la passe des compteurs, et `+0x1D0 = 25` est déjà posé |
| mot exactement `0x20` | `0x80031C14` | entrée sans animation, sans direction, sans test de mort, avec `+0x1CC = 0x78` |

**F10 [FAIT][R]** Hors ces cas, **aucun état du héros n'ignore l'entrée** : elle remplace tout (attaque, saut, échelle). Elle passe avant l'aiguillage d'état et avant la pente.

**F11 [FAIT][R+][N pour le corpus]** Le bit `0x20` est posé par `0x80062740` et `0x80075264`, effacé par `0x800621DC`, `0x80062330` et `0x80074DA8`.
- `0x80062740` est dans le gestionnaire natif **C89** (`0x80061EB8`).
- `0x80075264` et `0x80074DA8` sont dans **C48** (`0x80074D00`).
- Corpus [MESURÉ, `<B>/s10_corpus_c89.out`] : le champ `ProgramTick` des sprites vaut 89 pour la carte 471, sprite 208 ; il vaut 48 pour les cartes 99, 123 et 449, sprite 220.
- Atterrissage de C48 : `hero.+0x1CC = 0x78` en `0x80074D98` (R).

**F12 [FAIT][N]** `Flags |= 0x100` n'est posé qu'en branche libre. Ce bit conditionne le calcul du mot dangereux (F41).

## 5. Dégâts et défense

**F13 [FAIT][R+]** `ComputeNewHp(a0)` (`0x8003A374`) :
- La fiche de défense est `0x80127008` si `a0` est le héros (`0x8003A3A0-0x8003A3AC`), sinon `a0.+0x1C4`.
- L'attaque est `touche.+0x1C8`.
- Le résultat de `0x8004464C(attaque, fiche, hp)` est rangé dans `a0.+0x14` (`0x8003A4E4`, `0x8003A518`, `0x8003A528`). La fonction rend `(PV == 0)`.
- Le code gardé par `[0x800DC058] < 0 && [0x800DC05C] & 0x800` n'est que de l'affichage de debug.

**F14 [FAIT][R+]** `0x8004464C` :
- Si l'attaque ou la fiche est nulle, ou si l'octet d'attribut `attaque[0] == 0`, les PV sont inchangés.
- `r = fiche[(attr & 0xF) + 2]`.
  - `r & 0xC0 == 0x40` met les PV à 0 (valeur de retour 0, `0x8004480C`).
  - `r & 0xC0 == 0x80` ou `0xC0` laisse les PV.
- Sinon `puissance = attaque[1]`. Pour `attr & 0x80`, la puissance s'ajoute des octets des 3 fiches d'équipement (`0x80146E00 + 8i`). Cela ne concerne que les attaques du héros.
- `dégâts = (puissance * r) >> 4`, minimum 1 ; `PV = (dégâts < PV) ? PV − dégâts : 0` (`0x80044750-0x80044778`).

**F15 [FAIT][R]** La passe de coups teste l'immunité sur `cible.+0x1C4` (fiche d'équilibre 0, toutes réponses à 0), pas sur `0x80127008` (`0x800395D4-0x800395EC`). Le héros n'est donc jamais « immunisé » à cet endroit.

## 6. Choix de l'état, direction, recul

**F17 [FAIT][R+]** Bloc de blessure, dans l'ordre exact :

| Adresse | Action |
|---|---|
| `0x80031C58` | `[0x80126FFC] = 0` (compteur de charge) |
| `0x80031C60` | `[0x80127110] = 0` (delay slot du `jal` suivant) |
| `0x80031C64` | `jal 0x8002F884` (`a0 = 2`) |
| `0x80031C6C` | `jal 0x8003634C` (pilote d'objet, appelé **avant** de ranger le nouvel état) |
| `0x80031C74-0x80031C84` | `hero.+0x88 = (s2 == 4) ? 0x3A : 0x31` |
| `0x80031C94` | `hero.+0x8C = d` |
| `0x80031C9C` | `hero.+0x1CC = 0x78` (delay slot du `j` vers FIN) |

- `0x800350C0` (annulation de l'objet en cours d'emploi) n'est **pas** appelée ici (R).
- Le tick de la blessure ne passe ni par le prologue `0x8002F49C/0x8002F768/0x8002F884(0)` ni par un gestionnaire d'état : il saute à FIN.

**F18 [FAIT][R]** `0x3A` si la pente lue en `0x80031B98` vaut 4, sinon `0x31`.

**F19 [FAIT][R]** Direction :
- Toucheur : `GetDirection(héros − toucheur)`, donc vers l'extérieur du toucheur.
- Case dangereuse : `(+0x8C + 0x10) & 0x1F`, donc le dos au sens du regard.
- La direction ainsi posée est celle du recul **et** celle de l'animation `0x31`. Elle est retournée de `0x10` à la fin de la blessure (F24).

**F20 [MESURÉ][N pour le total]** Il n'y a pas d'affectation de vitesse dans l'entrée. La physique applique la vitesse de l'animation `0x31` (256) le long de `+0x8C`, puis `0x1C` (vitesse 0) la fait décroître. Sur un héros immobile frappé de face au sol, la force X par tick est :
- T..T+3 : −196 608.
- T+4..T+14 : −184 320, −172 032, −159 744, −147 456, −135 168, −122 880, −110 592, −98 304, −86 016, −73 728, −61 440.
- T+15 : −30 720. Puis 0.
- **Somme −2 168 832 brut = −33,09375 px** : x passe de 232 à 198,90625 (`<B>/s8_A1-pad0.out`).
- (N) `c2-binary.md` §2.3 écrit −2 138 112 / −32,625 px : c'est la somme sans le tick T+15.

**F21 [FAIT][R+][N pour les noms de champs]** Données d'animation du sprite 0 du héros (`data-extracted/data/map_alundra.json`, `<B>/s6_anims.out`).

| Anim | Vitesse | AnimFlags (octet 0xD) | Sfx | Délais (7 bits bas) | Fin |
|---|---|---|---|---|---|
| `0x31` | 256 | `0x40` | 23 | 1,1,1,1 (4 ticks) | terminateur 0 → chaîne |
| `0x1C` | 0 | `0x44` | 0 | 1,2,2,2,2,1 (10 ticks) | terminateur 0 → tenue |
| `0x3A` | 256 | `0x40` | 23 | 1,1,1,1 | chaîne |
| `0x3B` | 0 | `0x44` | 0 | 2,2,2,2 (8 ticks) | tenue |

- Dans le JSON, le champ nommé `Acceleration` est en réalité `AnimFlags`, et `Flags` vaut 0.

**F22 [FAIT][R+]** `UpdateAnimation` (`0x80038AB4`) :
- Il recharge si `+0x88 != +0x90` ou si l'index de direction change. Au rechargement : `+0xA8 = 0`, `+0xAC = 0`, `+0xB4 ← octet 0xD`.
- Il appelle `jal 0x800490FC` avec `Sfx` (octet 0xC, plus `0x100` si `octet0xD & 0x20`) **à chaque rechargement** (`0x80038BC8`), même quand `Sfx = 0`.

**F23 [FAIT][R+]** Ordre des passes (répartiteur `0x8003B388`) : `0x80038634` → événements (`MovePlayer` en tête) → compteurs `0x80038998` → listes `0x800384F4` → animations `0x80038E18` → physique `0x80038364` → ombres `0x80038E84` → coups `0x80039300`.
- Au tick T, l'état `0x31` est donc rangé par `MovePlayer`, puis `+0x1CC` baisse de 0x78 à 0x77 (compteurs), puis les listes calculent le clignotement, puis `0x31` est chargé (son 23), puis la force est appliquée.

## 7. Gestionnaires d'état `0x31`, `0x1C`, `0x3A`, `0x3B`

**F24 [FAIT][R]** Tableau `0x80022D3C`, indexé par l'état lui-même (borne `sltiu 0x50`). Pentes 0 à 3, 5, 7, et 6 après son prélude d'échelle `0x80031D20`.
- `0x31` → `0x80032680` : `TryUseItem()` puis FIN. Il ne lit pas le D-pad et laisse `+0x8C`. La fin de `0x31` se fait **par la chaîne d'animation**, pas par `MovePlayer`.
- `0x1C` et `0x3E` → `0x80032558` :
  - Si `TryUseItem() == 0`, alors `+0x8C ^= 0x10`, FIN (`0x8003270C`).
  - Sinon, si `+0xAC != 1`, FIN.
  - Sinon `+0x8C = (+0x8C + 0x10) & 0x1F` et `+0x88 = (hero.+0x144 != 0) ? 0 : 1` (sol : Idle ; air : 1).
- `0x3A` et `0x3B` → `0x80031DB4` : `+0x8C ^= 0x10`, puis `+0x88 = (+0x144 != 0) ? 0 : 0x2D`, puis `TryUseItem`, FIN. Hors pente 4, cette blessure ne dure donc qu'un tick côté `MovePlayer`.
- Prélude de la pente 6 (R) : sur cette pente, `+0x88` peut être écrasé par `0xE` (échelle) quand les cinq conditions de `0x80031D20-0x80031D64` sont réunies. Cela peut interrompre un état de blessure.

**F25 [FAIT][R]** Pente 4 (`0x8003260C`, table `0x80022E7C`).
- À chaque tick, il fait d'abord `[0x80126FFC] = 0`, `[0x80127110] = 0`, `0x8002F884(2)`.
- `0x1C`, `0x31` et `0x3E` → `0x80032658` : `+0x8C ^= 0x10`, `+0x88 = 0x1D`, `TryUseItem`.
- `0x3A` → `0x80032680`.
- `0x3B` → `0x800326FC` : attend `+0xAC == 1`, puis `+0x8C ^= 0x10` et `+0x88 = 0x1D`.

**F26 [MESURÉ][R+]** Chronologie de la durée (T = tick de blessure).
- Au sol : `0x31` de T à T+3, `0x1C` de T+4 à T+14 avec `+0xAC = 1` à T+14, Idle (état 0) à T+15 avec la direction retournée. Le tick T+15 applique encore −30 720 de force.
- Dans l'eau (scénario C1) : `0x3A` en T, `0x3B` en T+4, `+0xAC = 1` en T+12, `0x1D` en T+13 avec la direction retournée.

**F27 [FAIT][N]** Appui sur Cercle pendant la blessure, pour `TryUseItem` (`0x8002ED64`) :
- Il lit le front `lhu [0x80126F2A] & 0x20`. Si `[0x800DC4B8] == 0`, il appelle `0x8003499C`. Si le mot de contrôle n'est pas nul, il joue le son 3 et rend 1. Sans front, il rend 1.
- Les démarreurs d'objet ne lisent pas tous l'état du héros (tête de chacun lue, pas leur corps) :
  - Aucun test d'état : `0x80034224` (objet 0x1F : `+0x2C`, pente ≠ 4, `[0x80127164]`), `0x8003453C` (0x23, mêmes gardes) et les consommables instantanés `0x80034680`, `0x800346F0`, `0x80034760`, `0x800347D4`, `0x80034870`.
  - Exige l'état `< 2` : `0x800348E0` (objets 0x2B à 0x32).
  - `0x80034320` (objet 0x20) teste l'état `< 2`, la pente 3, et gère un cas « en cours d'emploi ».
- [INFÉRENCE] Un Cercle pendant `0x31`/`0x1C` peut donc, pour les démarreurs sans test d'état, remplacer l'état de blessure par celui de l'objet. C'est du domaine E20.h.

**F28 [FAIT][N]** L'octet de `0x80038D14 + 2×état` (« la charge compte dans cet état ») est non nul pour `0x31` (0x11), `0x1C` (0x7E), `0x3A` (0x7B), `0x3B` et `0x3E` (0x02). Le compteur `[0x80126FFC]`, remis à 0 à l'entrée, compte donc de nouveau dès T+1 si Carré est tenu et l'arme chargeable (`0x8002F49C`). Je n'ai pas vérifié que la table portée par E20.f1a (`AnimationCountsCharge`) contient ces entrées.

**F29 [FAIT][R+]** De T+1 à T+14, le prélude `0x8002F49C`, `0x8002F768` puis `0x8002F884(0)` (`0x80031D68-0x80031D78`) précède le gestionnaire d'état. Le pilote `0x8003634C` (`0x80031CC4`) est aussi appelé à chaque tick normal : un objet en cours d'emploi continue donc après une blessure (aucune annulation, F17).

## 8. Invulnérabilité, hurt-lock, clignotement

**F30 [FAIT][R+][N pour l'inventaire]** Sites qui écrivent un `+0x1CC` (21, `<B>/s3_animvals.out` et scan) :
- Pour le héros : `0x80031C2C` (0x20), `0x80031C9C` (blessure), `0x8002BC2C` (Start+Select), `0x80074D98` (C48).
- Générique : `0x8004002C`, l'opcode `0x86`, qui pose une valeur (n'ajoute pas) à **chaque entité d'une liste** (`0x8003C954`), pas seulement au héros.
- Les 15 autres sites sont des gestionnaires natifs qui écrivent leur propre entité.
- Lecteurs : `0x80031ABC` (garde hazard), `0x8003942C` (passe de coups), `0x800385F0` (liste de dessin), `0x80038EF4` (ombre).

**F31 [FAIT][N]** `+0x1D0` n'a **qu'un seul écrivain** dans toute l'image : `0x80039670`, valeur 25 (0x19), sur la cible, dans la passe de coups. Il est décrémenté par la passe des compteurs. Aucune autre source ne le pose.

**F32 [FAIT][R]** Passe des compteurs (`0x80038998`), par entité de 0 à `[0x80134360]` sans filtre de statut :
- `+0x1C++`.
- `+0x1CC--` s'il est non nul.
- `+0x1D0--` s'il est non nul.
- `+0x224 = 0`, `+0x220 = 0` et `+0x12C = 0`.
- Elle est sautée quand `ctl & 0x48`, donc les compteurs sont gelés pendant une pause.

**F33 [FAIT][R]** Passe des listes (`0x800384F4`) : la liste de dessin `0x80134250` (compte `0x8013435C`) prend toute entité de statut 2 ou 3 **sans** test de `+0x20`, avec `(+0x1CC & 3) != 3` (`0x800385D0-0x8003860C`).
- Le clignotement est donc **générique** : toute entité qui reçoit un `+0x1CC` clignote.
- Consommateurs du compte : `0x800399B8` (clés de profondeur) et `0x8003B484` (soumission des paquets).
- La passe des ombres (`0x80038E84`) libère l'ombre `+0x1B8` sur le même test (`0x80038EEC-0x80038F14`).
- La passe des listes tourne aussi sous `ctl & 0x48`.

**F34 [FAIT][N]** La passe de coups ignore toute cible avec `AnimFlags & 0x40` (`0x8003943C`). Animations du héros concernées, pour `ctl` et `+0x1CC` à part (`<B>/s6_anims.out`, scan des 94 jeux d'animations) :
- Sauts et chutes : `0x1A`, `0x1B`, `0x1E`, `0x20`, `0x21`, `0x22`, `0x23`, `0x24`, `0x27`.
- Blessures : `0x1C`, `0x31`, `0x3A`, `0x3B`, `0x3E`.
- Morts : `0x32`, `0x33`, `0x34`, `0x36`, `0x37`, `0x39`, `0x3C`, `0x4E`, `0x4F`.
- Saisie : `0x56`, `0x58`.
- Autres : `0x5B`, `0x5C`, `0x5D`.
- Le héros qui saute n'est donc pas touchable par un toucheur. La branche « case dangereuse » ne lit pas `AnimFlags`.

**F35 [MESURÉ][R]** Phase du clignotement : `+0x1CC` vaut 119 au tick T (0x78 posé, puis décrémenté), et `119 & 3 == 3`. Le héros est caché aux ticks T, T+4, T+8, …, T+116, soit 30 sur 120.

## 9. Son et HUD

**F36 [MESURÉ][R+]** La blessure ne joue qu'un son, le **23**, au chargement de l'animation (`0x31` ou `0x3A`). L'entrée ne joue rien (F22).
- La passe de coups ne joue pas non plus de son pour le héros : le journal du tick de contact ne montre que trois étincelles (effet 9). Les effets 4 et 5 sont ceux des attributs 6 et 10, et 7 et 9 (E20.f), voir F40.
- Le son 23 vient de l'`Sfx` de l'animation : la DLL a déjà le son du changement d'animation (E19.t), non vérifié ici pour `0x31`/`0x3A`.

**F37 [FAIT][N, correction de c2-binary §6]** Rafraîchissement des cœurs du HUD (`0x8004C210-0x8004C440`) :
- La baisse affichée (PV affichés > PV réels) décrémente d'un cœur tous les 4 ticks, **sans appel de son** (`0x8004C280-0x8004C2B0`).
- Le **son 8** est joué quand les PV affichés remontent (`0x8004C2F8`).
- Le **son 9** appartient à la barre de PM (`0x8004C41C`, après `GetPlayerMp`).
- `c2-binary.md` §6 écrit « son 9 aux incréments et son 8 aux autres », ce que ce décodage contredit.
- Dans la DLL, `Alundra/UI/Hud/HudDirector.cs:556` dit que ces sons ne sont pas portés.

## 10. Mort (renvoi E18)

**F38 [FAIT][R+][MESURÉ]** Seuil : `hero.+0x14 == 0` (`0x80031CA8`), testé **seulement** quand l'entrée a rendu `-1`.
- Un coup mortel joue donc la blessure en entier, et la mort commence au tick suivant.
- Table `0x80022FBC` (indice = état − 0x1C) :
  - `0x1C` → `0x80032898` : pente 4 → `0x3C` ; sinon, une fois `+0xAC` posé → `0x39`.
  - `0x31` et `0x39` → `0x80032870`.
  - `0x3A` et `0x3C` → `0x80032884`.
  - `0x3B` → `0x800328CC`.
  - `0x4E` et `0x4F` → `0x800327F0`.
  - Sinon → `0x800328F8`.
- Chronologie mesurée (scénario E1, 1 PV) : PV 0 en T = 2, `0x1C` à partir du tick 6, `0x39` au tick 17 (T+15), `0x4E` au tick 67, `+0xAC = 1` au tick 75, **`ctl = 0x4` au tick 76** (`<B>/s8_E1.out`).
- Le héros se déplace un peu plus pendant `0x39` (−49 152 au tick 17).
- L'entrée de la branche de mort exécute `[0x80126FFC] = 0`, `[0x80127110] = 0`, `0x8002F884(2)` (site `0x800327AC`), puis `0x800350C0`.

**F39 [FAIT][R]** Start+Select tenu 0x78 ticks (compteur `0x800986F0`, pad `0x900`) pose `Hp = 0` et `+0x1CC = 0x78` (`0x8002BC28-0x8002BC2C`), via `SetPlayerHp(0)`. C'est dans E18.

## 11. Qui produit le coup

**F40 [FAIT][R+][N pour le détail]** Passe de coups (`0x80039300`), vérifiée à neuf :
- **Attaquant** : entité de la liste active avec `+0x1D4 != 0` (boîte), `+0x1C8 != 0` et `attaque[0] != 0`. Son masque de classe vaut `fp = ((F>>2)&1) | ((F<<2)&8) | (F&0x1000 ? 0x800 : 0)`, avec `F` ses `Flags`, et ne doit pas être nul.
- **Cible** : toute autre entité de la liste, qui doit avoir `+0x1D0 == 0`, `+0x1CC == 0`, `AnimFlags & 0x40 == 0` et `Flags & fp != 0`.
  - Le héros a `Flags = 0x3118C` [MESURÉ], donc la classe `0x8`. Seuls les attaquants dont les `Flags` portent le bit `0x2` le touchent.
  - Les boîtes se recouvrent sur les trois axes (`+0x1D8..+0x1E0`, `+0x1F0..+0x1F8`).
- Sur recouvrement : si la réponse de la fiche `cible.+0x1C4` est `0x80`, on saute les effets et l'écriture de `TouchingEntity`. Sinon effet 4 pour l'attribut 6 ou 10, effet 5 pour 7 ou 9, puis **`cible.+0x224 = attaquant`** (`0x80039668`, seul site d'écriture).
- Dans tous les cas : `cible.+0x1D0 = 25`, `attaquant.+0x220++`, étincelles à `0x80039740`.
- `TouchingEntity` n'est vidé qu'à la fin du tick suivant (compteurs).
- Quatre gestionnnaires natifs lisent aussi `hero.+0x224` pour savoir si « ils ont touché le héros » (`0x800667F8`, `0x80066E58`, `0x8006F8B8`, `0x8007D2B0`) [N].
- Ennemis, projectiles et explosions suivent ce même chemin [INFÉRENCE : ce sont des entités avec `+0x1C8`/`+0x1D4`]. Je n'ai pas relu l'enregistrement d'attaque d'une bombe ni d'un projectile.
  - Le plan donne « le héros du kit perd 5 PV sur 10 », compatible avec `(5 × 16) >> 4 = 5`.

**F41 [FAIT][N]** Mot de case dangereuse (`0x80038064`) :
- Il n'est calculé que si `Flags & 0x100` (`0x80038104-0x80038108`). Sinon `+0x180 = +0x184 = 0` (`0x80038274-0x80038284`).
- Pour chaque coin (4), le mot de cellule ne compte que si `hauteur du coin + 1 == hero.+0x1E0`. Un coin non aligné donne 0 et remet la borne de pente à 0.
- `+0x180` est le OU des quatre valeurs, `+0x184` le ET. La pente `+0x18C` est le minimum de `(mot & 0xE00) >> 9`.
- Le coût vient donc de `mot & 0x180`. En l'air, aucun coin ne correspond : pas de blessure par case.
- Le mot est écrit en fin de tick (passe physique), la blessure arrive donc au tick 2 (c2-binary R).

**F42 [FAIT][N, E14]** Écritures de l'état du héros hors de l'entrée :
- Gestionnaire natif **C89** (`0x80061EB8`, carte 471 sprite 208), en `0x80062938-0x80062988` : `self.+0x88 = 5`, `s1[0x18] = 2`, puis `hero.+0x88 = 0x31`, `hero.+0x8C = 0`, `hero.Flags |= 0x108` et copie de X, Y de l'entité vers le héros. Aucune perte de PV.
- C48 : états `0x58` et `0x1CC = 0x78`.
- Écritures de PV du héros : régénération `0x80030D28`, `0x80041D84` (0xC3), `0x800333AC`/`0x80041C00` pour PV max, création `0x80031A08`, Start+Select `0x8002BC28`.
- Le scan ne trouve aucune écriture directe de `hero.+0x14` dans les gestionnaires natifs. Je n'ai pas scanné les opcodes de script qui écrivent un champ arbitraire.

## 12. Rejeu de l'oracle `e20-c2` (scénario `A1-pad0`)

Scénario : héros au repos, face à un Beannoïde de la carte 14 placé 20 px à l'est, kit neuf (armure `0x10`), pad 0, PV 10/10. L'attaque du toucheur est (attribut 1, puissance 1), ses `Flags` valent `0x103`. Dégâts : `max(1, (1×16)>>4) = 1`. Les valeurs sont celles de l'état à la fin de chaque tick, du vrai code (`<B>/s8_A1-pad0.out`).

| Tick | `+0x88` | `+0x8C` | x (px) | ForceX | `+0x1CC` | `+0x1D0` | PV | `+0xAC` | Dessiné | Journal |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0 | 24 | 232 | 0 | 0 | 0 | 10 | 0 | non | départ |
| 1 | 0 | 24 | 232 | 0 | 0 | 25 | 10 | 0 | oui | `TouchingEntity` = slot 1, 3 étincelles (effet 9) |
| **2** | **0x31** | **8** | 229 | −196 608 | **119** | 24 | **9** | 0 | **non** | son 23 |
| 3 | 0x31 | 8 | 226 | −196 608 | 118 | 23 | 9 | 0 | oui | |
| 4 | 0x31 | 8 | 223 | −196 608 | 117 | 22 | 9 | 0 | oui | |
| 5 | 0x31 | 8 | 220 | −196 608 | 116 | 21 | 9 | 0 | oui | |
| 6 | 0x1C | 8 | 217,1875 | −184 320 | 115 | 20 | 9 | 0 | non | son 0 |
| 7 | 0x1C | 8 | 214,5625 | −172 032 | 114 | 19 | 9 | 0 | oui | |
| 8 | 0x1C | 8 | 212,125 | −159 744 | 113 | 18 | 9 | 0 | oui | |
| 9 | 0x1C | 8 | 209,875 | −147 456 | 112 | 17 | 9 | 0 | oui | |
| 10 | 0x1C | 8 | 207,8125 | −135 168 | 111 | 16 | 9 | 0 | non | |
| 11 | 0x1C | 8 | 205,9375 | −122 880 | 110 | 15 | 9 | 0 | oui | |
| 12 | 0x1C | 8 | 204,25 | −110 592 | 109 | 14 | 9 | 0 | oui | |
| 13 | 0x1C | 8 | 202,75 | −98 304 | 108 | 13 | 9 | 0 | oui | |
| 14 | 0x1C | 8 | 201,4375 | −86 016 | 107 | 12 | 9 | 0 | non | |
| 15 | 0x1C | 8 | 200,3125 | −73 728 | 106 | 11 | 9 | 0 | oui | |
| 16 | 0x1C | 8 | 199,375 | −61 440 | 105 | 10 | 9 | **1** | oui | |
| **17** | **0** | **24** | 198,90625 | −30 720 | 104 | 9 | 9 | 0 | oui | son 0 |
| 18 | 0 | 24 | 198,90625 | 0 | 103 | 8 | 9 | 0 | non | |

- Les stats suivent au même tick : 9/10 dès le tick 2.
- `+0x1D0` atteint 0 au tick 26. `+0x1CC` atteint 0 au tick **121** (non montré en détail).
- Le héros ne revient pas dans la zone d'un second contact dans ce scénario (reculé de 33 px).

Bornes mesurées par d'autres scénarios de la même copie :
- **Seconde blessure** (contact maintenu, `A6-keep`) : toucher au tick 121 (`+0x1CC = 0`, `+0x1D0 = 25`), blessure au tick **122** (PV 9 → 8), puis aux ticks 241 et 242 (PV 8 → 7) (`<B>/s8_A6-keep.out`). Soit une blessure toutes les 120 ticks.
- **Case dangereuse** (`B1-080-kit`, coût 1, sans bottes) : blessure au tick 2 (PV 10 → 9, `0x31`, direction 24 → 8, `+0x1CC = 119`), identique au toucheur pour le recul, et une seconde au tick 122 (`<B>/s8_B1-080-kit.out`).
- **Eau** (`C1-bean`) : `0x3A` au tick 2, `0x3B` au tick 6, `+0xAC` au tick 14, `0x1D` au tick 15 avec la direction à 24 ; le héros se déplace (−3 px par tick sur 4 ticks) (`<B>/s8_C1.out`).

## 13. Déjà relu dans `e20-c2`, lu à neuf, et corrections

**Déjà relu à l'identique ou complété (R, R+).** F1 à F3, F6, F8 à F11, F13 à F15, F17 à F19, F21 à F26, F29, F32, F33, F35, F38, F39. Tous sont conformes à `c2-binary.md` §1, §2, §3, §4, §7 et à `check.md` (aucun écart de fond).

**Neuf (N) ou à corriger.**
- **F7** : `0x80031C64` est le lâcher d'objet (mode 2) du bloc commun, pas une entrée hazard.
- **F20** : recul total corrigé à −33,09375 px.
- **F27** : les démarreurs d'objet et l'état de blessure.
- **F28** : la charge compte pendant la blessure.
- **F31** : `+0x1D0` a un unique écrivain.
- **F34** : liste des 28 animations intouchables.
- **F37** : sons du HUD (correction de `c2-binary.md` §6).
- **F40** : détail de la passe de coups, les 4 lecteurs de `hero.+0x224`, et `Flags` du héros et du Beannoïde mesurés.
- **F41** : condition `Flags & 0x100` du mot dangereux.
- **F42** : C89 (carte 471) et C48 (cartes 99, 123, 449) identifiés par le corpus.
- **F5** : valeurs de `GetDirection` aux cas limites, dont (0,0) → 24.

**Point déjà tranché par l'oracle.** `c2-binary.md` U3 (« le héros ne se déplace pas pendant `0x3A` ») est infirmé par l'oracle `C1` de `e20-c2`, que je retrouve (F26).

## 14. Ce qui manque et questions ouvertes

**Non lu ou non vérifié.**
- Les corps des démarreurs d'objet (F27) : je n'ai lu que leurs gardes initiales.
- La physique de la décroissance du recul : je m'appuie sur la mesure d'oracle, pas sur le décodage de `0x80036830` et suivants. L'arrêt « force ou position différente de l'oracle » de `check.md` §c reste une question de physique, pas de c2b.
- Le côté DLL : je n'ai pas vérifié que `AnimationCountsCharge` contient `0x31`, `0x1C`, `0x3A`, ni que le son 23 sort de l'animation.
- Les enregistrements d'attaque d'une bombe et d'un projectile.
- L'identité fonctionnelle de C89 (carte 471 sprite 208) et de C48, qui relèvent de E14.
- La boucle de transition de carte : seul le répartiteur appelé par `0x8002E058` est lu. `0x8002F120` appelle `0x80031340(.., 0x36)` pour poser une demande de transition dans la zone `0x800DC4C0+`, mais je n'ai pas décodé le gel qui suit.
- Les opcodes de script qui écriraient un champ du héros.

**Questions ouvertes, à trancher par l'auteur ou par la suite du plan.**
1. PV à 0 : afficher 0 cœur et garder la main (cut D-E20-5, la branche `0x80031CA8` laissée non portée), ou un plancher à 1 ? (U1 de `c2-binary.md`.)
2. Clignotement générique ou propre au héros ? Le binaire est générique (F33), et `Q-E0C-3` (objets lâchés) demande la même règle.
3. Cercle pendant la blessure : reproduire le comportement fidèle (démarreurs sans test d'état, F27) ou attendre E20.h ?
4. Les trois résidus du plan : le pilote `0x8003634C` et `UpdatePlayerCarriedEntity(2)` restent des bouchons nommés (D-E20-14) ; le soin `0xC3` et `0xBC` est livré par c2a ; la branche de l'eau dépend d'E20.i, déjà faite.

## 15. Fichiers (tous sous `<B>/`)

- Outils : `bx.py` (copie), `mx.py` (scanner d'image : appelants, données, accès mémoire).
- Scans et sorties : `s1_callers.py` / `.out` ; `s2_herowrites.py` / `.out` ; `s3_animvals.py` / `.out` ; `s5b_tables.py` / `.out` ; `s6_anims.py` / `.out` ; `s7_readers.py` / `.out` ; `s9_ctl.py` / `.out` ; `s10_corpus_c89.py` / `.out` ; `s12_getdir.py` / `.out`.
- Rejeu de l'oracle : `s8_replay.py`, avec les sorties `s8_A1-pad0.out`, `s8_A6-keep.out`, `s8_B1-080-kit.out`, `s8_C1.out`, `s8_E1.out` ; `s8b_extras.py` avec `s8b_extras_A1-pad0.out` et `s8b_toucher.out`.
- Copie des sources de l'oracle : `<B>/oracle/` (lancées de là, rien écrit dans `docs/`).
- Listings bruts des plages citées : `<B>/dis_core.txt`, généré par `s11_dumpcore.py`.
- Documents de base relus : `docs/plan-e20-annexe/e20-c2/c2-binary.md`, `c2-oracle.md`, `check.md`, `oracle/notes.md`, `oracle/out/A1.txt`, et les sections E20.c2-0 et E20.c2a de `docs/plan-e20-heros.md`.