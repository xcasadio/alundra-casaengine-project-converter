# E14.b1 : jour 2 (cartes 14 et 15), Poisson 392, Guêpe 455, Orc masse 341, Orc hache 338, Orc armure de fer 344

Découverte en lecture seule du 2026-10-17 (label `e14b1`), dépôt lu à `9502f75`. Rien n'a été modifié, construit, exécuté ni exporté dans le dépôt ni dans le binaire. Vérité terrain : `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Étiquettes : **FAIT** (adresse du binaire, `fichier:ligne` ou mesure), **INFÉRENCE**, **INCONNU**.

Mes scripts et listings sont dans `S/` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/e14b1/`. Le fichier `S/report.md` n'a pas pu être écrit : l'outil Write l'a refusé (« les sous-agents rendent leurs résultats en texte »). Ce texte en tient lieu.

## 0. Ce qui a été prouvé, et comment

Les pseudo-codes du §3 ne sont pas seulement lus. Ils sont transcrits en Python (`S/models.py`) et comparés au **vrai code du binaire**, appel par appel.

L'interpréteur MIPS est celui de l'oracle d'E14.a1 (copie de `docs/plan-e14-annexe/e14a/oracle-a1/tools/emu.py`, dans `S/emu/`). Le harnais est `S/h.py` + `S/vec.py`. Chaque vecteur donne :
- une image d'entité de 0x294 octets ;
- le héros en emplacement 0 (`0x80127D30`) ;
- le mot du générateur (`0x80098708`) ;
- l'octet C de la carte (`[0x800E4334]+0xA`).

Sont **réels** : A0, C15 à C19, D5, D6, D11, D12 et les aides `0x8007FE20`, `0x8007FE8C`, `0x8007FEF8`, `0x80080260`, `0x800805C8`, `0x8002E814`. Sont **bouchonnés et journalisés** : le son `0x800490FC`, `CreateEffectEntity` `0x8003BDD8`, `ComputeNewHp` `0x8003A374` (résultat 0 ou 1 choisi par le harnais) et `DestroyEntity` `0x8003A59C`.

| Vérification | Résultat |
|---|---|
| Modèle contre code réel (image d'entité complète, mot du générateur, journal) | **0 écart** sur 9 000 vecteurs par gestionnaire (C15 à C19, D5, D6, D11, D12), 3 000 pour A0 et pour chaque aide, plus 1 536 motifs exhaustifs de `CanMoveForward` |
| Couverture d'instructions du code réel | **100 %** pour A0, C15, C16, C17, C18, D5, D6, D11, D12 et les aides. C19 : 98,6 % (les 2 mots `0x8006A9AC-0x8006A9B0` ne s'atteignent qu'avec une animation cible négative) |
| Sensibilité des vecteurs (`S/mut.py`, 10 mutants du modèle) | **10/10 tués** |
| `CanMoveForward` `0x80080260` contre `HandleAnimationDirection` `0x80080144` (code réel des deux) | **0 écart sur 82 944 cas** : la première est exactement la décision de la seconde, sans ses effets |
| Arbre de décision de la DLL (`AlundraEnemyHelpers.HandleAnimationDirection`, transcrit dans `S/dll_had.py`) contre `CanMoveForward` | **0 écart sur 40 000 cas** |
| `TryAttackPlayer` `0x80080704` (code réel) contre la décompilation transcrite | 0 écart sur 30 000 vecteurs |
| `SetEntityRandomDirection` `0x80080030` (modèle) contre le code réel | 0 écart sur 5 000 vecteurs |

Relancer : `cd S && python -B vec.py 3000` (environ 30 s) et `python -B mut.py` (environ 2,5 min).

Désassemblages complets annotés (champs, aides, tirages, tables de sauts) :
- `S/listings_handlers.txt` : A0, C15 à C19, D5, D6, D11, D12, E1 (1 280 lignes) ;
- `S/listings_helpers.txt` : les aides (904 lignes) ;
- un fichier par fonction dans `S/lst/`, produit par `S/dis2.py`.

Les tailles retrouvées par le flot de contrôle (176, 214, 178, 260, 143, 62, 25, 74, 25, 8, 3 mots) égalent `exe_span_words` de `handlers.tsv`.

## 1. Cadre

### 1.1 Adresses des tables natives (FAIT)

Le tableau maître est `0x80098F4C`. Bases : A `0x800C4B34`, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34` (B n'a pas de table). Entrée = base + 4 × code.

| Clé | Entrée de table | Code | Mots | Table de sauts | Types qui l'utilisent (en-tête) |
|---|---|---|---|---|---|
| A0 | `0x800C4B34` | `0x8006174C` | 3 | - | 180 types ; ici les cinq |
| C15 | `0x800C4F70` | `0x80069C84` | 176 | `0x80027334` (7) | 338 seul |
| C16 | `0x800C4F74` | `0x80069F44` | 214 | `0x80027354` (7) | 341 seul |
| C17 | `0x800C4F78` | `0x8006A29C` | 178 | `0x80027374` (7) | 344 seul |
| C18 | `0x800C4F7C` | `0x8006A564` | 260 | `0x80027394` (6) | 455 seul |
| C19 | `0x800C4F80` | `0x8006A974` | 143 | chaîne de `beq` sur les états 0, 1, 5, 7 | 392 seul |
| D5 | `0x800C5348` | `0x8007DBE0` | 62 | - | 344, 404, 425, 454, 461, 467 |
| D6 | `0x800C534C` | `0x8007DCD8` | 25 | - | 356, 389, 392, 401, 410, 440, 443, 456 |
| D11 | `0x800C5360` | `0x8007DEE8` | 25 (corps identique à D8) | - | 377, 386, 431, 449, 455, 458, 473, 482 |
| D12 | `0x800C5364` | `0x8007DF4C` | 74 | - | 338, 341 |
| E1 (= E0) | `0x800C5738` (`0x800C5734`) | `0x8007ED10` | 8 | - | 61 types d'en-tête ; **déjà en ligne dans la DLL pour tout type** (`AlundraEventProgramRunner.cs:378`) |

C16 : la décompilation tolère aussi le nom `Melzas2_FinalBoss`. Aucune donnée ne l'y envoie et aucun échange `0x41` non plus.

Règle de répartition (FAIT, `e14a/check.md:75`, `muruta.md` §1) :
- La phase 1 choisit E si une désactivation est demandée, sinon D si `+0x224` TouchingEntity est non nul, sinon C. **Un coup remplace donc le C de ce tick par le D.**
- La phase 2 appelle `table[lettre][entity[+0x70+4*s]]` seulement si `entity[+0x4C+4*s] & 0x7F == 0`.
- Les codes viennent des octets `+0x13..+0x17` de l'en-tête (`InitializeEntity`, `0x80039DD4-0x80039E10`).
- `+0x68` est le numéro de type du sprite (`0x80039D70`). D5 et D12 le comparent à `0x1A9` (425), `0x152` (338) et `0x155` (341).

### 1.2 Les cinq types (FAIT : en-têtes, `BALANCE.BIN.json`, `map_N.json`)

Les enregistrements de sprite sont identiques sur toutes les cartes, à quatre pointeurs mémoire près (19 comparaisons sur les 5 types). Indice de la page carte = type − 256 ; `SpriteType` de la DLL = `0x100 + indice` (`AlundraHitArena.cs:77`).

Entrées du catalogue exporté `alundra-project/Data/sprite-records.json`, toutes avec `BreakEffect` 8 :
- 338 : `a135a585-ba4d-5807-82b2-dc1b663f2d38`
- 341 : `d28eacdc-9805-5e9c-9548-d776acb16eaa`
- 344 : `0e87ce5c-d34e-5e0b-9ad0-7375a9172eb3`
- 392 : `47ee54ae-a760-5de5-ad23-0037d3244b45`
- 455 : `7eb50e32-b027-5fe4-969e-96672b146850`

| Type | A/C/D/E/F | Drapeaux `+0x6C` | Boîte | Vie | Attaque (attribut, puissance) | `Contents` | Enreg. activés (cartes) |
|---|---|---|---|---|---|---|---|
| Poisson 392 | 0/19/6/1/0 | `0x00103` (classe A, touche B, gravité, ombre 0) | 14×16×17 | 2 | (1, 1) | 0 | 48 (10) |
| Guêpe 455 | 0/18/11/1/0 | `0x10003` (**sans gravité**, ombre 1), décalage Z 4 | 16×14×14 | 2 | (1, 1) | 0 | 13 (4) |
| Orc masse 341 | 0/16/12/1/0 | `0x30103` | 20×14×32 | 9 | (2, 4) | 252 | 61 (13) |
| Orc hache 338 | 0/15/12/1/0 | `0x30103` | 20×14×32 | 9 | (1, 3) | 252 | 33 (8) |
| Orc armure de fer 344 | 0/17/5/1/0 | `0x30103` | 20×14×32 | 9 | (2, 1) | 252 | 2 (1) |

Fiches de balance :
- Niveau 255 seul, sans chaîne ; une attaque par fiche (`AttackForAnimation` rend toujours `Attacks[0]`).
- Réponses `[16,16,16,64,16,…]` : x1 pour les nibbles 1 à 3 et 5+, le nibble 4 (bombe) tue d'un coup.

Recomptage (`S/records.py`, `S/records_b1.txt`) : **157 enregistrements sur 31 cartes**, égal au plan §1.4. Trois enregistrements bruts de la carte 134 pointent `SpriteTable[85] = -1` et ne naissent jamais.

Hors cartes 14 et 15 :
- 49 enregistrements de l'Orc masse naissent au chargement (`SpriteDirection & 0x40`).
- 10 enregistrements de l'Orc masse (cartes 71 et 74) passent d'abord par un programme C scripté puis par `0x40 [C,0]`.
- 8 Poissons de la carte 139 ont C et D scriptés, donc jamais natifs.

**Cartes de l'histoire connue** (X/Y/hauteur en demi-cases ; INFÉRENCE sur l'unité, cohérente avec le Poisson en (42,46) dans le bassin des cases x 40-46, y 43-59) :
- **Carte 14**
  - Guêpes : enregistrements 7, 8, 9 en (56,94,10), (66,102,10), (54,104,10).
  - Poissons : enregistrements 11 et 12 en (84,92,0) et (88,104,0), dans le bassin de `AlundraHeroSwimArenaTests`.
  - Tous `SpriteDirection 0xC0` (naissent au chargement), tous codes 0, octet C = 7.
  - Hors b1 et donc inertes : 7 Beannoïdes (0-6, déjà natifs), 3 `Sbires du roi` 508 (C131 scripté), `Boss lézard` 494 (enreg. 17), `Homme-lézard (projectiles)` 371 (enreg. 23), `Surveillance élémentaire` 499 (enreg. 24), `Cercle magique de Zoldia` 510 (enreg. 16). Leur rôle exact au jour 2 n'est pas établi (INFÉRENCE).
