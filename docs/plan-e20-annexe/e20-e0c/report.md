# E20.e0c — butin (loot), objet ramassable : lecture du binaire, modèles, état de la DLL

HEAD du dépôt `2010ec3`. `ALUN_CD.EXE` (France) a bien le SHA-1 `a705f8da…`. Je n'ai rien écrit hors du dossier de travail :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/e0c/`
(désigné `$S` plus bas). Les étiquettes sont FACT (désassemblé ou rejoué), INFERENCE ou UNKNOWN.

## 0. Résumé

- **Tout le butin tient en trois fonctions et un tirage à la création.** `ChooseRandomlyAnItem` (`0x80032968`) fait un pas du générateur par maillon. `SpawnRandomLoot` (`0x80032C7C`, un seul appelant : la coupe d'herbe) et `SpawnContents` (`0x80032B90`, trois appelants) font naître l'objet. Le tirage du contenu se fait à la création de chaque entité (`0x80032A40`), pas à la mort.
- **Tout le ramassage passe par le gestionnaire C255 (`0x8007C174`)**, qui est le seul appelant de `GiveItem` (`0x80033F00`) et d'`ApplyItem` (`0x80033DBC`). A255 (`0x80061BD4`) n'initialise que les objets posés sur la carte et le coffre. Le même C255 sert aussi les 151 objets posés sur les cartes, avec message et fanfare (§1.6).
- **La DLL a déjà le tirage et la table de tirage** (octet pour octet identique au binaire). Elle n'a ni le lâcher, ni le veto MP, ni C255/A255, ni `GiveItem` ; le bouchon de `DestroyEntity` est `AlundraWorldProxy.cs:3269`.
- **Trois manques de la DLL à ne pas oublier.** `ModdedPos` (`+0x1D8..`), que C255 lit pour le contact, n'est écrit qu'à la création. Aucun rendu ne cache une entité selon `(DamagedTickCounter & 3) == 3`. `AlundraItemTables` ne garde que la colonne 2 de `item-drop-properties.json`, alors que le lâcher et le ramassage ont besoin des colonnes 0 et 1.
- **Cinq fonctions et le gestionnaire C255 sont modélisés en Python et comparés au vrai code** : 0 écart sur des dizaines de milliers de vecteurs, couverture complète, mutants tués (§2).

## 1. Faits du binaire

### 1.1 `ChooseRandomlyAnItem(code)` — `0x80032968`..`0x800329FC`, 38 instructions

- FACT : tant que `code < 0x100` (comparaison signée, `slti`) :
  - si `code & 0x80` : `s = s*0x7D2B89DD + 0xE06A02E7` sur le mot `0x80098708`, puis `code = table[(code&0x7F)*16 + (s>>28)]` ;
  - sinon : retour `code` si `code < 0x62`, sinon 0.
  - `code >= 0x100` rend 0 ; un code négatif passe la garde (sans effet en pratique).
- FACT : un pas du générateur par maillon, donc 0, 1 ou 2 pas avec la table réelle. Table `0x80028E0C`, 128 lignes de 16 octets, md5 `7c6b97ac…`. La table de la DLL (`AlundraLootRoll.cs:108`) lui est identique octet pour octet (vérifié).
- FACT : les seules lignes non nulles sont celles des codes `0x81-0x83` et `0xF6-0xFF`. Distributions exactes (`$S/t_table.py`) :

| Code | Distribution | Pas moyens |
|---|---|---|
| `0x80` | rien, après 1 tirage | 1 |
| `0x82` | rien 11/16, `0x54` 3/16, `0x55` 1/8 | 1 |
| `0xF7` | rien 1/2, `0x24` 1/2 | 1 |
| `0xF8` | `0x45` 1/8, `0x46` 1/8, `0x47` 1/8, `0x48` 1/16, `0x51` 1/4, `0x54` 1/8, `0x55` 1/8, `0x56` 1/16 | 1 |
| `0xFA` | `0x45` 15/64, `0x46` 21/64, `0x47` 3/16, `0x48` 1/4 | 2 |
| `0xFB` | rien 9/16, `0x45` 19/256, `0x46` 19/256, `0x47` 5/128, `0x54` 3/16, `0x55` 1/16 | 23/16 |
| `0xFC` | rien 11/16, `0x45` 19/256, `0x46` 19/256, `0x47` 5/128, `0x54` 3/32, `0x55` 1/32 | 21/16 |
| `0xFF` | rien 7/8, `0x45` 9/256, `0x46` 5/256, `0x47` 1/128, `0x54` 3/64, `0x55` 1/64 | 9/8 |

Les codes `0x81` (14 objets possibles), `0x83`, `0xF6`, `0xF9`, `0xFD` et `0xFE` existent dans la table mais aucune entité posée ne les porte (§4).

### 1.2 Tirage à la création : `InitializeContents` `0x80032A40`..`0x80032B24`

- FACT : unique appelant `0x80032A40` ← `0x80039F38`, fin du noyau d'initialisation d'entité `0x80039D04`. Ce noyau a 5 appelants : `0x800319EC`, `0x8003A014` (`SpawnWarpEntity`), `0x8003A18C`, `0x8003A33C`, `0x8003AC78`.
- FACT :
  - avec enregistrement de carte (`+0x44`) : `f = u16[rec+0x10]`, mis à 0 si `(f&0x7FFF) >= 0x800` ; `+0x40 = f` ; si `f != 0` et son bit est levé (banque `0x801EBA40` si `f&0x8000`, sinon `0x801EB344`, mot `(f>>5)&0x3FF`), `code = 0` ; sinon `code = u8[rec+0x12]`, ou à défaut `u8[+0x64->+0x1F]` (octet `Contents` du en-tête) ;
  - sans enregistrement : `+0x40 = 0` et `code = u8[+0x64->+0x1F]` ;
  - puis `+0x3C = Choose(code)`.
- FACT : `+0x3C` n'est jamais remis à 0 par le lâcher ; 12 `sw zero,0x3C` (`0x80062E08`, `0x80064488`, `0x800646C4`, `0x800650C4`, `0x8006780C`, `0x8006787C`, `0x80067D40`, `0x8007527C`, `0x8007C8B4`, `0x8007CA5C`, `0x8007CB74`, `0x8007D0C4`) suivent des `SpawnWarpEntity` de gestionnaires : INFERENCE, ils retirent le butin des enfants créés par un ennemi (E14.b).
- FACT, mort apparente : `0x80032AB8` (arme négative d'un `bgez` sur un `lhu`) n'est jamais exécutée.

### 1.3 Veto MP `0x80032A00`

- FACT : pour les objets `0x26`, `0x51` et `0x52` seulement, le lâcher est annulé si `MpMax == 0`. `MpMax` est `lh [[0x80176318]+6]` (`0x8004DDDC`).
- FACT : dans les données, seul `0x51` est atteignable (ligne `0xF8`, 42 objets posés).

### 1.4 `SpawnRandomLoot(code, X, Y, Z)` — `0x80032C7C`..`0x80032D70`

Un seul appelant : la coupe d'herbe, `0x8003A988`.

```
item = Choose(code)                      // le générateur avance même si on abandonne ensuite
if item == 0 return 0
if veto(item) return 0                   // §1.3
e = SpawnWarpEntity(parent 0, page 0 (globale), type item+0x1E, X, Y, Z, direction 0)   // 0x80039F58
if e == null return 0                    // table pleine : pas de son, pas de retour en arrière sur le générateur
e.+0xB8 = 0xA0000                        // ForceZ, poussée de 10 px/tick
e.+0x274 = 1  (mot)                      // « objet lâché » : A255 rend la main tout de suite
e.+0x6C &= ~0x80                         // plus Collidable
e.+0x278 = (table[item].octet4 != 0) ? 600 : -1
e.+0x27C = 0 ; e.+0x280 = 0 ; e.+0x284 = 0xA0000
PlaySound(0x54) ; return 1
```

### 1.5 `SpawnContents(e)` — `0x80032B90`..`0x80032C78`

- FACT : même corps que `SpawnRandomLoot`, avec trois différences :
  - `item = e.+0x3C` ;
  - `(X,Y,Z) = e.+0x114 / +0x118 / +0x11C` ;
  - `+0x280 = e.+0x40`, le drapeau du conteneur, que le ramassage lèvera.
- FACT : trois appelants.
  - `DestroyEntity` `0x8003A59C` (appel `0x8003A5B0`, **première** instruction utile, avant l'état 4).
  - La récursion `0x8003A648` (appel `0x8003A6D0`).
  - Le gestionnaire C6, appel `0x800670F8`.
- FACT, C6 (mimique, type de carte 148, 22 posés, `Contents 0xFA`) : dans son état 6, si `((lcg(s))*4)>>32 == 0` (1 chance sur 4), il appelle `SpawnContents(soi)` puis repasse à l'état 0 avec une temporisation de 30. Il peut donc cracher plusieurs fois.
- FACT : `DestroyEntity(e, a1)` fait dans l'ordre :
  1. `SpawnContents(e)` ;
  2. `+0x10 = 4` ;
  3. `+0x228 = -1` ;
  4. libération de l'effet actif `+0x1B8` (`+0x68 = 0`, `+0x1B8 = 0`) ;
  5. si `a1 == -1`, `a1 = u8[+0x64->+0x1E]` ; si `a1 != 0`, `0x8003BE74(0, a1, 0, e, 1, 0, 0, 0)` ;
  6. `[+0x28]->+0x2C = 0`.
- FACT : `0x8003A774` (« destruction légère ») fait seulement les étapes 2, 3, 4 et 6. Aucun butin, aucun effet. Elle a 32 appelants, dont C255 (fin de vie et ramassage).
- FACT, récursion `0x8003A648(e, a1)` :
  - le test d'état `1 ≤ e.+0x10 ≤ 3` porte sur **la cible** (`lw 0x10(s2)`), pas sur le candidat ;
  - pour chaque emplacement `i` de 0 à `[0x80134360]` inclus dont `+0x0C == e`, elle se rappelle ;
  - puis elle détruit `e` avec la même suite qu'`0x8003A59C`.
  - Les enfants sont détruits d'abord, dans l'ordre des emplacements, un enfant déjà mort (état 4) l'est de nouveau ; chaque destruction lâche le `+0x3C` de l'entité.
  - Un cycle de parenté ne finirait jamais (comme dans le jeu).
  - Elle sert le gestionnaire E11 (`0x8007F25C`), enregistré pour 7 types de boss : 509, 476, 418, 488, 479, 446, 463.
- FACT : la décompilation (`FunctionTypeE.cs:291-345`) est fidèle sur ce point.

### 1.6 Gestionnaire C255 `0x8007C174`..`0x8007C764` (381 mots) et A255 `0x80061BD4`

C255 est enregistré comme tick pour 87 types globaux (31 à 118, sauf 72). A255 l'est pour 88 types (les mêmes plus le coffre 30). Aucun enregistrement n'existe côté carte. `item = type − 0x1E`.

**Branche objet lâché ou posé (`+0x274 != 2`)** :

```
si +0x144 (IsOnGround) != 0 et +0x284 != 0 :
    v = (+0x284 * 12) >> 4            // signé, 32 bits
    +0x284 = v ; si !( (u16[[0x800E4334]+4] << 8) < v ) alors +0x284 = 0
    +0xB8 = +0x284
d = +0x278
si d ∉ {-1, 0} : d -= 1 ; +0x278 = d ; si d == 0 → destruction légère ; return
                 si d == 0x78 : +0x1CC = 0x78            // clignotement des 120 derniers ticks
selon +0x27C :
  0 : si +0xB8 > 0 return ; sinon +0x27C = 1                // attend la fin de la montée
  1 : ramassage (ci-dessous)
  2 : si CdBusy() return ; +0x27C = 3 ; [0x80149CBC] = 3 ; 0x80049AF4()
  3 : si dialogue ouvert return ; Unblock(soi) ; destruction légère
```

- **Ramassage (`+0x27C == 1`)** :
  1. rien si un dialogue est ouvert (`[0x80152F08] & 4`) ;
  2. test de recouvrement sur 3 axes avec la boîte du héros (`0x80127D30`) : `+0x1D8/+0x1DC/+0x1E0` et tailles `+0x1F0/+0x1F4/+0x1F8`. Si `a = e.pos − hero.pos ≥ 0`, il faut `a < hero.taille + 1` ; sinon `hero.pos − e.pos < e.taille + 1` ;
  3. `r = GiveItem(héros, item)` ;
  4. si `r == 0` et `+0x274 != 0`, le tampon de texte est vidé (**aucun message** pour un objet lâché) ; sinon le texte est « nom de l'objet » + ETC `0x45` (« gagné! ») ;
  5. `SetGameFlag(+0x280)` ;
  6. si `table[item].octet5 != 0` : son `octet5`, message ouvert en mode 0 s'il y a du texte, `+0x27C += 2`. Sinon c'est un objet-clé : `+0x27C += 1`, blocage des autres entités, `0x80049B7C(0)`, `0x8005A9E0(0xB)` (fanfare CD), message en mode 1.
- **Branche coffre (`+0x274 == 2`)** : décompte `+0x278`, messages ETC `0x4C-0x4F` (« Tu transportes trop de choses pour garder… »), `ApplyItem` (`0x80033DBC`), `SetGameFlag([+0x280]->+0x40)`, puis `Unblock([+0x280])` et destruction légère.
- FACT : le mot `[0x80029BCC]` vaut 1 dans l'image et n'est jamais écrit. La garde `[0x80029BCC] != 0 || [0x8009A858] != 0` est donc toujours vraie (chemin objet-clé).
- FACT : séquence de rebond de `+0x284`, depuis `0xA0000`, à `Gravity = 128` (481 cartes) : `0x78000, 0x5A000, 0x43800, 0x32A00, 0x25F80, 0x1C7A0, 0x155B8, 0x1004A, 0xC037, 0x9029, 0`. À `Gravity = 3` (cartes 159 et 160) : 24 rebonds.
- FACT, A255 :
  - type `0x1E` (coffre) : `+0x88 = 1` si `+0x3C == 0`, sinon rien ;
  - `+0x274 == 1` : retour immédiat. C'est le rôle de l'écriture `+0x274 = 1` du lâcher : A255 ne réinitialise pas l'objet lâché ;
  - `+0x274 == 2` : attend `+0x88 == 1` du coffre (sinon `+0x10 = 1`) ;
  - objet posé (`+0x274 == 0`) : `+0x6C &= ~0x80` ; si `+0x40 != 0` et son bit est **bas** → destruction légère, sinon `+0x27C = 0`, `+0x280 = -1`, `+0x284 = 0`, `+0x278 = +0x40`.
- FACT : pour un objet posé, `+0x280 = -1` fait appeler `SetGameFlag(0xFFFFFFFF)` au ramassage. Cela écrit le bit 31 du mot `0x801EBA40 + 0xFFC = 0x801ECA3C`. UNKNOWN : ce qui se trouve à cette adresse. Les 151 objets posés ont tous `_10 = 0` (aucun n'est lié à un drapeau).

### 1.7 Remise à l'objet au héros : `GiveItem(héros, item)` — `0x80033F00`

Table de sauts `0x800230D4` (0x57 entrées). Retour `table[item].octet4 == 0` (donc 0 pour tous les objets lâchés).

| Objet | Effet (FACT, rejoué) |
|---|---|
| `0x45`..`0x48` | `AddMoney` de 1, 5, 10, 30, plafond 9999 |
| `0x4F` | `+1` au compteur `+0xE` des becs (plafond `0x32`), et bit `0x400` de `0x801EB3F8`, soit le drapeau 1450 |
| `0x54` | `Hp = min(HpMax, Hp+2)` (`0x80032E2C`), sans effet visuel |
| `0x55` | `Hp += max(1, HpMax/4)`, un effet global 14 animation 0 à `Z + 0x100000`, `+0x5C = 0x20000` (`0x80032EEC`) |
| `0x56` | `Hp += max(5, HpMax/2)`, 4 effets global 14 animation 0 (`0x80032F84`) |
| `0x51`, `0x26` | `MP += 1` (écrêté), 4 effets global 14 animation 2 (`0x800335AC`) |
| `0x24` | **inventaire d'abord** (`0x80033EC8` : le compte avant < le compte après ?). Si l'objet n'entre pas, il est utilisé : `Hp += max(5, HpMax/4)` et un effet (`0x80032E50`) |
| `0x25`, `0x27`, `0x29`, `0x26` | même règle « inventaire d'abord » (`0x80033274`, `0x80033A2C`, `0x800330FC`, `0x800335AC`), corps de `0x25`, `0x27`, `0x29` non lus |
| `0x50`, `0x52`, `0x53` | `0x8003382C`, `0x800336F0`, `0x800333AC` (vase de vie, déjà porté) |
| autre | ajout à l'inventaire (`0x8004E530`) |

- Les 4 étincelles de `0x56`, `0x51` et `0x26` : par effet **créé** seulement, 2 pas du générateur, `ForceX = hi(r1*0x20001) − 0x10000`, `ForceY = hi(r2*0x30001) − 0x18000`, `ForceZ = 0x20000`. Cela fait 8 pas par ramassage si le réservoir n'est pas plein.
- `0x80034108` (opcode `0x82` des événements) appelle les **mêmes** fonctions d'effet.
- La décompilation (`PlayerManager.cs:4562-4800`, `:5070`) est fidèle sur les objets atteignables (noms de `AddLowHp`/`AddMediumHp` échangés, sans conséquence).

### 1.8 Table des objets `0x800C5F7C` (8 octets par rangée, 98 rangées)

- Mot 0 : pointeur de nom, rempli au chargement (`0x8002C66C-0x8002C69C`, chaînes `0x200 + i`). Dans l'EXE, tous valent `0x80029BC8`.
- Octet 4 : règle des 600 ticks. Non nul pour 14 objets : `0x24-0x27, 0x29, 0x45-0x48, 0x51, 0x52, 0x54-0x56`. Les autres durent indéfiniment.
- Octet 5 : son de ramassage. `0x35` pour les gildas, `0x34` pour les cœurs et la plupart des potions, `0x36` pour `0x51/0x52`, 0 pour les objets-clés.
- `Data/item-drop-properties.json` est exactement les octets 4 à 7 des 98 rangées (vérifié).

### 1.9 Ordre dans l'image et champs

- FACT : cadre `0x8003B390` : `0x80038634`, `0x800386D0` (tick du héros puis choix des programmes ; `DestroyEntity` des cas pente et VRAM), `0x80038998` (compteurs), `0x800384F4` (listes), `0x80038E18` (avance de frame), `0x80038364` (fin d'image), `0x80038E84` (ombres), `0x80039300` (passe de coups).
- FACT : `0x80038364` réécrit `+0x1D8/+0x1DC/+0x1E0 = Pos + (+0x1E4/+0x1E8/+0x1EC)` (`0x800383C0-0x800383E0`) pour les seules entités de la liste active (états 2 et 3, non bloquées), puis applique forces et déplacement. Le contact de C255 voit donc les positions de l'image précédente.
- FACT : `+0x144 = (sol >= PosZ)` est écrit par `0x80038064` (`0x8003810C`), à la création (`0x80039F30`) et en fin d'image.
- FACT : `0x800384F4` n'ajoute une entité à la liste des visibles que si `(+0x1CC & 3) != 3` (`0x800385E8-0x800385FC`). `UpdateActiveEffects` (`0x80038E84`) retire alors aussi son ombre. Le bit `0x80` de `+0x6C` (retiré au lâcher) décide de la liste des « collidables ».
- INFERENCE : le premier tick de C255 voit `IsOnGround = 0` car la fin d'image suivant la création a déplacé l'objet de `0xA0000`. Il n'y a donc pas de rebond parasite à 0x78000 au premier tick. À confirmer avec la physique de la DLL.

## 2. Modèles Python contre le vrai code (`$S`)

Machine MIPS copiée de `docs/plan-e14-annexe/e14b1/work/emu/`, avec `lh.py` en plus. Seuls `SpawnWarpEntity`, le son, le dialogue, la CD et les tampons de texte sont des bouchons à journal ; tout le reste est du vrai code.

| Fonction | Script | Vecteurs | Instructions couvertes | Écarts | Mutants |
|---|---|---|---|---|---|
| `Choose` `0x80032968` | `t_choose.py` | 11 680 | 38/38 | 0 | tués par `t_spawn.py` / `t_init.py` |
| `SpawnRandomLoot` | `t_spawn.py` | 12 000 | 62/62 | 0 | tués (veto, 600, Z, ordre Y/Z, son, `+0x284`) |
| `SpawnContents` | `t_spawn.py` | 12 000 | 59/59 | 0 | tués (dont `+0x280`) |
| `InitializeContents` | `t_init.py` | 20 000 | 57/58 | 0 | tués (seuil `0x800`, banques, bit, repli) |
| C255 | `t_c255.py` | 8 000 vecteurs, 41 919 ticks | 381/381 | 0 | 46/46 (`mut_c255.py`) |
| A255 | `t_a255.py` | 15 671 puis 15 853 | 79/80 | 0 | — |
| `GiveItem` (11 objets) | `t_give.py` | 30 000 | cœurs et herbe 178/181, cristal 81/81, aide 14/14 | 0 | 7 tués, 2 équivalents |
| `DestroyEntity` + récursion | `t_destroy.py` | 8 000 | 43/43 et 75/75 | 0 | 7 tués, 2 équivalents, 1 sans effet |

Mutants des modèles (`mut_models.py`) : 25, dont 23 tués et 2 équivalents (`>= 0x101` au lieu de `>= 0x100`, plafond d'argent `0x270F`). Mutants de la récursion (`mut_destroy.py`) : 11 ; 7 tués, 2 équivalents (emplacement 0 jamais enfant dans le générateur ; `a1 > 0` égal à `a1 != 0` en entiers non signés Python), 1 remplacement sans effet.

Erreurs de lecture corrigées en cours de route, signalées pour l'archive : l'offset d'effet du héros est `0x100000` (pas `0x10000`) ; le bit des becs est `0x801EB3F8` ; le mot de garde de C255 est `0x8009A858` (pas `0x80099858`) ; la condition d'état de la récursion porte sur la cible.

## 3. Écarts avec la décompilation (le binaire tranche)

- **L1** `ChooseRandomlyAnItem` : la décompilation avance le générateur deux fois par maillon (`Random.Next()%17` puis un second tirage) ; le binaire une seule (déjà D7 de `e20-e/check.md`).
- **L2** `InitializeContents` : `EntityRecord.Contents` est un `ushort`, le binaire lit l'octet `+0x12`. Aucun effet : aucune entité posée n'a `Contents > 0xFF`.
- **L3** C255 : la garde `[0x80029BCC] != 0 || [0x8009A858] != 0` est perdue (toujours vraie). Pour le décompte, le binaire décrémente aussi quand `d ≤ −2` (`(d+1) >=u 2`), la décompilation seulement quand `d > 0` ; inatteignable sur la branche objet lâché. Dans la branche coffre, le binaire garde un pointeur en `+0x280`, la décompilation un indice (`g_entitySlots[AIValues[0]]`) : même effet.
- **L4** Aucun écart pour `SpawnEntityContents`, `RandomlySpawnItem`, la récursion `FUN_8003a648` et `GiveItem` sur les objets atteignables.

## 4. Recensement des `Contents` (`$S/census2.py`, `census2_out.txt`)

Entités posées activées : 9 631 dans les 483 cartes. `Contents` d'enregistrement > `0xFF` : 0 ; drapeau `_10` invalide (`>= 0x800`) : 0. Code effectif (octet d'enregistrement sinon octet d'en-tête) :

- 4 928 sans butin ;
- 497 objets fixes (`< 0x62`), tous des coffres de type 30 (515 coffres, 98 contenus distincts ; INFERENCE pour 3 coffres des cartes 144 et 303 dont la page est ambiguë dans mon recensement) ;
- 4 206 tirés dans la table (`>= 0x80`) ;
- aucune valeur `0x62-0x7F`.

396 entités portent un drapeau de contenu : 393 coffres globaux et 3 coffres de page ambiguë ; aucun drapeau de banque temporaire.

Tirés dans la table, par code (sprites en page de carte = `M`, en page globale = `G`) :

| Code | Posés | Sprites |
|---|---|---|
| `0x82` (cœurs seuls) | 2 070 | M34 tonneau 76, M35 cruche 915, M36 caisse 995, M37 pierre 27, G254 panneau 57 |
| `0xF8` | 42 | M38 cruche n°2 |
| `0xF7` | 8 | M41 herbe transportable |
| `0xFA` (argent seul) | 22 | M148 mimique |
| `0xFB` | 674 | M91/94/100 Muruta (203/39/30), M106/109/115 hommes-lézards (110/25/40), M128 statue 150, M211 fourneau 37, M217 oiseau de feu 35, M252 sbires du roi 5 |
| `0xFC` | 853 | M73 slime 118, M79 Beannoïde 130, M82/85/88 orcs (33/61/2), M121 Mimming 65, M133 Roper 12, M139 tête de roche 4, M145 homme-insecte 84, M154 ver 76, M169 momie 34, M172 fantôme 16, M175 homme de boue 16, M181/185 tortues (75/14), M184 homme-ombre 40, M187 champignon 26, M193 zombie 15, M196 P-Zoldia 26, M205 homme de bois 6 |
| `0xFF` | 537 | murs/colonnes/ronces (G230, 232, 234, 238, 239, M118, 119, 120, 126, 131, 134) |

- Les cruches, caisses et tonneaux ne donnent que des cœurs (`0x82`) ; l'argent vient des ennemis, de la cruche n°2 et de l'herbe.
- Objets ramassables atteignables dans les données : `0x24, 0x45-0x48, 0x51, 0x54, 0x55, 0x56`.
- Pas du générateur espérés par les tirages à la création de toutes ces entités : environ 4 857 sur 483 cartes.
- Têtes avec `Contents` mais 0 posé (créés par des gestionnaires, `+0x3C` peut-être remis à 0, INFERENCE) : M167 (`0xFB`), M222 petit slime, M240, M248 (`0xFC`), M249 (`0xFB`). Les 7 types de boss de E11 n'ont pas de `Contents` ; leurs enfants M248, M249, M252 en ont.
- Objets posés sur les cartes servis par C255 : 151 entités, 51 types, tous `_10 = 0`, tous avec A255 et C255 (dont 16 vases de vie, 10 cœurs moyens, 9 herbes, 17 bouquets).

## 5. La DLL au HEAD `2010ec3`

**Existe**
- Tirage : `AlundraLootRoll.cs` (`:22`, `Choose :51`, `Roll :77`, table `:108`). Appelé aux trois sites de création `AlundraWorldProxy.cs:1091, :3406, :3526` par `RollLootAtCreation :3429` (`RecordContentsByte :3420`), sur le flux réel `AlundraRandom.Next` (`AlundraRandom.cs:52`). Octets lus en `HeaderContents` (`AlundraEntityScriptProxy.cs:100`, posé par `AlundraEntitySpawnFactory.cs:743`).
- Champs de l'objet : `ContentsItemId`/`ContentsGameFlag` (`AlundraEntityScriptProxy.cs:93-94`), `ForceZ :325`, `IsOnGround :404`, `DamagedTickCounter :470`, `Bytes`/`DelayOrAngleOrEntityId`/`ItemState`/`AIValues :1214-1217`, `Width/Height/Depth :475` (posés `AlundraEntitySpawnFactory.cs:858-860`).
- Apparition par indice : `SpawnEntityBySpriteIndex` (`IEntityWorldContext.cs:70`, impl. `AlundraWorldProxy.cs:3448`, `:3555`).
- Item d'état : `AddMoney :2272`, `AddOneItemIfUnlocked :2225`, `UseItem :2250`, `SetPlayerMp :1957`, `IncreaseHpMaxAndFill :1862`, `hero.Hp/HpMax` (`:1441`).
- Opcode `0x82` (`AlundraEventProgramRunner.cs:3150`, `UseLifeVessel :3196`) : 0x45-0x48, 0x4F, 0x53 faits ; les autres `0x50-0x56` dégradés (O-E19-39).
- Étincelles : modèle `AlundraNativeEffects.CreateLifeVesselEffects :28` ; effet global 14 (3 animations) exporté ; sons 52, 53, 54, 84 présents dans `Sounds/sfx-manifest.json`.
- Registre natif : `AlundraNativeSpriteRegistry.Register :47` (exemple `AlundraWaveSwordAi.cs:28`).
- Test de dialogue ouvert : `dialogue.IsOpen || IsAwaitingChoice` (`AlundraSaveBook.cs:232-244`, équivalent de `0x80045004`).
- Tests : `AlundraLootRollTests.cs`, `AlundraLootRollWorldTests.cs`, `AlundraLootRollWorldTests.Rolls.cs`.

**Manque**
- Aucun `SpawnRandomLoot`, `SpawnContents`, veto MP, `GiveItem`, `ApplyItem`, C255, A255.
- `DestroyEntity(entity, effectId)` : « (1) loot : E20.e0 » à `AlundraWorldProxy.cs:3269` et « (4) libération de l'effet actif » à `:3273`, bouchons nommés. Le test `AlundraDestroyEntityBreakEffectTests.cs:199-214` épingle le bouchon (aucun tirage, aucune entité créée).
- Le slot E d'index 11 (récursion) est un no-op (`AlundraEventProgramRunner.cs:335`).
- `AlundraItemTables` ne garde que la colonne 2 (`DropField3`) ; la colonne 0 (règle des 600) et la colonne 1 (son) sont dans le JSON mais non exposées.
- Aucun accesseur 32 bits pour `AIValues` (mots `+0x280/+0x284/+0x288`).
- `ModdedPosX/Y/Z` n'est écrit qu'à la création (`AlundraEntitySpawnFactory.cs:821-823`) ; la fin d'image du binaire le rafraîchit à chaque tick.
- Aucun rendu ne cache une entité si `(DamagedTickCounter & 3) == 3` (grep ; le compteur est décrémenté `AlundraWorldProxy.cs:2958-2978`).
- Les 151 objets posés sont inertes aujourd'hui.

## 6. Inconnues et inférences

- UNKNOWN : sens exact de `[0x800C480C]` (lu par `CdBusy` `0x8005A7D4`) ; valeur du registre `[0x80149CBC]` écrit à 3 ou 4 par C255 (c'est « SetEtcAnimationMode »/« Set dialog choice » de la décompilation, lu par un opcode `0x8003EB08`) ; contenu de `0x801ECA3C`.
- INFERENCE : C255 est lancé par la passe de choix des programmes à chaque tick d'une entité d'état 2/3, le premier tick de l'objet lâché étant à l'image suivante.
- INFERENCE : après ramassage, l'objet reste dessiné 2 ou 3 ticks (états 2 et 3 de `+0x27C`) avant la destruction légère.
- INFERENCE : le clignotement masque l'objet 1 tick sur 4 pendant 120 ticks.
- UNKNOWN : corps de `0x80033274`, `0x80033A2C`, `0x800330FC`, `0x8003382C`, `0x800336F0` (pas atteignables par le butin).

## 7. Questions ouvertes (à poser avec ce que cela change en jeu)

- **Q-e0c-1.** Le ramassage des objets posés sur les cartes (message « X gagné! », fanfare CD pour les objets-clés, blocage des autres entités) est-il dans cette tranche ? Le même gestionnaire les sert, mais ils exigent dialogue et CD. *Sans* : les vases de vie, cœurs et herbes posés restent inertes, seul le butin est ramassable.
- **Q-e0c-2.** Les étincelles de ramassage (0x55, 0x56, 0x51) naissent-elles tout de suite (`CreateNow`, comme le binaire, ordre des 8 pas du générateur exact) ou par la file (`Emit`, ordre différent) ? Cela demande d'étendre l'ADR-0057 aux objets.
- **Q-e0c-3.** Le clignotement des 120 derniers ticks : le règle `(DamagedTickCounter & 3) == 3` générique maintenant, ou avec E20.c2b (le clignotement du héros blessé utilise la même règle) ? *Sans* : l'objet disparaît à 600 ticks sans avertissement.
- **Q-e0c-4.** Le contact de ramassage : rafraîchir `ModdedPos` en fin d'image pour toutes les entités (fidèle, décalage d'une image), ou le calculer en direct à partir de `Pos + Mod` ?
- **Q-e0c-5.** Ne pas porter `SetGameFlag(0xFFFFFFFF)` des objets posés (écriture parasite du binaire) ni le registre `[0x80149CBC]` ? Proposé : non pour les deux, écart documenté (règle « corriger les défauts de l'original »).
- **Q-e0c-6.** Le lâcher des enfants d'un boss par E11 (récursion) entre-t-il dans T3, ou attend-il E14.b ? La DLL traite aujourd'hui E11 comme un no-op.
- **Q-e0c-7.** Les points de vie des cœurs : écrire `hero.Hp` quand `LifePulledFromStats`, les stats sinon (même règle que `IncreaseHpMaxAndFill`) ? Proposé : oui.

## 8. Tâches proposées (remplacent la liste de `e20-e/report.md` §9 pour e0c)

1. **e0c-T0 — annexe et oracle.** Archiver dans `docs/plan-e20-annexe/e20-e0c/` les listings (`$S/lst/*.dis`, 26 fichiers), les modèles (`models.py`, `give_model.py`, `c255model.py`), les harnais (`lh.py`, `t_*.py`, `mut_*.py`), le recensement (`extract.py`, `census2.py`) et un `SHA1SUMS`. Prérequis : aucun (déjà fait, reste à verser).
2. **e0c-T1 — `AlundraLootDrop` pur.**
   - `SpawnRandomLoot(code, x, y, zBinaire)` : `AlundraLootRoll.Choose`, veto, `SpawnEntityBySpriteIndex(null, false, item+0x1E, x, y, z−1, 0)`, test de null, puis les sept écritures, le son 84.
   - `SpawnContents(entity)` : mêmes écritures ; position du conteneur passée telle quelle, `PosZ` en repère DLL (le binaire lit `+0x11C`, repère binaire).
   - Exposer les colonnes 0 et 1 de `item-drop-properties.json` (durée, son) dans `AlundraItemTables`.
   - Un accesseur 32 bits des mots `+0x280/+0x284/+0x288`.
   - Tests : vecteurs de §2 (`SpawnRandomLoot`, `SpawnContents`, veto, table pleine, flux consommé même en cas d'abandon).
3. **e0c-T2a — C255 et A255 natifs, objets lâchés (`+0x274 == 1`).**
   - Enregistrer `(slot C, code 255)` pour les 87 types et `(slot A, code 255)` pour les 88.
   - Rebond et seuil `Gravity << 8`, durée de vie 600 et marque à 120, montée puis ramassage, sortie en destruction légère.
   - Recouvrement de la boîte du héros (selon Q-e0c-4).
   - `GiveItem` pour `0x24`, `0x45-0x48`, `0x4F`, `0x51`, `0x54-0x56`, en réutilisant `AddMoney`, `AddOneItemIfUnlocked`, `SetPlayerMp`. Un seul `AlundraItemEffects` doit servir aussi `HandleMapTriggerCommand` et fermer O-E19-39 pour ces cas.
   - Étincelles dans `AlundraNativeEffects` sur le modèle de `CreateLifeVesselEffects` (selon Q-e0c-2).
   - Tests : modèle de C255 (8 000 vecteurs) rejoué sur le natif ; les vecteurs de bornes (recouvrement, seuil de rebond).
4. **e0c-T2b — objets posés et coffre (`+0x274 == 0` ou `2`).** Messages ETC, fanfare, blocage, `ApplyItem`, `SetGameFlag`. Dépend de Q-e0c-1 et du dialogue ; le coffre (F255) reste une tranche à part.
5. **e0c-T3 — brancher `DestroyEntity`.** `SpawnContents` en première instruction de `AlundraWorldProxy.DestroyEntity(entity, effectId)` (`:3269`) ; mettre à jour le test `AlundraDestroyEntityBreakEffectTests.cs:199-214`. Ajouter ensuite la récursion E11 si Q-e0c-6 le décide.
6. **e0c-T4 — arcs sur cartes réelles.** Tuer un Beannoïde (`0xFC`, carte 14) ou un Muruta (`0xFB`) : l'objet naît, monte, rebondit, est ramassé, ou s'éteint à 600 ticks. Parité du générateur (1 ou 2 pas à la création, 8 pas pour les étincelles de 0x56/0x51).
7. **e0c-T5 — recette de l'auteur.** Sauvegarde `butin-m<N>` ; tuer, voir l'objet, le ramasser (argent et cœurs), laisser filer 600 ticks.

## 9. Fichiers (`$S`)

- Modèles et harnais : `models.py`, `give_model.py`, `c255model.py`, `lh.py`, `t_choose.py`, `t_spawn.py`, `t_init.py`, `t_c255.py`, `t_a255.py`, `t_give.py`, `t_destroy.py`, `mut_models.py`, `mut_c255.py`, `mut_destroy.py`.
- Recensement : `extract.py`, `census.py`, `census2.py`, `census2_out.txt`, `t_table.py`, `cache/maps.pkl`, `cache/placed.pkl`, `cache/c255_vectors.pkl`.
- Outils de lecture : `calls.py` (appelants), `hand.py` (gestionnaires), `mipsdis.py`, `exelib.py`, `emu/`.
- Listings : `lst/*.dis`, `c255.dis`, `p33dbc.dis`.