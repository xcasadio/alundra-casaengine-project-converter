# Rapport thème 2 : le coffre au trésor (F255 `0x8007FC88`) et la Graine magique #80

Lecture seule respectée. Je n'ai écrit que sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest/` (abrégé `T2/`). Aucun build, aucun test, aucune commande git qui écrit.

Binaire : `ALUN_CD.EXE` (France). DLL lue au HEAD `43a47432`, arbre de travail avec g1b en cours : `PlayerManager.cs` a décalé de +78 lignes après `:955`.

Étiquettes : **[FAIT]** lu dans le code, le plan ou une annexe, source citée. **[MESURÉ]** produit par mes scripts sur le binaire ou les données. **[INFÉRENCE]** non vérifié.

---

## 0. Synthèse

1. **Le coffre n'a aucune tranche, et la DLL ne l'ouvre pas.**
   - Le plan le renvoie à « E14 » (`plan-e20-heros.md:8418`, `:8491`), mais `plan-e14-ia-native.md` ne contient ni « coffre », ni F255, ni A255, ni C255.
   - Dans la DLL, Alundra qui presse Carré contre un coffre s'arrête en Idle, le pick F tombe sur un handler absent (no-op compté), et rien ne se passe. Ni son, ni animation, ni objet, ni drapeau.
   - Le coffre de la clé de la carte 117 ne peut donc pas être ouvert, alors que la porte de la 115 (`0x84 [61]` en pc 982) consomme cette clé.