- **Carte 15**
  - Orc masse : enregistrements 15, 16, 17 en (14,60,14), (14,72,14), (42,72,14), `0xC0`, codes 0.
  - Hors b1 : 3 Muruta (0-2, scripts), 2 `Boss lézard` (7, 13), `Mille-pattes` 488, `Muruta (boomerang)` 356 (enreg. 10).
  - Le héros de `AlundraTarnMurggArcTests` y démarre à la case (26, 21), z 7.
- L'octet C vaut 7 sur les 13 cartes de l'Orc masse. L'effet global 7 = 9 images de 3 ticks, `End: Destroy`, semi-transparent (`effects-global.json`).

### 1.3 Planches d'animation (FAIT : `S/animsheets.md`, générateur `S/animsheet.py`)

Convention de `muruta.md` §2 :
- `ticks` = somme des délais (`& 0x7F`) du sens bas.
- Fin d'animation : délai 1 = boucle ; sinon, si le bit 7 de l'octet suivant est posé, `Hold` (pose `+0xAC = 1`), sinon enchaînement.
- Vitesse en `+8` ; impulsion Z = s16 `(Flags<<8 | Unknown)` ; drapeaux d'animation = octet `+0xD`.

**Poisson 392**
- 0 repos (48 t, boucle).
- 1 nage (v 64, 32 t).
- **3 saut-morsure** (v 64, impulsion Z 1792, drapeaux 7, son 150, 20 t, 4 images à boîte, enchaîne 5).
- **5 retombée** (boucle 20 t, 4 images à boîte, attend `IsOnGround`).
- **6 recul de D6** (v 256, son 65, 4 t, enchaîne 7).
- **7 attente de fin** (8 t, Hold).
- **8 mort** (8 t, Hold, son 64).
- 2, 4, 9, 10 : non utilisées.

**Guêpe 455**
- 0 vol sur place, 1 vol avancé (v 128), 2 **montée** (v 128, impulsion Z +256), 3 **descente** (v 128, −256). Toutes en boucle de 24 t, 12 images à boîte.
- 4 **recul de D11** (v 256, son 65, 4 t, enchaîne 5).
- 5 attente de fin (8 t, Hold).
- 6 mort (8 t, Hold, son 64).

**Orc masse 341**
- 0 repos (40 t, boîtes).
- 1 marche (v 128, 72 t).
- 2 mort (Hold, son 64).
- **3 charge** (v 64, son 29, 43 t, dernière image 29 t, Hold, 6 images à boîte).
- 4 recul (v 256, son 65, enchaîne 5).
- 5 attente de fin (8 t, Hold).
- **6 parade** (v 256, son 225, 18 t, Hold).
- **7 armement** (24 t, 3 images à boîte, **enchaîne 3**).

**Orc hache 338** : comme la masse, sans l'animation 7 ; 3 = **frappe** (30 t, Hold, 6 images à boîte, son 29).

**Orc armure de fer 344**
- 0 repos (40 t).
- 1 marche (v 64).
- **2, 3, 4 : attaques identiques** (99 t, 26 images à boîte, son 35, Hold).
- 5 recul de D5 (v 256, son 65, enchaîne 6).
- 6 attente de fin (Hold).
- 7 mort (Hold, son 64).

## 2. Aides du binaire utilisées par b1

Notation commune à tous les pseudo-codes :

| Symbole | Sens |
|---|---|
| `TA` | TargetAnimationId `+0x88` |
| `TD` | TargetDirection `+0x8C` (0..31 : 0 bas, 8 gauche, 0x10 haut, 0x18 droite) |
| `CA` | animation courante `+0x90` |
| `AD` | mot de direction d'animation `+0x98` (0 bas, 1 haut, 2 gauche, 3 droite) |
| `END` | drapeau de fin `+0xAC`, posé par un `Hold` |
| `FA` | ForceAdjusted `+0x13C` |
| `OG` | IsOnGround `+0x144` |
| `B0..B3` | octets `+0x274..+0x277` |
| `AI0`, `AI1` | demi-mots non signés `+0x280`, `+0x282` |
| `W28C` | mot `+0x28C` |
| `stop6` | mise à zéro de `+0xBC +0xC0 +0xC4 +0xC8 +0xD4 +0xD8` |
| `rel` | `[|dx|, |dy|, |dz|, dx, dy, dz]`, avec `dx = self.TileX − hero.TileX` en cases |
| `below(n)` | un tirage : `s = s·0x7D2B89DD + 0xE06A02E7 (mod 2^32)` puis `(s·n) >> 32` |
| `versHéros` | `GetDirectionToTarget(hero.Pos − self.Pos)` (`+0x114`, `+0x118`) |
| `loinDuHéros` | `GetDirectionToTarget(self.Pos − hero.Pos)` |

| Adresse | Nom (binaire) | Sémantique (FAIT, vérifiée sur le code réel) | DLL |
|---|---|---|---|
| `0x8007FE20` | CalculateEntityRelativePosition | `rel`, avec `dz = self.FloorHeight(+0x134) − hero.FloorHeight` | **oui** `AlundraEnemyHelpers.RelativePosition` (:35) |
| `0x8007FE8C` | variante Z | `dz = self.PosZ(+0x11C) − hero.FloorHeight` (Guêpe) | **oui** `RelativePositionFromPosZ` (:39) |
| `0x800805C8` | TryAttackPlayerFront(self, rel, near, far, zmax) | Faux si `zmax < |dz|`. Selon `AD` : 0 `near>=|dx|, dy<=0, far>=|dy|` ; 1 idem avec `dy>=0` ; 2 `near>=|dy|, dx>=0, far>=|dx|` ; 3 idem avec `dx<=0` ; sinon faux. `zmax` en 5e argument (`[sp+0x10]`) | **oui** (:65), lit `BoxDirection` (valeur du tick, D-E14-6) |
| `0x8007FEF8` | « StartFlying » (décompilation) | `r = below(0x65)`. Si `r < (chance & 0xFF)` : `TD = versHéros`, sinon `TD = below(0x20)`. `TA = anim & 0xFF`. Si `range & 0xFFFF != 0` : `AI1 = below(16) + range`. Donc 2 ou 3 tirages avec `range`, 1 ou 2 sans | **oui**, sous le nom `StartWalk` (:201) |
| `0x80080260` | « CanMoveForward » | `thr = dist+1`, `Dk = FloorHeight − MapHeights[k]`, `bk = thr < Dk`. **Vrai** ssi `(b0&b1&9<=TD<=23) | (b1&b3&17<=TD<=31) | (b3&b2&!(8<=TD<=24)) | (b2&b0&TD<16)`. **Vrai = un vide devant** (sens inverse du nom) | **non** : la décision existe dans `HandleAnimationDirection` (:95-170), il manque la fonction sans effet |
| `0x80080144` | HandleAnimationDirection | `CanMoveForward`, puis si vrai : `TA = anim`, `stop6`, `TD = QuarterTurn[TD]` | **oui** (:95) |
| `0x8002E814` | GetDirectionToTarget | angle 0..31 | **oui** `ScriptHelper.GetDirectionToTarget` |
| `0x8003A374` | ComputeNewHp | vie après l'attaque de `TouchingEntity.CurAttack`, vrai si la vie est 0 | **oui** `AlundraDamage.ComputeNewHp` (:76) |
| `0x800490FC` | PlaySoundEffect | son | **oui** `context.SoundPlayer?.PlaySfx` (ids 0x94 et 0x96 présents dans `sfx-manifest.json`) |
| `0x8003BDD8` | CreateEffectEntity(banque, effet, anim, x ; [sp+0x10] y, [sp+0x14] z) | alloue (`0x8003B9C4`) puis crée en mode 0 (`0x8003BB14`) | **oui** `AlundraEffectReservoir.Create` (:220-221). **Manque** : aucune couture n'existe pour l'émettre ni pour lire l'octet C de la carte |
| `0x8003A59C` | DestroyEntity(self, −1) = E1 | butin, `Status 4`, effet de bris `+0x1E` de l'en-tête (8 ici) | **oui**, en ligne (`AlundraWorldProxy.cs:3262-3278`) ; butin = E20.e0 |

**Aides nommées dans l'énoncé mais non utilisées par b1** (pour b0) :
- `0x80080030` SetEntityRandomDirection (FAIT, modèle égal au code réel) : `r = below(0x2000)` ; `r` impair : `TD = versHéros`, sinon `TD = r >> 8` ; `TA = a1 & 0xFF` ; `AI1 = below(16) + a2` si `a2 & 0xFFFF != 0`.
- `0x80080704` TryAttackPlayer : la transcription de la décompilation (`EntityGameplayManager.cs:348-430`) est **exacte**.
- `0x800808DC` (listing seul, non exécuté) : `d = GetEntityTileHeight(self, a1&0xFF, TD) − FloorHeight`. Si `d <= 0` ou `d > a3` : demi-tour (`TA = a1&0xFF`, `stop6`, `TD = QuarterTurn[TD]`). Si `0 < d <= a3` : `TA = a2&0xFF`. Retourne `d`.
- `0x8003AB54` GetEntityTileHeight (listing seul) : vitesse de l'animation `+8` de la fiche × table de direction `0x80023654/0x80023694`, puis `0x8003A9E0` = hauteur maximale du pied sur les 4 coins (`0x07800000` si bloqué).

## 3. Les gestionnaires (pseudo-code du binaire, chaque ligne vérifiée au §0)

### 3.1 A0 `0x8006174C` et E1 `0x8007ED10`

- **A0** : `AI0 = (s16) lh(+0x11E)`, demi-mot haut signé de `PosZ`. Aucun tirage, aucun autre champ.
  - La DLL a un A0 pour le Muruta seul : `AlundraMurutaAi.Load` (:52), `(PosZ + 1) >> 16`. **Les hauteurs de la DLL sont celles du binaire moins 1** (ADR-0026).
  - Seule la Guêpe lit `AI0` (sa hauteur de base de vol). Pour les quatre autres, la valeur écrite n'est lue par personne, mais l'image d'entité de l'oracle la verra.