2. **Mécanisme exact [MESURÉ, rejoué sur le vrai code].**
   - Contact avec le coffre plus Carré « naissant » (`0x80`). L'interaction passe par `CheckEntityInteraction` `0x8002E910`, le coffre portant le drapeau 0x8000 (`InteractRequiresButton`).
   - F255 pose le coffre à `+0x88 = 2` (animation d'ouverture, son 51 posé par l'animation, 61 ticks, puis chaînage vers l'animation 1).
   - F255 fait naître l'objet (sprite `contenu + 0x1E`, 16 px au-dessus du coffre) et fige le monde par `Block(coffre)`. Le monde reste figé jusqu'à la fermeture de la boîte.
   - Cet objet monte 64 ticks. Puis arrive le message « &lt;nom&gt; gagné! », puis `ApplyItem`, puis `SetGameFlag(drapeau du coffre)`, puis `Unblock`.
   - Du Carré au message : 127 ticks (rejoué), environ 2,5 s à 50 Hz [INFÉRENCE sur la fréquence].

3. **Persistance.**
   - Le drapeau `_10` du coffre n'est posé qu'à la toute fin (après la fermeture de la boîte), dans la banque persistante.
   - À la création du coffre, `InitializeContents` `0x80032A40` met `+0x3C` à 0 si le drapeau est levé. A255 dessine alors le coffre ouvert (`+0x88 = 1`) et F255 répond le texte « vide » (ETC 0x48).
   - Les 420 coffres ont des drapeaux persistants inférieurs à 2048. Aucun n'est dans la banque temporaire : tous sont sauvegardables.

4. **Sac plein.** Seuls les objets `0x24`, `0x25`, `0x26`, `0x27` et `0x29` ont un plafond de stock (9, 1, 1, 1, 1).
   - L'objet monte, le message dit « Tu transportes trop de choses pour garder X. », rien n'est donné, le drapeau n'est pas posé.
   - Le coffre retombe en animation 0 (fermé) et se rouvrira.
   - Cela concerne 174 des 400 coffres à contenu.

5. **Graine magique #80.** Son corps `0x8003382C` est le **jumeau instruction pour instruction du vase de vie** `0x800333AC` [MESURÉ] : 13 instructions sur 127 diffèrent.
   - Effet : MpMax + 1 (plafond 4), PM remplis au nouveau MpMax, son `0x36`, 8 anneaux plus 4 étincelles de l'animation **2** (le vase utilise l'animation 0).
   - Trois entrées binaires y mènent : `GiveItem`, `ApplyItem` (coffre) et l'opcode `0x82` via `0x80034108`.
   - MpMax initial = 0 en partie normale. Les seules sources sont les 4 coffres des cartes 7, 15, 25, 434 (plus l'idx 80 de la carte 0).

6. **Données.** Le chiffre de l'inventaire « 471 » est faux : c'est 569 − 98, qui inclut 51 enregistrements de page-carte (tombes), pas des coffres. Il y a **420 coffres** hors carte 0 : 400 avec contenu sur 158 cartes, 20 vides, 404 natifs F255 et 16 à F scripté. Les 400 et 158 de l'inventaire sont exacts.

7. **Moteur.** Rien de bloquant côté CasaEngine. Deux points sont à consigner et un à mesurer (§5).

---

## 1. Comportement du binaire

### 1.1 Comment le coffre s'ouvre

- **Données du sprite 30** [MESURÉ] : `T2/t12_chestanim.out`.
  - `MoreFlags 0x80`, `CanPickup 0x81`, soit les drapeaux d'entité `0x8180` = Collidable (0x80), Gravity (0x100), InteractRequiresButton (0x8000).
  - `ProgramLoad = 255`, `ProgramInteract = 255`, tick/touch/deactivate = 0.
  - Boîte 24 × 16 × 16.
  - Seul sprite global avec un F = 255 ; A255 est porté par 88 types, C255 par 87 (pas le 30).
  - Les tables de créneaux donnent bien A255 = `0x80061BD4`, C255 = `0x8007C174`, F255 = `0x8007FC88` (table maître `0x80098F4C`).
- **Chaîne d'appel** [MESURÉ, désassemblage `T2/t3_checkinteraction.dis` et `T2/t4_pick.dis`].
  - `MovePlayer`, cas Idle/Moving : `TryUseItem`, puis `PlayerTryAction` (`0x8002EEAC`), puis `CheckEntityInteraction` (`0x8002E910`).
  - `PlayerTryAction`, avec un contact (`hero+0x130`) et Carré naissant, rend le résultat de `TryLift` (`0x8002EDBC`).
  - Pour le coffre, `TryLift` rend 0 : le genre de ramassage `(flags & 0x600) >> 9` vaut 0. Le résultat 0 laisse passer la suite.
  - `CheckEntityInteraction` lit `hero+0x130` (XCollisionEntity, l'entité qui a bloqué le dernier pas).
    - Si le drapeau 0x8000 est levé, il mémorise (latch) l'entité, les positions des deux et la direction du héros.
    - Sans contact, le latch compte encore tant que tout est inchangé et que la direction est la même.
    - L'entité doit avoir `+0x60` ou `+0x84` non nul (code F). Le coffre a `+0x84 = 255`.
    - Avec le drapeau 0x8000 : il faut `ButtonsJustPressed (0x80126F2A) & 0x80`, donc Carré naissant. Résultat 2, `[0x80127108] = coffre`. Sinon 0.
  - Le pick (`0x80038840-0x80038888`) choisit le créneau 5 (F) si `[0x80127108]` est ce coffre et qu'il a un F ; sinon 2 (C).
- **Conclusion** :
  - Il faut pousser contre le coffre, d'un côté quelconque, et presser Carré dans l'état Idle ou Moving.
  - Il n'y a pas de test de direction autre que le latch. Une fois le contact établi, on peut lâcher le stick tant qu'on ne bouge pas.
  - Résultat 2 : le héros passe en Idle et le tick s'arrête avant `PlayerTryAttack`. Pas de coup d'épée.

### 1.2 F255 pas à pas (`T2/f255.dis`)

Entrée : e = le coffre. Adresses `0x8007FC88-0x8007FE1C`.

1. **Gardes**, chacune faisant sortir sans rien faire :
   - `[0x800DC4B8] != 0`. C'est `g_playerControlFlags` : tout bit levé (verrou, menu, boîte de message, séquence forcée…).
   - `[0x800DC4C4] != 0`. C'est `g_isGameEnding` pour la décompilation, un warp ou fin de boucle en cours. Je l'ai vu écrit à 1 en `0x8002BCFC` et `0x8005123C`, remis à 0 en `0x8002BD3C` [INFÉRENCE sur son rôle exact].
   - `hero+0x2C != 0` (objet porté).
   - dialogue ouvert (`0x80045004` : `[0x80152F08] & 4`).
2. `s2 = e+0x3C` (contenu). Si `s2 == 0` **ou** `e+0x88 != 0` : texte ETC 0x48, ouvert par `0x800450F0(texte, mode 0)`. Fin.
3. `e+0x88 = 2`, puis `o = SpawnWarpEntity(e, page 0, type s2+0x1E, e+0x114, e+0x118, e+0x11C + 0x100000, direction 0)`.
   - Si `o == 0` : texte ETC 0x49 (« Erreur système… ») en mode 0. Fin. Le coffre reste à 2.
4. `Block(e)` (`0x8003AD30`). Écritures sur `o` : `+0xB8 = 0x8000`, `+0x278 = 0x40`, `+0x6C &= ~0x180` (retire Collidable et Gravity), `+0x20 = 0`, `+0x274 = 2`, `+0x27C = 0`, `+0x280 = e` (pointeur).
5. `o+0x284 = CanTake(s2)` (`0x80033D34`).
   - Si 0 : `+0x288 = 0`. Fin.
   - Si `table[s2].octet5 != 0` : `+0x288 = 0`. Fin.
   - Sinon (objet-clé) : `[0x80029BCC] != 0` est toujours vrai (valeur d'image 1, jamais écrite : lectures seules par `xref`). Donc `+0x288 = 1` et `LoadBgm(0)` (`0x80049B7C`) arrête la musique. Fin.

**CanTake `0x80033D34`** [MESURÉ] : 0 pour l'objet 0 ; vrai pour 1..0x23 ; pour `0x24..0x27` et `0x29`, `GetNumberOfItem < table[item].octet7`. Ces octets valent 9, 1, 1, 1, 1 et coïncident avec le maximum de `items-properties.json` colonne 3. Vrai pour tout le reste, sans test de stock.

**Rejeu F255** (`T2/f255_oracle.py`, `T2/f255_oracle.out`) :
- 12 000 vecteurs (graines 11 et 97), **0 écart**, **102/102 instructions** couvertes.
- **15 mutants du modèle sur 15 tués** (`T2/f255_mutants.out`).
- Seuls SpawnWarpEntity, Block, GetEtc, l'ouvreur et LoadBgm sont des bouchons journalisés. CanTake, `GetNumberOfItem` et le test de dialogue tournent pour de vrai.
- Mon erreur de lecture corrigée en route : le masque est `-0x181` signé = `~0x180`, pas `~0x181`.
- Table de 244 vecteurs exportée en TSV pour les tests de la DLL : `T2/f255_vectors.tsv`.

### 1.3 A255 et C255 de l'objet-coffre (`T2/a255.dis`, `T2/c255_whole.dis`)

- **A255 sur le coffre** (type `0x1E`) : si `+0x3C == 0`, alors `+0x88 = 1` (dessiné ouvert). Sinon rien.
- **A255 sur l'objet (kind 2)** :
  - si `chest+0x88 != 1`, il met `+0x10 = 1` (reste Loaded, donc ni simulé ni dessiné, voir `T2/lists_800384f4.dis`) ;
  - sinon, si `+0x288 != 0`, il lance la piste CD 11 (`0x8005A9E0(0xB)`).
- **C255 branche kind 2** (`0x8007C174-0x8007C394`, états de `+0x27C`) :
  - **Décompte `+0x278`** : 64 ticks, un par tick.
  - **État 0** :
    - `+0xB8 = 0` (la montée s'arrête) ; si dialogue ouvert, on attend.
    - Si `+0x284 != 0`, le texte est ETC 0x4C + nom + ETC 0x4D, avec `ETC 0x4C` vide.
      - Objet-clé (`+0x288`) : `+0x27C += 1`, boîte en mode 1, `0x8004507C(4)` (fermeture par script seule).
      - Sinon : `+0x27C += 2`, son `octet5`, boîte en mode 1.
    - Si `+0x284 == 0` : `+0x27C += 2`, texte ETC 0x4E + nom + ETC 0x4F, boîte en mode 1.
  - **État 1** (clés seulement) : attente de `CdBusy`, puis masque 3 et `StopAllSound`. La musique repart.
  - **État 2** (dialogue fermé) :
    - si `+0x284 != 0` : `ApplyItem(héros, item)` (`0x80033DBC`) et `SetGameFlag(coffre+0x40)` ;
    - sinon `coffre+0x88 = 0` (le coffre se referme) ;
    - puis `Unblock(coffre)` et destruction légère de l'objet.
- **Rejeu bout en bout** (`T2/chest_sequence.py`, `T2/chest_sequence.out`) : F255, A255 et C255 réels, boîte et CD simulés. Extrait de la chronologie :

| Tick | Événement |
|---|---|
| 0 | F255 : coffre à 2, objet né, `Block`, `LoadBgm(0)` si clé |
| 0 | son 51 (animation) |
| 62 | A255 libère l'objet ; piste CD 11 si clé |
| 63 à 126 | décompte de 64 ticks |
| 127 | son 52/53/54 si non-clé ; message ; mode 1 |
| 212 | (clé) masque 3 et `StopAllSound` (CD simulé de 150 ticks) |
| fin de boîte | `ApplyItem`, `SetGameFlag`, `Unblock`, destruction |

  - Herbe (stock 3) : « Herbes gagné! », son 52.
  - Herbe (stock 9) : « Tu transportes trop de choses pour\ngarder Herbes. », puis `chest+0x88 = 0`.
  - Graine (clé) : « Graine magique gagné! », masque 4 puis 3.
  - 30 florins : son 53.
  - Clef (item `0x3D`) : octet5 = 52, donc **pas** un objet-clé malgré son nom.
  - Épée (item 2) : objet-clé, fanfare.
- Ré-exécution du modèle C255 de l'annexe e0c sur copie, graine 31 : `T2/c255_chest_rerun.out`, 5 000 vecteurs, 0 écart, 381/381 instructions.

### 1.4 Animation et son [MESURÉ sur le vrai `UpdateAnimation`]

`T2/chest_anim_oracle.py`, `T2/chest_anim_oracle.out`.

- Animation 2, direction 0 : son 51 au tick 0 de la bascule. À la fin du tick 61, `TargetAnimationId` passe de 2 à **1** (chaînage `00 01`). Le son 0 de la bascule n'est pas un vrai son.
- Les directions 1 et 2 chaînent vers 0 (coffre refermé). La direction 3 dure 81 ticks. Aucun des 420 coffres n'en a besoin : `dirb ∈ {0, 64}`, direction 0.
- Côté DLL, `sprite-records.json` porte `End: Chain, ChainTo: 1` (direction 0) et `Sfx: 51` pour l'animation 2.
- L'objet monte à `+0xB8 = 0x8000` par tick, sans gravité ni collision. Environ 32 px sur le décompte [INFÉRENCE sur l'intégration exacte].

### 1.5 Textes (ETC de l'export, indice réel = position − 1)

`T2/t2_etc.out`, `alundra-project/Dialogues/Etc.yarn`.

| ETC | Texte | Usage |
|---|---|---|
| 0x48 (Etc_0072) | « Tu as regardé dans le coffre dans\\nl'espoir de trouver un objet oublié,\\nmais malheureusement, il reste vide. » | coffre vide ou déjà ouvert, mode 0 |
| 0x49 (Etc_0073) | « (Erreur système)… La mémoire est-elle saturée? » | échec d'apparition, mode 0 |
| 0x4C (Etc_0076) | vide | préfixe du gain |
| 0x4D (Etc_0077) | « gagné! » (espace de tête dans le binaire, perdu par le compilateur Yarn) | |
| 0x4E (Etc_0078) | « Tu transportes trop de choses pour\\ngarder » (espace final dans le binaire) | sac plein |
| 0x4F (Etc_0079) | « . » | |
| 0x46 (Etc_0070) | branche morte | |

Résultats : « Herbes gagné! » (avec l'espace) et « Tu transportes trop de choses pour\\ngarder Herbes. ». Les noms viennent de l'ETC `0x200 + objet`.

### 1.6 Ce que donne chaque contenu : `ApplyItem` `0x80033DBC`

`T2/seed_oracle.out`, `T2/cantake_applyitem.dis`. Seul appelant : C255 en `0x8007C358`. C'est distinct de `GiveItem` `0x80033F00` (butin et objets posés) et de `0x80034108` (opcode `0x82`).

- Table `0x8002308C` : `0x45` à `0x48` ajoutent 1, 5, 10 ou 30 gildas. `0x49` à `0x4E` partent à `AddItem` `0x8004E530`. `0x4F` appelle `0x8004E6EC(1)`. `0x50` appelle `0x8003382C`. `0x51` appelle `0x800335AC`, `0x52` appelle `0x800336F0`, `0x53` appelle `0x800333AC`, `0x54` à `0x56` sont les cœurs. Tout le reste va à `AddItem`.
- **Différence avec `GiveItem`** : pas de règle « l'inventaire d'abord ». Un herbe qui n'entre pas n'est jamais bue sur place, puisque `CanTake` a déjà refusé.
- Les 42 contenus distincts rencontrés sont donc tous couverts, sauf `0x50`, qui reste un bouchon.

### 1.7 Persistance

- `InitializeContents` `0x80032A40` (`T2/t5_initcontents.dis`) :
  - Le drapeau est le champ `_10` de l'enregistrement. Un drapeau dont `(flag & 0x7FFF) >= 0x800` est remis à 0.
  - La banque est temporaire (`0x801EBA40`) si le bit 15 est levé, persistante (`0x801EB344`) sinon.
  - Drapeau levé : code = 0, donc contenu 0.
  - Sinon : code = octet `+0x12` de l'enregistrement, ou défaut `+0x1F` de l'en-tête (0 pour le coffre), puis `ChooseRandomlyAnItem(code)`. Un code `< 0x62` rend le code : aucun des 400 coffres n'a de code aléatoire (`≥ 0x62` : 0).
- La DLL le fait déjà : `Items/LootRoll.cs:104-131`, appelé par `Maps/WorldProxy.cs:3532`.
- `SetGameFlag` `0x80032B28` : drapeau 0 = rien ; sinon le bit `flag mod 32` du mot `flag >> 5` de la bonne banque.
- Coffres scriptés : l'opcode `0x9F` « If chest opened » est déjà porté (`EventProgramRunner.cs:1562-1569`). Il lit le drapeau de la première entité trouvée.

### 1.8 La Graine magique #80 en détail

`T2/t9_seed.dis`, `T2/t10_mp.dis`, `T2/t16_seed_vs_vessel.out`, `T2/seed_oracle.out`.

- Corps `0x8003382C(héros)` :
  1. `0x8004DE4C(1)` = `SetPlayerMpMax(MpMax + 1)` ; `0x8004DDF4` plafonne : ≥ 5 donne 4, < 0 donne 0.
  2. `SetPlayerMp(GetPlayerMpMax())` : PM = nouveau MpMax.
  3. Son `0x36`.
  4. 8 effets globaux (effet 14, **animation 2**, position du héros, Z + `0x100000`), décalés de `x<<13`, `y<<13`, forces `−(x<<9)`, `−(y<<9)`. Les tables `0x80023654` et `0x80023694` sont identiques à `RingOffsetX/Y` de `Rendering/Effects/NativeEffects.cs:21-23`.
  5. 4 étincelles de même animation, ForceZ `0x40000`, **8 tirages du générateur** (`ForceX = hi(r1·0x20001) − 0x10000`, `ForceY = hi(r2·0x30001) − 0x18000`).
- Rejeu complet de `ApplyItem(0x50)` : 15 cas (MpMax de 0 à 5, Mp de 0 au maximum). MpMax 0 donne 1, 1 donne 2, 2 donne 3, 3 donne 4, 4 reste 4 ; Mp = MpMax après. Le son vaut toujours `0x36`, 12 effets, 8 pas du générateur.
- **Comparaison avec le vase de vie `0x800333AC`** (127 instructions chacun, `T2/t16_seed_vs_vessel.out`) : 13 diffèrent.
  - Entrée : vie contre PM.
  - Son : `0x31` contre `0x36`.
  - Animation `a2` : 0 contre 2, aux deux boucles.
  - Les tables, les décalages et les tirages sont identiques.
- Les trois entrées appellent le même corps :
  - `GiveItem` table `0x800230D4[0x50]` = `0x80034040`, qui fait `jal 0x8003382c` ;
  - `ApplyItem` table `0x8002308C[0x50]` = `0x80033E40`, qui fait `jal 0x8003382c` ;
  - `0x80034108` table `0x80023234[0x50]` = `0x80034180`, qui fait `jal 0x8003382c`.
- **MpMax initial** [MESURÉ] : la fonction `0x80031700` met `SetPlayerMpMax(0)`, `SetPlayerMp(0)`, `SetMoney(0)` si `[0x80029BC4]->0` (nouvelle partie), en `0x800317D0-0x800317E4` (`s4 = 0`). Une autre branche (donnée de slot ni 0 ni 1, probablement un démarrage de debug [INFÉRENCE]) met `HpMax 38`, `MpMax 3`, `Mp 2`, `argent 2163` en `0x800317F0-0x8003183C`. Côté DLL : `Session/PlayerStats.cs:50` (`MpMax` à 0, doc « New Game default 0 »).
- **Chaîne PM** :
  - `IncreaseMpMax` n'a qu'un appelant, la graine. MpMax n'augmente nulle part ailleurs.
  - Le butin d'objets de magie (`0x26`, `0x51`, `0x52`) est vetoé tant que MpMax = 0 (`0x80032A00`), ce que `Items/LootDrop.cs:27-29` porte déjà.
  - `DecreaseMp` n'a qu'un appelant, le départ des tomes.
  - Sans la graine, les tomes (hors bâton magique) ne se jouent que sur sauvegarde de test avec `MpMax 4`.

---

## 2. Données

### 2.1 Recensement (`T2/t7_census_chests.py`, `T2/t7_census.out`, `T2/t7_chests.tsv`)

Règle de page : bit 7 de `SpriteDirection` = page de carte ; sinon page globale (annexe e0c).

| Mesure | Valeur |
|---|---|
| Enregistrements `SpriteTableIndex == 30`, toutes cartes | 569 |
| Page de carte (tombes de 10, 144, 303, 331 ; taille 20 × 14 × 32, aucun code natif) | 51 |
| Page globale, carte 0 | 98 (un objet par indice, pas de drapeau) |
| **Coffres hors carte 0** | **420**, sur 170 cartes |
| Avec contenu | **400**, sur 158 cartes |
| Vides | 20 (16 scriptés F, 4 natifs en carte 325 idx 2 à 5) |
| **Natifs F255** | **404** (400 avec contenu, 4 vides) |
| À F scripté | 16, tous à contenu 0 |
| À C scripté | 54 : 38 natifs « qui tombent » + 16 scriptés |
| Drapeau `_10 == 0` malgré un contenu | 4 : cartes 11 (idx 30), 161, 362, 398 |
| Drapeau dans la banque temporaire (bit 15) | 0 |
| Drapeau ≥ 0x800 | 0 |
| Drapeaux distincts | 367 ; de 185 à 2000 |
| Contenu `≥ 0x62` (table aléatoire) | 0 |
| Direction (`SpriteDirection`) 64 contre 0 | 353 contre 67 ; tous orientés vers le bas |
| Natifs avec contenu apparaissant au chargement de la carte (`SpriteDirection & 0x40`) | 342 contre 58 créés plus tard par script |

- Le coffre « qui tombe » est un script C commun (`Is above ground`, son 55, attente d'atterrissage, son 24, puis C remis à 0). Voir cartes 25, 30, 48, 95, etc. (`T2/t7_chests.tsv`, colonne C).
- Doublons exacts de la carte 80 : les idx 0/53, 1/54 et 2/55 sont à la même position avec le même drapeau (741, 742, 743). L'un ouvert, l'autre garde son contenu jusqu'au rechargement.
- Drapeaux partagés entre cartes jumelles (17/439, 83/410, 84/411, 12/407, 18/450, 5/479, 311/398). Deux coffres partagent un drapeau dans une même carte : 79 (0/4 et 6/7) et 54 (21/22).
- Histogramme des contenus (`T2/t7_census.out`) : Herbes 108, Clef 47, Faucon doré 54, Vaisseau de vie 38, 30 florins 39, Fortifiant 26, Essence magique 17, Elixir de force 14, Elixir de magie 9, Graine 4, etc.
  - Objets à stock plafonné (« sac plein » possible) : 108 + 14 + 9 + 17 + 26 = **174 coffres**.
  - Objets qui n'ont qu'un coffre hors carte 0 : liste par objet dans `T2/t13_unique_items.out` (armes, bottes, tomes, emblèmes, clés).
  - Table des octets 4 à 7 pour ces contenus : `T2/t11_itemtable.out`.
- **Écart avec l'inventaire** : 471 = 569 − 98 inclut les 51 sprites de page de carte. Les 400 « avec contenu » et les 158 cartes sont exacts. Il y a 420 vrais coffres, pas 471.

### 2.2 Coffres scriptés contre natifs

- **16 coffres à F scripté** (`T2/t15_scripted_chests.py`) :
  - Cartes 36 à 40 (« Nirude's Lair », textes `Disuse`), 55 à 60 (« Torla Buggy… early version »), 136 idx 14 (Église), 154 idx 14, 363 idx 2 et 3.
  - Le pattern : F pose un drapeau temporaire, le C du même coffre l'attend, joue l'animation 2 puis 1, ouvre une boîte, pose un drapeau d'ouverture.
  - Seul **136 idx 14** donne un objet avec fanfare : `0x8B` (apparition), `0xA6 [0]` (arrêt BGM), `0x50 [4]` (masque 4), `0xB9 [11]` (CD), `0x82 [89]` (Clef bizarre).
  - Les chemins 37 à 39 donnent l'objet 36 (herbes) par `0x82`. Les autres n'affichent qu'un texte.
- **Sprite 227 « Coffre d'Anzes »** (31 enregistrements, cartes 163, 170, 177, 184, etc., dont la chaîne) : F script unique `0x0D [9]`, un dialogue. Hors périmètre, déjà géré par l'interpréteur.
- Les chaînages du plan ne doivent pas confondre : natif F255 = 404 coffres, scripté = 16 plus les 31 coffres d'Anzes.

### 2.3 Chaîne d'histoire

Les 30 cartes de `StoryChainOpcodeAudit.ChainMaps` (`Alundra.Tests/Support/StoryChainOpcodeAudit.cs:27-31`) **n'ont aucun coffre**. Les sept cartes de combat (14, 15, 44, 115, 116, 117, 362) en ont **25** (`T2/t7_chests.tsv`, ou liste dans la sortie du recensement).

| Carte | Coffres |
|---|---|
| 14 | 3 : herbes idx 13, vase idx 19 (drapeau 1665), Faucon idx 20 |
| 15 | 9 : **Graine idx 4** (70,83 ; drapeau 1741), 4 × 30 florins (idx 5, 18, 19, 20), 3 herbes, Faucon |
| 44 | 2 |
| 115 | 3 : vase idx 10 (drapeau 659), herbes idx 11, Fortifiant idx 21 (tombe ; C 150 ; drapeau 261) |
| 116 | 3 |
| 117 | 4 : **Clef idx 19 (drapeau 260)**, herbes, 30 florins |
| 362 | 1 : Faucon idx 10, sans drapeau, créé par script (B[7]) |

- **Couplage de progression** :
  - Carte 117 : un programme attend le drapeau 260 (`WaitFlagSet`) puis détruit une entité.
  - Carte 115 : `0x84 [61]` en pc 982 consomme la Clef. Un autre programme (pc 1065) attend `0x9F [10]` puis joue le son 223 et anime des tuiles : l'ouverture du coffre idx 10 (drapeau 659) ouvre un passage.
  - Sans F255, la chaîne bloque à la 117 et à la 115. L'opcode `0x9F` ne rend jamais 1.
- 21 sites `0x9F` : cartes 25, 30, 76, 95, 115, 148, 153, 154, 325, 343 (`T2/t8_out.txt`). 23 drapeaux de coffre sont lus ou posés par d'autres scripts de carte.

### 2.4 Les quatre graines (+ la carte de test)

| Carte | Nom | Idx | Position | Drapeau | Particularité |
|---|---|---|---|---|---|
| 7 | Overworld 1,2 | 9 | (45,105) | 1349 | |
| 15 | Overworld 3,2 | 4 | (70,83) | 1741 | sur la route de la chaîne |
| 25 | Lars' Crypt | 1 | (74,36) | 626 | direction 0, script C qui tombe |
| 434 | Lake Shrine (garden) | 9 | (57,22) | 1773 | |
| 0 | Test Map | 80 | (6,54) | 0 | |

### 2.5 Cartes d'essai utiles

- **Carte 0** : grille de 10 × 10, un objet par indice (contenu = indice), aucun drapeau. Seuls les coffres à direction 64 apparaissent au chargement (armes idx 2, 3, 5, 9 ; Clef idx 61 ; Bouquet idx 74 ; Livre d'Elna idx 88 ; etc.). Les herbes, florins et graine y sont à direction 0, donc créés par script.
- **Debug zone (carte 11)** : idx 30 (14,48) Vaisseau de vie **sans drapeau** (réouvrable à l'infini) ; idx 37 (34,70) Herbes drapeau 2000.
- **Carte 15** : le meilleur terrain de recette (graine, argent, herbes, Faucon sur une même carte).

---

## 3. État de la DLL au HEAD

| Élément | État | Fichier:ligne |
|---|---|---|
| F255 (gestionnaire du coffre) | **absent** | aucune clé `(5, 255)` ; `NativeSpriteRegistry.cs:35-47` |
| A255 sur le type 30 | non inscrit (D-E0C-2) | `PickupAi.cs:32` (`SpriteTypes` = 31..118 sauf 72) |
| A255 branche coffre et C255 branche coffre | **bouchons nommés** | `PickupAi.cs:21`, `:70`, `:76`, `:116-118` |
| `CanTake` `0x80033D34`, `ApplyItem` `0x80033DBC` | absents | aucun symbole |
| Graine `0x8003382C` | bouchon nommé | `Items/ItemEffects.cs:23`, `:100-101` ; test `ItemEffectsTests.cs:366-379` |
| Route `0x82` pour `0x50` à `0x56` | dégradée | `Events/EventProgramRunner.cs:3170-3172` |
| Contenu tiré à la création | **porté** | `Items/LootRoll.cs:104-131`, `WorldProxy.cs:3469`, `:3532` |
| Opcode `0x9F` | **porté** | `EventProgramRunner.cs:1562-1569` |
| Pick F, `CheckEntityInteraction` | **porté** | `Hero/PlayerManager.cs:1920`, `EntityScriptProxy.cs:1965-1978` |
| `TryUseItem`, `PlayerTryAction` | bouchons | `PlayerManager.cs:947`, `:955` (g1b en cours) |
| `Block`, `Unblock` | portés | `Maps/IEntityWorldContext.cs:97`, `:102` ; `WorldProxy.cs:3667-3695` |
| Boîte composée, masque de fermeture | portés | `Dialogues/DialogueDirector.cs:259`, `:405` |
| Apparition par indice de sprite avec parent | portée | `IEntityWorldContext.cs:81`, `WorldProxy.cs:3545-3600`, `EntitySpawnFactory.cs:536` |
| Effets (anneaux, étincelles) | portés pour le vase et les soins | `NativeEffects.cs:21-23`, `:31-60`, `:106` |
| `SetPlayerMpMax` (plafond 4), `SetPlayerMp`, `AddOneItemIfUnlocked` (= `0x8004E530`) | portés | `PlayerManager.cs:2121`, `:2140`, `:2408` |
| Chaînage `Chain` et son d'animation | portés | `EntitySpawnFactory.cs:213`, `FrameSyncPasses.cs:90`, `:240` |

**Quand Alundra touche un coffre aujourd'hui.** Le contact et Carré donnent le résultat 2 : le héros passe en Idle, le tick s'arrête, `ActiveCollisionEntity` est posée, puis le pick F choisit le créneau 5.
- Le registre n'a pas d'entrée pour le type 30 : un no-op compté, une trace debug par clé.
- Le coffre garde l'animation 0 (fermé). Les 20 coffres vides et les coffres déjà ouverts sont aussi dessinés fermés.
- Les coffres à F scripté (16) fonctionnent déjà, car le script l'emporte.

**Tests qui épinglent le bouchon** (à amender par la tranche) :
- `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:51-62` : décompte du registre et `TryGet(C, 255, 30)` faux.
- `Alundra.Tests/Ai/Objects/PickupAiDropWorldTests.cs:408-425` (w7).
- `Alundra.Tests/Ai/Objects/PickupAiPlacedTests.cs:90-101` (P1) et `:520-524` (P8).
- `Alundra.Tests/Items/ItemEffectsTests.cs:366-379` (graine).
- `Alundra.Tests/ExportGuards/PickupObjectsExportGuardTests.cs:102` (F255 sur le coffre seul) : à conserver.

---

## 4. Ce qu'il faut ajouter : tranche proposée « E20.e0d, le coffre au trésor et la Graine magique »

Dépendances : e0c (faite), `PickupAi`, `Block/Unblock`, `OpenComposed`. Aucun lien avec g1b, f6b ni h0a. **Aucun fichier de `PlayerManager.cs` à toucher** (g1b y travaille).

**Phase T0, mesures sans code de production** :
- (a) La DLL joue-t-elle le son 51 et chaîne-t-elle 2 vers 1 en 61 ticks sur un coffre ?
- (b) Une entité sans gravité avance-t-elle bien de `ForceZ` par tick ? (`EntityScriptProxy.cs:1195-1235` : « a rise adds F »)
- (c) **L'objet tenu en Loaded (état 1) est-il dessiné ?** Dans le binaire, les listes `0x800384F4` ne contiennent que les états 2 et 3 : l'objet est invisible pendant les 61 ticks. La DLL ne cache qu'un `FlagToDestroy` (`FrameSyncPasses.cs:107-110`), donc elle le dessinerait trop tôt.
- (d) La pause du dessin des entités bloquées : `GameplayFreeze` ne gère que le masque `MenuOpen`. Le `Block` d'e0c ne gèle que la logique.
- (e) L'équivalent DLL de `g_isGameEnding`.
- (f) Les 21 sites `0x9F`.

**Phase A, données et affichage** : A255 inscrit sur le type 30 (`+0x88 = 1` quand le contenu est 0) ; garde de données (420 / 404 / 16 / 4 / 0 / 0 / 0 / 342 / 58, drapeaux < 2048, aucun code ≥ 0x62).

**Phase B, ouverture ordinaire** :
- `ChestAi` (nouveau, `Alundra/Ai/Objects/`), clé `(F, 255)` pour `[30]`. Le registre interdit deux inscriptions de la même clé : étendre les types de A255 dans `PickupAi` plutôt qu'en ajouter une.
- `ItemEffects.CanTake` et `ItemEffects.ApplyItem` (nouveaux ; en partie les mêmes corps que `GiveItem`, sans la règle « l'inventaire d'abord »).
- Branche kind 2 de C255 : états 0 et 2, textes composés, `Unblock`.
- `+0x280` est un pointeur : utiliser `ParentEntity` (posé par l'apparition) plutôt que `AIValues`. `+0x288` tient dans `AIValues[4..5]` (le tableau a 10 shorts).
- Rejeu : 244 vecteurs de `T2/f255_vectors.tsv`, plus les six cas de `T2/chest_sequence.out`.

**Phase C, graine, clés et fanfare** :
- `MagicSeed` partagé, avec `NativeEffects.CreateLifeVesselEffects` paramétré par l'animation (0 vase, 2 graine).
- Fanfare : bouchon journalisé comme e0c, à deux endroits (arrêt de la musique au tick 0, piste 11 au tick 62).
- `EventProgramRunner.cs:3170` : le cas `0x50` cesse d'être dégradé.
- Un petit utilitaire `IncreaseMpMax` dans `HeroHeals` ou `ItemEffects`, pas dans `PlayerManager`.

**Tests proposés** :
- Table du binaire : 15 lignes de `T2/seed_oracle.out` (MpMax/Mp avant puis après), 12 effets, 8 pas du générateur, son `0x36`.
- Chronologie : 127 ticks du Carré au message, A255 libère l'objet au tick 62, décompte 63 à 126.
- Garde de données (§2.1).
- Persistance : ouvrir, sauvegarder, recharger, la carte montre le coffre ouvert et F255 répond ETC 0x48.
- Régression : la clé de la 117 ouvre la porte de la 115.

**Recettes et sauvegardes** :
- Ajouter à `SaveGames/TestSaves.cs` des préréglages sur le modèle de `pickup-m11` : `chest-m15` (carte 15), `chest-m11` (idx 30 et 37), `chest-m0` (armes dir 64), `chest-m117` (la clé). Départs à mesurer en T0 : les positions du recensement sont en demi-tuiles et hauteurs ; je n'ai pas converti en tuile.
- Sauvegardes réelles (`tools/Saves/`, **noms seuls**, correspondances [INFÉRENCE]) : `60-crypte-de-lars.sav` à `68-after-crypte-de-lars.sav` pour les quatre coffres qui tombent de la carte 25 (graine idx 1) ; `200-canal-souterrain.sav`/`201-…` (cartes 77, 79) ; `260`-`267-elenes-dream.sav` (cartes 48, 54) ; `350`-`358-lake-castle*.sav` pour le Lake Shrine ; `mine-de-charbon*.sav` ; `cape-de-sable.sav` (coffre de la carte 402) ; `240`-`250` pour Nirude.

---

## 5. Moteur (CasaEngineMonogame)

- **Rien de bloquant.** Le chaînage d'animation, le son d'animation, l'apparition avec parent, `Block`, la boîte composée et les effets existent déjà.
- **À consigner** : la fanfare du CD (piste 11) reste le bouchon E19.sm7 (ADR-0076) ; le coffre ajoute un second site d'appel.
- **À mesurer en T0, correctif côté DLL possible** : la pause du dessin des sprites pour les entités bloquées par `Block` (propriété `AnimatedSpriteComponent.IsPlaybackPaused` déjà utilisée par `GameplayFreeze`) ; le masquage de l'objet tenu en Loaded.
- Ordre d'une passe : O-E20-23 (une entité de rang supérieur au bloqueur ne joue pas son programme dans la DLL) s'applique aussi à F255 ; P3, sans effet connu.

---

## 6. Questions à l'auteur, défaut « comme l'original »

1. **Périmètre** : une tranche en trois phases (affichage, ouverture ordinaire, graine et clés) avec le bouchon de fanfare d'e0c ? Défaut : oui. Alternative : une tranche entière.
2. **Fanfare avant E19.sm7** : couper la musique au tick 0, la relancer à l'état 1 (immédiat, aucune attente), sans piste CD ? Défaut : oui (même bouchon que les objets-clés d'e0c).
3. **Coffres sans drapeau** (carte 11 idx 30, 161 idx 6, 362 idx 10, 398 idx 14) : ils se remplissent à chaque entrée dans la carte. Corriger en leur posant un drapeau, ou garder ? Défaut : garder (c'est une absence de donnée du jeu, pas un défaut de code). Seuls le vase de la carte 11 (debug) et le Faucon de la 362 sont exploitables.
4. **Doublons de la carte 80** (idx 0/53, 1/54, 2/55, même position, même drapeau) : garder tel quel ? Défaut : oui.
5. **Dessin des entités bloquées** pendant l'ouverture (environ 127 ticks plus la lecture) : les figer aussi visuellement, comme la logique ? Défaut : oui, comme l'original (fige les listes active et visible).
6. **Objet pendant l'animation** : invisible 61 ticks (état Loaded) ? Défaut : oui, comme l'original.
7. **Textes** : restaurer l'espace avant « gagné! » et après « garder », comme e0c l'a fait pour ETC 0x45. Défaut : oui.
8. **Présets de recette** : `chest-m15`, `chest-m11`, `chest-m0`, `chest-m117` ? Défaut : oui, les quatre.
9. **Coffres scriptés** des cartes d'essai (36 à 40, 55 à 60) et coffres d'Anzes : hors tranche. Défaut : oui.
10. **Priorité** : la chaîne bloque à la 115 et à la 117 sans coffre. Le placer avant h1 ? Défaut : après g1b en cours, avant h0a et h1, puisqu'il ne touche pas `PlayerManager.cs`.

---

## 7. Écarts avec l'inventaire (`report-main.md`, `critic-main.md`)

- « 471 coffres hors carte 0 » : 420 vrais coffres ; 471 compte 51 sprites de tombes (§2.1).
- « Aucune tranche E14 ne le nomme » : confirmé (`plan-e14-ia-native.md`, aucune occurrence).
- « Graine : +1 PM max, PM pleins, son 0x36, effets d'anneau » : confirmé et précisé (jumeau du vase, animation 2, 8 anneaux plus 4 étincelles, 8 tirages, §1.8).
- Le critique C1 (chaîne PM, coffre, graine) : confirmé ; MpMax initial = 0 est mesuré au binaire (`0x800317D0`) et non seulement lu dans la DLL.
- Le libellé du plan « A255 inscrit sur 88 types, C255 sur 87 » : confirmé par les en-têtes de sprites.
- Le critique B2 (« TryLift existe en fonction pure ») ne change rien pour le coffre : `TryLift` rend 0 pour le genre 0.

---

## 8. Index des fichiers (`T2/`, chemins complets sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-chest/`)

- Désassemblages : `f255.dis`, `c255_whole.dis`, `a255.dis`, `cantake_applyitem.dis`, `t3_checkinteraction.dis`, `t4_pick.dis`, `t5_initcontents.dis`, `t9_seed.dis`, `vessel_body.dis`, `t10_mp.dis`, `t14_trylift.dis`, `lists_800384f4.dis`, `updateanimation.dis`.
- Rejeux : `f255_oracle.py` / `f255_oracle.out`, `f255_mutants.py` / `f255_mutants.out`, `f255_vectors.py` / `f255_vectors.tsv`, `seed_oracle.py` / `seed_oracle.out`, `chest_sequence.py` / `chest_sequence.out`, `chest_anim_oracle.py` / `chest_anim_oracle.out`, `c255_chest_rerun.out`, `t16_seed_vs_vessel.py` / `.out`.
- Données : `t7_census_chests.py`, `t7_census.out`, `t7_chests.tsv`, `t8_chestflags_in_scripts.py`, `t8_out.txt`, `t11_itemtable.py` / `.out`, `t12_chestanim.py` / `.out`, `t13_unique_items.out`, `t15_scripted_chests.py`, `t1_applytable.py` / `.out`, `t2_etc.py` / `.out`.
- Outils : `bx.py` (lecteur et désassembleur), `xref.py` (références de données et appels), `evlib.py` (décodage des programmes d'événements), copie de la machine MIPS dans `emu/` (et `e0c_copy/`).