- **E1** : `DestroyEntity(self, −1)`, effet de bris 8 pour les cinq. Butin : un code `Contents` >= 0x80 tire `below(16)` à la création (INFÉRENCE d'après `muruta.md` §5.7, donc les Orcs avec 252 ; le Poisson et la Guêpe avec 0 ne tirent pas). Domaine E20.e0.

### 3.2 Poisson 392 : C19 `0x8006A974` + D6 `0x8007DCD8`

`rel` est à `sp+0x10`. Aiguillage par chaîne de comparaisons sur `TA` (0, 1, 5, 7 ; autre : sortie).

```
C19:
 TA == 1 (nage):
   si |dx|<3 et |dy|<3 et non(0x100000 < |dz|):       // le héros est tout près
        B0 = 1 ; r = below(16) ; TA = 0 ; AI1 = r + 0x3C ; fin           // 1 tirage
   si FA == 0 et CanMoveForward(self, 0) == 0 : fin                      // rien ne bloque
   r = below(0x20) ; TA = 0 ; AI1 = 0x3C ; TD = r ; fin                  // mur ou vide : nouvelle direction (1 tirage)
 TA == 0 (repos):
   si AI1 != 0 : AI1-- ; fin
   si B0 == 0 : TA = 1 ; fin
   TD = versHéros ; TA = 3 ; B0 = 0                                     // le saut-morsure
 TA == 5 (retombée): si OG == 0 : fin ; TA = 0 ; AI1 = 0x14 ; PlaySoundEffect(0x96)
 TA == 7 (fin du recul): si END == 0 : fin ; si B3 != 0 : TA = 8 ; Flags |= 0x40
                         sinon TA = 0 ; AI1 = 0x14 ; B0 = 0
D6:  r = ComputeNewHp(self) ; si r : B3 = 1 ; TA = 6 ; TD = loinDuHéros    // forme de HitReaction(6)
```

- Champs lus : `+0x88 +0x274 +0x282 +0x13C +0x144 +0xAC +0x277`, `rel`, `MapHeights +0x158..+0x164`, `TD`.
- Champs écrits : `+0x88 +0x8C +0x274 +0x282 +0x6C`.
- Tirages : 0 sauf en état 1 (1 tirage : `below(16)` près du héros, `below(32)` sur obstacle). D6 : 0.
- Son propre : 0x96 à la retombée. Sons d'animation : 150, 65, 64.
- Chaîne : coup, puis D6, puis animation 6 (4 t), puis 7 (Hold), puis C19 état 7, puis mort 8 ou repos.
- Comportement (INFÉRENCE d'après le pseudo-code et la planche) : il nage en ligne droite jusqu'à un mur ou un vide. Le héros à moins de 3 cases (dz <= 1 hauteur) l'arme (B0). Après 60 à 75 ticks de repos il bondit vers lui, retombe et repose 20 ticks. Un coup d'épée (2 dégâts) le tue (vie 2).

### 3.3 Guêpe 455 : C18 `0x8006A564` + D11 `0x8007DEE8`

`rel` vient de `0x8007FE8C` (dz = `PosZ` − sol du héros), à `sp+0x18`. Table `0x80027394` : 0 `0x8006A5AC`, 1 `0x8006A7EC`, 2 `0x8006A864`, 3 `0x8006A888`, 4 sortie, 5 `0x8006A920`, >= 6 sortie.

```
C18:
 TA == 0:
   si B0 != 0:                                        // course d'attaque
        TD = versHéros ; AI1 = 0x3C ; B0-- ;
        TA = (dz > 0xFFFFF) ? ((dz > 0x180000) ? 3 : 1) : 2 ; fin       // dz signé = rel[5]
   si AI1 != 0:
        AI1-- ; si AI1 != 0 : fin ;
        AI1 = 0x78 ;
        TA = (dz > 0xFFFFF) ? ((dz > 0x180000 et OG == 0) ? 3 : 1) : 2 ; fin
   // AI1 == 0
   si |dx|<3 et |dy|<3 et non(0x200000 < |dz|) : TD = versHéros ; AI1 = 0x3C ; fin
   r1 = below(32) ; r2 = below(41) ;                  // deux tirages DANS TOUS LES CAS
   TD = r1 ; t0 = PosZ(+0x11C) − (AI0 << 16) ; AI1 = r2 + 0x50 ;
   si t0 > 0x4FFFFF : TA = 3
   sinon si t0 > −0x500000 : TA = below(3) + 1        // 3e tirage seulement ici
   sinon TA = 2
 TA == 1, 2, 3:
   AI1-- ; (TA==3 : si AI1 == 0 ou OG != 0 : TA = 0) (TA==1,2 : si AI1 == 0 : TA = 0)
   si B0 != 0 : fin
   si TryAttackPlayerFront(self, rel, 1, 4, 0x200000) : TA = 0 ; TD = versHéros ; AI1 = 0 ; B0 = 10 ; fin
   (TA==1 seulement) si FA != 0 : TA = 0 ; TD = (TD + 4) & 0x1F ; fin    // pas de course d'attaque après un mur
 TA == 5: si END == 0 : fin ; si B3 != 0 : TA = 6 ; Flags |= 0x40 ; sinon TA = 0 ; B0 = 0 ; AI1 = 0
D11: r = ComputeNewHp(self) ; si r : B3 = 1 ; TA = 4 ; TD = loinDuHéros     // HitReaction(4)
```

- Champs lus : `+0x88 +0x274 +0x280 +0x282 +0x11C +0x144 +0x13C +0x8C +0x98 +0xAC +0x277`, `rel`.
- Champs écrits : `+0x88 +0x8C +0x274 +0x282 +0x6C`.
- Tirages : 0, ou 2 (r1, r2), ou 3 (avec `below(3)`). D11 : 0.
- Aucun son propre (sons d'animation 65 et 64).
- **Pièges de portage**
  - `AI0` est lu **non signé** (`lhu`).
  - `PosZ` du binaire = `PosZ` de la DLL + 1. Les bornes `0x4FFFFF` et `−0x500000` sont absolues.
  - `rel[5]` utilise `PosZ` et non le sol.
- Comportement (INFÉRENCE ; montée et descente viennent des impulsions Z +256 et −256 des planches) :
  - Il plane autour de sa hauteur de naissance (± 5 hauteurs) et change d'allure toutes les 80 à 120 ticks.
  - Près du héros, il le fixe.
  - S'il le voit dans la fenêtre avant (±1 case de côté, 4 de profondeur, dz <= 2 hauteurs), il lance 10 passes de poursuite.
  - Un coup tue (vie 2).

### 3.4 Orc hache 338 : C15 `0x80069C84` + D12 `0x8007DF4C`

`rel` est à `sp+0x18`. Table `0x80027334` : 0 et 1 `0x80069CCC`, 2 sortie, 3 `0x80069E40`, 4 sortie, 5 et 6 `0x80069ECC`, >= 7 sortie.

```
C15:
 TA == 0 ou 1:
   si B0 != 0 : TA = 3 ; TD = versHéros ; fin                              // attaque en cours
   si B1 == 0 : B1 = 4 ; stop6 ; TD = (Cardinal[AD] − 8) & 0x1F            // quart de tour (Cardinal = 00 10 08 18)
   TA = 1 ;
   si AI1 == 0:
        AI1 = 0x1E ;
        si TryAttackPlayerFront(self, rel, 2, 2, 0) :                      // fenêtre 2×2, |dz| == 0
             si below(5) == 0 : PlaySoundEffect(0x94) ;  B0 = 3
   sinon: AI1-- ; si FA != 0 : stop6 ; B1-- ; TD = (Cardinal[AD] − 0x10) & 0x1F     // demi-tour au mur
 TA == 3 (frappe): si END == 0 : fin ; B0-- ;
        si B0 == 0 : r = below(4) ; TA = 0 ; AI1 = 0 ; B1 = 0 ; TD = r << 3
        sinon CA = 0xFFFFFFFF                                              // relance l'animation 3
 TA == 5 ou 6: si END == 0 : fin ;
        si B3 != 0 : TA = 2 ; Flags |= 0x40
        sinon : si |dx| < 2 et |dy| < 2 : B0 = 3 ;  TA = 0                  // riposte
D12: voir 3.7
```

- Tirages : 0 ; 1 (`below(5)`) au lancement d'une attaque ; 1 (`below(4)`) à la fin de la dernière frappe.
- Son : 0x94 (1 fois sur 5). Sons d'animation : 29 (frappe), 64, 65, 225.
- Champs écrits : `+0x88 +0x8C +0x90 +0x274 +0x275 +0x282 +0x6C` et `stop6`.
- **Piège** : `AD` (+0x98) est la valeur du tick (`BoxDirection`), comme pour `TryAttackPlayerFront`.
- Comportement : il marche en ligne droite et fait demi-tour à chaque mur (compteur `B1` initialisé à 4). Quand `B1` retombe à 0, il fait un quart de tour. Toutes les 30 ticks (`AI1`), il regarde si le héros est devant lui à moins de 2 cases et à hauteur égale. Il attaque alors 3 fois de suite (animation relancée par `CA = −1`). Touché, il riposte 3 coups si le héros est à moins de 2×2.

### 3.5 Orc masse 341 : C16 `0x80069F44` (+ D12)

Table `0x80027354` : 0 `0x80069F8C`, 1 `0x80069FC0`, 2 sortie, 3 `0x8006A18C`, 4 sortie, 5 `0x8006A220`, 6 `0x8006A1F4`, >= 7 sortie. Les animations 2, 4 et 7 n'ont pas de code dans C16 (7 s'enchaîne sur 3 par la planche).

```
C16:
 TA == 0: si AI1 != 0 : AI1-- ; fin
          StartFlying(self, anim 1, range 0x78, chance 0x46) ; B1 = 0          // 70/101 vers le héros, sinon direction au hasard ; AI1 = 0x78 + below(16)
 TA == 1: AI1-- ;
     si AI1 == 0 : si B1 != 0 : B1-- ; r = below(32) ; AI1 = 0x78 ; TD = r
                   sinon : TA = 0 ; AI1 = 0x14 ; B0 = 0 ;  fin
     si TryAttackPlayerFront(self, rel, 3, 3, 0) : si below(5) == 0 : PlaySoundEffect(0x94) ; ATTAQUE
     sinon si FA != 0 :
         si B1 != 0 : B1-- ; r = below(32) ; AI1 = 0x78 ; TD = r
         sinon : B0++ ; si B0 < 4 : TA = 0 ; fin ; B0 = 0 ; r = below(32) ; AI1 = 0x78 ; B1 = 3 ; TD = r
 ATTAQUE (0x8006A258) : TA = 7 ; TD = versHéros ; W28C = 0
 TA == 3 (charge): W28C++ ; si (W28C & 7) == 0 : CreateEffectEntity(0, mapC, 0, PosX, PosY, FloorHeight)    // poussière au pied
          si END != 0 : TA = 0 ; AI1 = 0x1E
 TA == 5: si END == 0 : fin ; si B3 != 0 : TA = 2 ; Flags |= 0x40 ; sinon ATTAQUE        // riposte immédiate
 TA == 6 (parade finie): si END != 0 : TA = 0 ; AI1 = 0x14 ; TD = (TD + 0x10) & 0x1F
```

- `mapC` est l'octet `+0xA` de la structure pointée par `[0x800E4334]` (7 sur toutes les cartes de la masse).
- Tirages :
  - État 0 avec `AI1 == 0` : `StartFlying` tire 2 nombres (cap vers le héros) ou 3 (cap au hasard).
  - État 1 : 1 tirage (`below(32)` sur obstacle ou fin de jambe, `below(5)` sur attaque).
- Effets : jusqu'à 5 poussières par charge (compteur 8, 16, 24, 32, 40 sur 43 ticks).
- **Pièges**
  - `W28C` est un mot de 32 bits. La décompilation le coupe en `AIValues[6]` et `[7]` de 16 bits, sans conséquence ici, mais la DLL a `short[] AIValues`.
  - Le `FloorHeight` de l'effet est celui du binaire, soit celui de la DLL + 1.
- Comportement : repos de `AI1` ticks, puis marche de 120 à 135 ticks vers le héros (70 %) ou au hasard, avec jusqu'à 3 réorientations sur mur. Le héros dans la fenêtre 3×3 devant lui déclenche l'armement (animation 7, 24 ticks) puis la charge (animation 3). Touché, il recule (4 puis 5) puis riposte (animation 7).

### 3.6 Orc armure de fer 344 : C17 `0x8006A29C` + D5 `0x8007DBE0`

`rel` est à `sp+0x10` : `|dx|` en `+0x10`, `|dy|` en `+0x14`, `|dz|` en `+0x18`, `dx` en `+0x1C`, `dy` en `+0x20`. Table `0x80027374` : 0 `0x8006A2E4`, 1 `0x8006A3A4`, 2 à 4 `0x8006A4C8`, 5 sortie, 6 `0x8006A4E4`, >= 7 sortie.

```
C17:
 TA == 0 (regard): si AI1 == 0 : r = below(32) ; AI1 = 0x3C ; TD = r ;
          AI1-- ; si AI1 != 0 : fin ; TA = 1 ; AI1 = below(32) + 0x78
 TA == 1 (marche): AI1-- ; si AI1 == 0 : TA = 0 ; fin
     si dy <= 0 et |dx| < 4 et |dz| <= 0x100000 :                 // le héros est « au sud », à moins de 4 colonnes
         si |dx| == 0 et dy >= −4 : TA = 4 ; TD = 0 ; fin
         si dy >= −1 : TA = 2 ; TD = (dx < −1) ? 0x18 : 0 ; fin
         si dy >= −4 : TA = 3 ; TD = (dx < −1) ? 0x18 : 0 ; fin
     // sinon (dy < −4) on retombe ici
     si FA != 0 : TD = (TD − 0x10) & 0x1F ; AI1 = (AD != 0) ? 0x78 : 0xF0 ; TA = 1 ; fin
     si CanMoveForward(self, 0) : TA = 0 ; AI1 = 0              // un vide devant : il s'arrête
 TA == 2, 3, 4 (attaques de 99 ticks): si END != 0 : TA = 0 ; AI1 = 0
 TA == 6: si END == 0 : fin ; si B3 != 0 : TA = 7 ; Flags |= 0x40 ; sinon TA = 1 ; TD = versHéros ; AI1 = 0x78
D5: TD = loinDuHéros ; nib = TouchingEntity.CurAttack[0] & 0xF ;
    si (nib − 1) <u 3 et type(+0x68) == 0x1A9 et TA <u 2 :           // Homme momie 425 SEULEMENT
         si below(100) >= 0x3C : TA = 11 ; fin                        // 40 % : esquive, aucun dégât
    r = ComputeNewHp(self) ; si r : B3 = 1 ; TA = 5
```

- Tirages C17 : 0 ou 1, jamais 2 dans un appel.
- Tirages D5 : **0 pour l'Orc de fer**. Le tirage n'a lieu que pour le type 425, branche morte tant que 425 n'est pas enregistré.
- Aucun son propre. Sons d'animation : 35, 65, 64.
- **Les attaques n'ont que deux orientations** : bas (0) ou droite (0x18). Le binaire n'a pas de cas « héros à gauche ».
- Comportement : il regarde 60 ticks puis marche 120 à 151 ticks et s'arrête devant un vide. Le héros « au sud » à moins de 4 colonnes déclenche une attaque de 99 ticks (type 4, 2 ou 3 selon la distance verticale). Touché (recul 5 puis 6), il marche vers lui pendant 120 ticks.

### 3.7 D12 `0x8007DF4C` (Orc hache 338 et Orc masse 341)

```
TD = loinDuHéros ; nib = TouchingEntity.CurAttack[0] & 0xF
si (nib − 1) <u 3 :                       // attaque 1, 2 ou 3 (l'épée est le nibble 1)
     r = below(100)                       // UN tirage pour les deux types, quel que soit l'état
     si r >= 0x46 (70) :                  // 30 %
         si type == 0x152 (hache) :  si TA <u 2 : TA = 6 ; fin
         si type == 0x155 (masse) :  si TA <u 2 ou TA == 7 : TA = 6 ; fin
r = ComputeNewHp(self) ; si r : B3 = 1 ; TA = 4
```

- La parade (animation 6, aucun dégât) vaut pour la hache dans les états 0 et 1, et pour la masse dans les états 0, 1 **et 7 (armement)**.
- Tirages : 0 si le nibble n'est pas 1 à 3, sinon 1.

## 4. Écarts avec la décompilation (le binaire gagne)

| # | Écart | Preuve | Effet si on portait la décompilation |
|---|---|---|---|
| E1 | **Guêpe : `FUN_8007fe8c`** (`FunctionTypeC.cs:8112`) calcule `dz = entity.FloorHeight − hero.FloorHeight`. Le binaire lit `self.PosZ (+0x11C)` (`0x8007FEA4`) | mutant « sol au lieu de PosZ » : 264 écarts sur 4 500 vecteurs | l'altitude relative de la guêpe serait fausse |
| E2 | **Guêpe état 1** : après la branche du mur (`FA != 0`), la décompilation retombe dans la course d'attaque (`FunctionTypeC.cs:8049-8056` : `TA=0 ; TD=versHéros ; AI1=0 ; B0=10`). Le binaire sort à `0x8006A960` après `TD = (TD+4)&0x1F` | mutant : 93 écarts sur 4 500 | la guêpe contre un mur se mettrait en poursuite au lieu de tourner de 45° |
| E3 | **Direction au hasard** : `Random.Next() % 32` (`FunctionTypeC.cs:7955`, `EntityGameplayManager.cs:110` dans « StartFlying ») donne les 5 bits **bas** du mot. Le binaire prend `(s·32) >> 32`, les 5 bits **hauts** (`0x8007FFA0-0x8007FFB8`, `0x8006A6D0`) | `S/decomp_variants.py` : 431 vecteurs sur 1 466 qui tirent diffèrent pour C16, 309 sur 317 pour C18 état 0 | toutes les directions tirées au hasard par la masse et la guêpe seraient fausses |
| E4 | **D12 : la parade de l'Orc masse est perdue** (`FunctionTypeD.cs:277-301`). Le test du type 0x155 est imbriqué dans le bloc du type 0x152 (code mort) et se contredit (`< 2` puis `== 7`) | `S/decomp_variants.py` : 6,1 % de tous les vecteurs D12 de la masse diffèrent (tirage compris). Le mutant « pas de parade à l'animation 7 » en donne 19 à 37 | 30 % des coups d'épée sur la masse (dans les états 0, 1 et 7) passeraient au lieu d'être parés |
| E5 | **`CanMoveForward`** (`FunctionTypeC.cs:10799-10857`) : sens du dernier test inversé (`f2 <= adj \|\| adj < f0` au lieu de `b2 && b0`) | `S/decomp_cmf.py` : **171 cas sur 1 536 diffèrent**. Sur terrain plat avec `TD < 16`, la décompilation répond « vide devant » (1) | le poisson (C19) et l'Orc de fer (C17) s'arrêteraient et tourneraient en permanence |
| E6 | `W28C` (poussière de la masse) : la décompilation lit et écrit `AIValues[6]` en 16 bits et remet `[6]`, `[7]` à zéro (`FunctionTypeC.cs:811-812, 846-847`). Le binaire utilise un mot de 32 bits | lecture du code (`lw/sw +0x28C`) | aucun en jeu (le compteur reste < 64) |
| E7 | Noms trompeurs : « `SetSpawnFlagFromZPos` » (`FunctionTypeA.cs:9`) est `AI0 = PosZ>>16` ; « `StartFlying` » démarre une marche ; « `CanMoveForward` » renvoie 1 quand c'est **impossible** | binaire | confusions de portage |
| E8 | Gardes par nom d'entité (`entity.Name != "…"` puis `Breakpoint.TriggerBreak`) : absentes du binaire | binaire | aucun (à ignorer) |

Écarts cherchés et **non trouvés** : C15 en entier, C17 (hors `CanMoveForward`), C19 (hors `CanMoveForward`), C16 (hors E3 et E6), D5, D6, D11, corps d'A0, E1, `TryAttackPlayer` `0x80080704`.

## 5. Ce que la DLL a déjà et ce qui manque (FAIT, `Alundra/Scripts/`)

| Besoin | Existe | Manque ou piège |
|---|---|---|
| Inscription | `AlundraNativeSpriteRegistry.Register(slot, code, types, handler)` (:47) | **Lève sur une clé déjà inscrite** (:53-55), alors que A0 est déjà pris par le Muruta (`AlundraMurutaAi.Register`, :40) et que D5, D6, D11, D12 seront partagés par d'autres tranches. Il faut une inscription **cumulative par type** (b0). `TryGet` (:63) cherche déjà par type |
| Contenu actuel | 7 clés (A1, C1, D1 Beannoïde ; A0, C10, D9 Muruta ; C61 vague) | b1 ajoute 9 clés (C15 à C19, D5, D6, D11, D12) et étend A0 : 16 clés |
| Position relative | `RelativePosition`, `RelativePositionFromPosZ` | - |
| Fenêtres | `TryAttackPlayerFront`, `TryAttackPlayer2` | `TryAttackPlayer` (non utilisée par b1) |
| Marche | `StartWalk` (= `0x8007FEF8`), `ZeroForces`, `CardinalDirections`, `QuarterTurnTable` | - |
| Vide devant | arbre de décision dans `HandleAnimationDirection` (:95-170) | **`CanMoveForward(self, dist)`** à extraire (0 écart sur 40 000 cas) |
| Dégâts | `AlundraDamage.ComputeNewHp` (:76) ; `AlundraEnemyHelpers.HitReaction(self, anim, context)` (:224). **D6 = `HitReaction(6)`, D11 = `HitReaction(4)`** | D5 et D12 : lire le nibble de l'attaque, tirer `below(100)`, parer, comparer le **type** (`SpriteType` 425, 338, 341). Le nibble se lit par `(TouchingEntity.GameplayProxy as AlundraEntityScriptProxy)?.CurrentAttack?.Attribute & 0xF` (`AlundraEntityScriptProxy.HitPass.cs:25`). Sans attaque : nibble 0, donc pas de tirage et dégâts normaux |
| Champs | `Bytes` (`byte[4]`, :1214) ; `AIValues` (`short[10]`, :1217 : lire AI1 et AI0 en `unchecked((ushort)…)` comme le Muruta) ; `TargetAnimationId` (:145) ; `ForceResetAnimationFlag` (= `END`, :154) ; `ForceAdjusted` (:384) ; `IsOnGround` (:404) ; `MapHeights` (:406) ; `FloorHeight` (:352) ; `PosZ` (:340) ; `TileX`, `TileY` (:341-342) ; `CurrentAnimationId` (:147) ; `Flags` (:143) | `W28C` : mot de 32 bits (`AIValues[6]` en entier non signé suffit) |
| Sol et coins des entités | `UpdateEnemyGroundHeights` à la naissance (`AlundraEntitySpawnFactory.cs:817`) et dans la physique (`AlundraEntityScriptProxy.cs:834`) pour tout `SpriteType >= 0` | - |
| Relance d'animation (`CA = −1`, C15) | la synchro compare `CurrentAnimationId` et la cible (`AlundraFrameSyncPasses.cs:506`) | à vérifier contre l'idiome `~TargetAnimationId` d'E19.c2 (:483) ; pas de conflit pour la cible 3 |
| Hauteurs | « DLL = binaire − 1 » (ADR-0026) | C18 : `t0 = (PosZ + 1) − (AI0 << 16)`. A0 : `(PosZ + 1) >> 16`. C16 : z de l'effet = `FloorHeight + 1` |
| Poussière de la masse | `AlundraEffectReservoir.Create(false, effect, 0, x, y, z)` (:220-221) via `IEntityWorldContext.EffectStage` (:199) | **Couture pour l'octet C de la carte.** Le convertisseur l'exporte déjà (propriété Tiled `C`, `TiledMapExporter.cs` vers :273 ; `AlundraEntitySpawnFactory.cs:886-888` lit déjà `Gravity` et `ZViscosity` dans `CustomProperties`). La DLL ne l'expose pas aux gestionnaires |
| Sons | `context.SoundPlayer?.PlaySfx(0x94 / 0x96)` | - |
| E1 et mort | en ligne pour tout type, effet de bris 8 (`AlundraWorldProxy.cs:3262-3278`) | butin : E20.e0c ; héros blessé : E20.c2b |
| Entrée du tick | `RunSpriteEvent` (`AlundraEventProgramRunner.cs:345-352`) : `NativeHandlersEnabled && NativeSprites.TryGet(slot, code, SpriteType)` | un héros absent (`PlayerEntity` nul) : les gestionnaires C rendent la main, comme C10 |
| Exports | `sprite-records.json` : les 5 types avec `BreakEffect` 8, `Contents` 0 ou 252, `AnimSets` (11, 7, 8, 7, 8) | - |

## 6. Tests existants qui changeraient de base quand ces gestionnaires deviennent vivants

Constat clé (FAIT) :
- `NativeHandlersEnabled` vaut **vrai par défaut** (`AlundraEventProgramRunner.cs:106`).
- Seul `AlundraHitArena` le met à faux (`AlundraHitArena.cs:68`). C'est avant tout tick, car le constructeur d'`ArcRun` n'avance aucune image.
- La carte 14 est le monde de plus de trente classes de tests ; la carte 15 n'est chargée que par un arc.
- Aucun test ne nomme les types 392, 455, 341, 338, 344, ni les clés C15 à C19, D5, D6, D11, D12 (recherche textuelle).
- Aucun autre test ne charge l'une des 31 cartes de b1. Les cartes jouées par les tests sont 10, 17, 26, 44, 45, 47, 83, 96, 109, 115, 116, 135, 162 à 165, 176 à 179, 185, 226, 260, 303, 311, 321, 389 à 392, 411, 445, 476, 478 (hors 14 et 15).

1. **Insulés (natifs coupés).** 17 fichiers emploient `HitArena` : `AlundraEnemyGroundTests`, `AlundraEnemyHitReactionTests`, `AlundraFlailBounceArcTests`, `AlundraFlailBounceWallProbeTests`, `AlundraHeroAuraWorldTests`, `AlundraHeroChargeArenaTests`, `AlundraHeroRunArenaTests`, `AlundraHeroSwimArenaTests`, `AlundraHitEffectWorldTests`, `AlundraHitPassCatchUpTests`, `AlundraHitPassTests`, `AlundraRandomPerTickTests.Advance`, `AlundraShotDirectionTests`, `AlundraStoredHitBoxTests`, `AlundraWaveSwordSupport`, `AlundraWaveSwordLifeTests`, `AlundraWaveSwordWiringTests`. Dans `HeroSwimArena`, les 2 Poissons du bassin existent mais restent inertes. **Exception** : `AlundraWaveSwordLifeTests` (:120) et `AlundraWaveSwordWiringTests` rallument les natifs pour leurs cas positifs, donc ils sont exposés.
2. **Exposés (carte 14, `ArcRun`, natifs vivants, héros à la case (8, 6), 2 000 images maximum).**
   - Classes : `AlundraBirthBoxTests` (6 tests), `AlundraBirthTickProbePaTests` (5), `AlundraBirthTickTests` (11), `AlundraCreationSwitchClockTests` (10), `AlundraEntitySlotV5RowsTests` (3), `AlundraEntitySlotWorldTests` (14), `AlundraHeroLifeArcTests` (`Map14`, :734), `AlundraLogicalZStepTests` (9), `AlundraLogicalZWorldTests` (12), `AlundraLootRollWorldTests` (3, mot du générateur après le chargement), `AlundraMapEdgeWorldTests` (5), `AlundraMapStateTests` (4), `AlundraMapStateWorldTests` (4), `AlundraRecordBirthClockTests` (5), `AlundraRecordBirthTickTests` (7), `AlundraSpawnBySpriteIndexInterfaceTests` (11, graine `0x12345678`), `AlundraWeaponSpawnSeamTests` (7), plus les deux de WaveSword ci-dessus.
   - Canaux de variation (INFÉRENCE, non exécutés) :
     - (a) **A0** de 3 Guêpes et 2 Poissons à la première passe d'événements : `AI0` écrit, visible dans toute image d'entité complète.
     - (b) **Le flux du générateur partagé** : chaque Guêpe tire 2 à 3 nombres à chaque changement d'allure (toutes les 80 à 120 ticks), les Poissons 1 sur obstacle. Tout test qui compare le mot du générateur ou une trajectoire qui en dépend après N images change. À regarder en premier : `AlundraLootRollWorldTests`, `AlundraSpawnBySpriteIndexInterfaceTests`, les tests de la vague d'épée.
     - (c) Mouvement, collisions de boîtes et liste des entités actives : les Guêpes volent, les Poissons nagent dans le bassin.
     - (d) `AlundraHeroLifeArcTests` : aucune blessure n'est possible sans E20.c2b, donc aucun changement de vie attendu.
   - Les 7 Beannoïdes de la carte 14 sont déjà vivants dans ces tests : leur base absorbe déjà ce type de variation.
3. **Carte 15 : `AlundraTarnMurggArcTests.R32`** (:20-23, héros case (26, 21), z 7, `FrameLimit` 1 500).
   - Les 3 Orcs masse naissent au chargement, aux cases (7,30), (7,36), (21,36), z 7 (INFÉRENCE sur l'unité des positions).
   - Ils marchent 70 % du temps vers le héros et peuvent atteindre sa zone avant la fin de la scène des Murggs (la durée de l'arc n'a pas été lue).
   - O-E14-2 (fermé) attendait que R3-2 ne bouge pas avec les Muruta de la 15 : à relire explicitement avec les Orcs, puis à fermer ou à repiquer.
4. **Registre.** `AlundraNativeSpriteRegistryTests.cs:51` (`Assert.Equal(6 + 1, NativeSprites.Count)`) passe à 16 clés après b1. Le test `:235-245` (`Registering_TheSameKeyTwice_Throws_…`) doit changer : la règle devient « type déjà inscrit pour cette clé », et non plus « clé déjà inscrite ».
5. **Inchangés attendus** (cartes sans les cinq types) : les arcs des leviers, `AlundraEntityContactArcTests`, `AlundraVisionArcTests`, `AlundraShipBlockArcTests`, `AlundraDay3SceneArcTests`, `AlundraInoaDayOneArcTests`.

Compte de départ à mesurer en T0 : le dernier relevé du dépôt est « suites 5092/0 » (commit `37a6df7`).

## 7. Ce qu'un oracle pour ces gestionnaires doit contenir (méthode `docs/plan-e14-annexe/e14a/oracle-a1/`)

**Réutilisable tel quel** (`notes.md` §3) :
- `tools/` : interpréteur `emu.py`, second interpréteur `mips.py`, passes d'entités réelles, générateur, `0x2C`.
- `e14world.py` : machine à enregistrement par appel, tables de gestionnaires bouchonnées sauf l'ensemble `LIVE`, cellules réelles de `map_N.json`, gabarit d'effacement `0x80134368`, `FIELDS`.
- `e14vec.py` (vecteurs d'un appel), `e14model.py` (modèle indépendant), `e14cov.py`, mutations d'un mot.
- `build_world` pose déjà `[0x800E4334]` et l'octet C = 7 (`tools/world.py:108-114`). La poussière de la masse est donc couverte : l'effet `0x8003BDD8` est un bouchon journalisé qui rapportera `(0, 7, 0, x, y, z)`.

**À ajouter pour b1**
1. **Cinq « sortes »** dans `KINDS` :
   - `poisson` (carte 14, indice 136), `guepe` (14, 199), `orc_masse` (15, 85), `orc_hache` (4, 82), `orc_fer` (3, 88) ;
   - leur fiche de balance (392, 455, 341, 338, 344 : niveau 255 seul) ;
   - `LIVE` étendu à C15 à C19, D5, D6, D11, D12 (A0 et E1 y sont déjà) ;
   - cellules du bassin de la carte 14 pour le Poisson (INCONNU : eau et physique du bord pour une entité non héros).
2. **Vecteurs d'un appel.** Le prototype est terminé (`S/vec.py` + `S/models.py` : 9 000 par gestionnaire, 100 % des instructions, 10 mutants tués). À rejouer avec le vrai `ComputeNewHp` et les vraies passes pour D5, D6, D11, D12 (le prototype les bouche), et à écrire au format des `vectors-*.json` lus par `AlundraEnemyHandlerOracle`.
3. **Scénarios** (héros fantôme, tirages forcés par `seed_chain`)
   - Poisson : `PO-calm`, `PO-close` (morsure 3 puis 5 puis repos), `PO-wall` (bord du bassin réel), `PO-ledge`, `PO-hurt`, `PO-death`.
   - Guêpe : `GU-calm`, `GU-high` et `GU-low` (bornes ±0x500000), `GU-attack` (10 passes), `GU-wall`, `GU-hurt`, `GU-death`.
   - Orc hache : `OH-patrol` (4 murs puis quart de tour), `OH-attack` (3 frappes, tirage 4), `OH-hurt-counter`, `OH-parry` (tirage >= 70), `OH-death`.
   - Orc masse : `OM-walk` (70 % et 30 %), `OM-charge` (armement 7 puis 3, poussière tous les 8 ticks), `OM-wall` (B1 = 3, B0 = 4), `OM-parry` (états 0, 1 et 7), `OM-hurt-counter`, `OM-death`.
   - Orc de fer : `OI-idle-walk`, `OI-charge` (zone sud, animations 4, 2, 3), `OI-wall` (0x78 et 0xF0), `OI-ledge`, `OI-hurt-walk` (5 puis 6), `OI-death`.
4. **Calendrier de mort à mesurer** : coup, puis D (H+1), puis reculs (6 puis 7 pour le Poisson, 4 puis 5 pour la guêpe et les orcs, 5 puis 6 pour l'Orc de fer), puis `END`, puis animation de mort, puis `Flags |= 0x40`, puis E1. À comparer à celui du Muruta (H+23, effacement H+24).
5. **Contrôles** : second interpréteur sur toutes les sorties ; modèle indépendant (`S/models.py` peut en tenir lieu s'il est réécrit depuis le binaire par un autre agent) ; au moins 12 mutants (`S/mut.py` en donne 10) ; couverture 100 % ; deux exécutions complètes au même manifeste ; porte D1 inchangée.
6. **Limites à écrire d'avance** : sol de test plat sauf cartes réelles ; effets et butin bouchonnés (E20.e0) ; poussière journalisée seulement ; héros fantôme (E20.c2b) ; physique Z de la Guêpe (impulsions ±256 sans gravité) à observer dans les scénarios `GU-*` avant d'écrire le port.

## 8. Questions ouvertes

| Réf. | Question (en termes concrets) |
|---|---|
| Q-E14-b3 (plan §2) | b1 s'accepte-t-elle sans la blessure du héros (E20.c2b) ni le butin (E20.e0c) ? On verrait poissons, guêpes et orcs bouger, attaquer, parer, reculer et mourir, mais **le héros ne perdrait aucune vie et rien ne tomberait** |
| Q-b1-1 | Porter la **parade de l'Orc masse** (30 % des coups d'épée parés dans les états 0, 1 et 7), que la décompilation a perdue ? Règle de l'auteur : le binaire tranche. Recommandation : oui |
| Q-b1-2 | Porter la branche **« Homme momie » de D5** (esquive 40 %), morte tant que 425 n'est pas enregistré ? Recommandation : oui (corps partagé, fidèle au binaire, vecteurs 425 dans l'oracle) |
| Q-b1-3 | Pour la vingtaine de classes de tests de la carte 14 qui ne coupent pas les natifs : les laisser exposées et ne repiquer que ce qui bouge (recommandé, avec un verifier frais), ou couper les natifs dans chacune ? |
| Q-b1-4 | **Octet C de la carte** : exposer la propriété exportée `C` (comme le binaire lit `[0x800E4334]+0xA`) ou coder 7 ? Recommandation : lire la propriété (7 sur les 13 cartes de la masse, mais c'est une donnée de carte) |
| Q-b1-5 | Après b1, les cartes 14 et 15 gardent du monde inerte (`Boss lézard` 494, `Mille-pattes` 488, `Muruta (boomerang)` 356, projectiles 371). Sont-ils attendus au jour 2 ? (Q-E14-b2) |
| INCONNU-1 | Physique du Poisson dans le bassin pour une entité non héros : eau profonde, `IsOnGround` pendant la retombée, murs du bord |
| INCONNU-2 | Physique verticale de la Guêpe dans la DLL : impulsion ±256 d'une animation en boucle sur une entité sans gravité (`AlundraEntityScriptProxy.cs:1036-1045`, E19.d2c1) |
| INCONNU-3 | Comportement des enregistrements hors histoire (cartes 3, 7, 11, 48, 69, 71 à 75, 119, 120) et des 8 Poissons scriptés de la carte 139 après un `0x40 [C,0]` (chemin déjà exercé par le Muruta de la carte 115) |
| INCONNU-4 | Durée de l'arc R3-2 et distance réelle des Orcs (non lues) |

## 9. Liste de tâches proposée pour E14.b1

Un commit par tâche, tests rouges d'abord, verifier frais en fin de tranche. La tranche dépend de b0 (inscription cumulative par type, `CanMoveForward`, couture « octet C »). Si b0 n'est pas faite, T2 les prend.

- **T0 - Annexe et base** (aucun code)
  - Copier cette découverte en annexe (`docs/plan-e14-annexe/e14b1/` : rapport, listings, `models.py`, `vec.py`, `mut.py`, `animsheets.md`, `records_b1.txt`).
  - Relever le compte de départ de la suite (Release et Debug) et l'état de `AlundraTarnMurggArcTests` et des classes du §6.
  - Trancher Q-E14-b3 et Q-b1-1 à Q-b1-4 avec l'auteur.
  - Écrire l'ADR (écarts E1 à E5 de la décompilation, sens inverse de `CanMoveForward`).
- **T1 - Oracle b1** : cinq sortes, `LIVE`, scénarios du §7.3, vecteurs réels (vrai `ComputeNewHp`), calendrier de mort, modèle indépendant, 12 mutants minimum, couverture, double exécution au même manifeste, sorties versées en texte (comme O-E14-6).
- **T2 - Socle** (si b0 absente)
  - Registre cumulatif par type (test « même clé deux fois » réécrit).
  - `AlundraEnemyHelpers.CanMoveForward` (extraire la décision de `HandleAnimationDirection` ; test sur les 82 944 cas).
  - Couture de l'octet C de la carte dans `IEntityWorldContext`.
  - Lecture du nibble d'attaque du `TouchingEntity` pour la parade.
  - Tout rouge d'abord.
- **T3 - Poisson** (A0, C19, D6) : vecteurs et scénarios `PO-*` contre l'oracle. C'est la plus courte (D6 = `HitReaction(6)`).
- **T4 - Orc hache** (A0, C15, D12), puis **T5 - Orc masse** (A0, C16, D12 avec parade en état 7, poussière, `StartWalk`) : D12 est partagé, un seul corps.
- **T6 - Orc armure de fer** (A0, C17, D5, branche 425 morte mais testée par vecteurs).
- **T7 - Guêpe** (A0, C18, D11) : `PosZ + 1`, bandes ±0x500000, scénario `GU-*` d'altitude. INCONNU-2 d'abord.
- **T8 - Câblage de production et bases**
  - `CreateForProduction` (16 clés) et test du registre (:51 passe à 16).
  - Arcs : carte 14 (poisson et guêpe), `AlundraTarnMurggArcTests` R3-2 relu (O-E14-2).
  - Classes du §6 relancées, repiquage de ce qui bouge avec preuve.
  - Arc de mort pour l'effet de bris 8 des cinq (E1 est déjà en ligne).
- **T9 - Verifiers frais** (plan puis résultat) et recette en jeu des cartes 14 et 15 par l'auteur.

## 10. Fichiers (dans `S/`)

- Pseudo-codes exécutables : `models.py`.
- Preuves : `vec.py`, `vec_b0.py`, `mut.py`, `decomp_cmf.py`, `decomp_variants.py`, `dll_had.py`, `had_vs_cmf.py`.
- Harnais : `h.py`, `emu/` (copie de l'interpréteur), `dis2.py` (désassembleur annoté), `exelib.py` et `mipsdis.py` (copies des aides en lecture seule de `e14-roster/`).
- Données : `animsheet.py` → `animsheets.md` ; `records.py` → `records_b1.txt`.
- Listings : `listings_handlers.txt`, `listings_helpers.txt`, `lst/`.
- Sorties : `vec_report.txt`, `vec_final_stdout.txt`, `decomp_cmf_out.txt`, `dll_had_out.txt`, `had_vs_cmf_out.txt`, `vec_b0_out.txt`.