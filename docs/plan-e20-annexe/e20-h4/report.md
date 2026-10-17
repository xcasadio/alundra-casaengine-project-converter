# Rapport h4 : magies des tomes (E20.h4) et cadre d'usage d'objet (E20.h0)

Lecture seule : rien n'a été écrit dans le dépôt ni dans le binaire. Tout est dans le dossier de travail `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/h4/`, appelé `H4/` ci-dessous.

- `H4/lst/` : les listings du binaire.
- `H4/oracle/` : les outils.
- `H4/results/` : les sorties enregistrées.

**Légende des preuves.**
- [L] : lu dans le désassemblage.
- [V] : comparé au vrai code MIPS sur vecteurs aléatoires.
- [M] : mesuré dans le jeu émulé (MovePlayer, pilote, `SpawnWarpEntity`, passes d'entités et passe de coups, tous réels).

Les étiquettes FAIT, INFÉRENCE et INCONNU suivent la demande.

## 0. Résumé des faits qui changent le plan

1. **Le « vent bas » (I50) n'est pas un warp.** C'est une tornade (sprite 24) qui attire les ennemis (C65). FAIT [L][V][M].
   - La décompilation nomme `IsFadeActive` la fonction `0x800351B8`. Cette fonction n'est pas un fondu. Elle teste « pente 4, ou animation du héros hors `0x32/0x33` » et appelle l'annulation `0x800350C0`.
   - L'hypothèse de `h-g-deps.md:109` est donc fausse.
2. **Les deux tomes d'eau font naître le même sprite 19.** Seules leurs tables diffèrent : 3 orbes pour I45, 6 pour I46. FAIT. Le sprite 18 (« vide ») n'est jamais créé : les 102 appels de `0x80039F58` ont été balayés.
3. **Coût et contrôles.** FAIT [L][V][M]. Le coût est de 1 PM. Si l'arme équipée est l'objet 7 (bâton magique), aucun PM n'est exigé ni consommé. À 0 PM sans bâton, le départ échoue (son 3). La décompilation lance quand même le sort dans ce cas (D1).
4. **Prérequis non planifié : la poussée `+0xCC/+0xD0`.** FAIT [L].
   - La DLL a les champs `PreviousAdjustedForceX/Y` (`AlundraEntityScriptProxy.cs:328-329`) mais aucun consommateur.
   - Le binaire les additionne à la force XY de l'entité à chaque tick, puis les remet à zéro (`0x800366FC`).
   - Les orbes d'eau (en orbite et en vol) et la tornade (attraction) n'avancent que par cette poussée.
5. **La requête de cibles `0x8003AF70` n'est appelée que par les sorts.** FAIT [L]. Les appelants sont `0x80035FA4` (vent 1), `0x8007ACB4` (C63), `0x8007AF5C` (C65) et `0x8007BF20` (C75). Deux défauts probables, à trancher (Q2) :
   - elle balaie les emplacements 1 à `count-1` : l'entité du dernier emplacement utilisé n'est jamais visée ;
   - elle ne trie qu'à partir de 3 résultats, par un quicksort de Hoare instable (`0x8003AE04`).
6. **Pendant qu'un objet est « en cours », le menu Start est refusé.** FAIT [L], `0x8002BC90`. Pour l'eau, cela dure jusqu'à 344 ou 394 ticks. La DLL a déjà l'emplacement du garde : `AlundraInventoryDirector.cs:17` et `:406-411`, « `g_warpLockTimer`… NO PORT EQUIVALENT ».
7. **E3 est partagé avec des ennemis.** FAIT [L]. 98 sprites de 75 cartes l'utilisent (balayage des en-têtes de `data-extracted/data/map_*.json`). E13 est utilisé par un sprite (carte 321, sprite 213). C63, C65, C75 et E28 n'apparaissent que dans la page globale. La clé de registre (E,3) devra être commune avec E14.
8. **Le départ lance une piste XA.** FAIT [L]. L'appel est `0x8005A9E0(byte[0x80023365+item])`, avec les octets 0, 1, 6, 6, 2, 3, 4, 5 pour `0x2B` à `0x32`. C'est le même mécanisme que l'opcode `0xB9`, que ni la DLL ni l'extraction ne savent lire (O-E19-36).
9. **Calendrier.** FAIT [M]. Le pilote est appelé avant l'aiguillage d'état (`0x80031CC4`). Le premier appel a donc lieu au tick suivant le départ, avec le compteur à 0. La première frappe du vent 1 (compteur 30) est perdue, car le héros est encore en `0x32` (32 ticks).
10. **Les tomes n'exigent pas E20.g.** Le départ teste seulement `hero+0x2C == 0`. L'enveloppe (ligne 11) peut le dire.

## 1. Le cadre d'usage (E20.h0)

Tous les points de cette section sont FAIT [L], sauf mention contraire.

**`TryUseItem` `0x8002ED64`**
- Pas de front du Rond (`[0x80126F2A] & 0x20`) : rend 1.
- Front du Rond et `[0x800DC4B8] != 0` (n'importe quel bit, pas seulement `0x34`) : son 3, rend 1.
- Sinon : `jal 0x8003499C` et rend son résultat.

**Départ `0x8003499C`**
- Lit `s0 = 0x8004E0F8()`.
  - Elle rend l'ancien `stats+0xC` si le compte est non nul, sinon -1.
  - Elle écrit en `stats+0xC` le meilleur objet de la case d'inventaire, par priorité de remplacement. C'est ce qui fait passer du tome haut au tome bas quand on possède les deux (cases 16 à 19, priorités 0 et 1, `0x800B9FE8`).
- Échec (son 3, rend 1) si l'un de ces cas se présente :
  - `s0 >= 0x62` non signé ;
  - `[0x80127164]` est non nul et différent de `s0` ;
  - `s0-0x1F >= 0x14` ;
  - l'objet est dans un trou de la table `0x8002339C` (`0x21`, `0x22`, `0x28`, `0x2A`).
- Un résultat 0 pose `[0x80127164]=item`, `[0x8012715C]=0` et `[0x80127160]=0`.
- Les objets instantanés rendent la constante 1 (`0x80034A38…`).

**Départ des tomes `0x800348E0`**, conditions dans cet ordre :
- `hero+0x2C == 0` ;
- `hero+0x88 < 2` non signé (Idle ou Moving seulement) ;
- `[0x80127164] == 0` ;
- si `0x8004E030() != 7` : `0x8004DEA4() != 0`, sinon son 3 ; puis `0x8004DF3C(1)` (SetMp de mp-1, borné à `[0, MpMax]`).
Puis XA, `hero+0x88 = 0x32`, rend 0. `UseItem` n'est jamais appelé : les tomes ne sont pas consommés.

**Pilote `0x8003634C`**
- Rend 1 si `[0x80127164] == 0`.
- Sinon il saute par `0x800235F4`, pour les objets `0x1F` à `0x32` :

| Objets | Pilote |
|---|---|
| `0x1F` | `0x80035204` (graines) |
| `0x20` | `0x80035260` (cape) |
| `0x23` | `0x800352C4` (bombe) |
| `0x2B` | `0x80035320` |
| `0x2C` | `0x800354D0` |
| `0x2D`, `0x2E` | `0x80035790` |
| `0x2F` | `0x80035A84` |
| `0x30` | `0x80035C64` |
| `0x31` | `0x80035EB0` |
| `0x32` | `0x80036218` |
| autres | rendent 1 |

- Il incrémente `[0x8012715C]` (sans dépasser `0x7FFFFFFF`), puis met `[0x80127164]` à 0 si le pilote a rendu non nul.
- Appelé en `0x80031CC4` (chaque tick libre) et en `0x80031C6C` (chemin de blessure, avant que l'animation passe à `0x31/0x3A`). Dans ce second cas le pilote fait donc un dernier pas avec l'ancienne animation.
- Les petits pilotes des graines et de la bombe rendent 1 si rien n'est porté, ou si l'objet porté n'est plus de type 1 ou 2. Ils annulent si la pente vaut 4. Le pilote de la cape reste actif tant que l'animation est dans `0x20` à `0x24` et annule au sol si la pente vaut 4, ou en l'air.

**Garde de pente `0x800351B8`**
- Si `hero+0x18C == 4` : annulation, rend 1.
- Sinon, si `hero+0x88` est dans `{0x32, 0x33}` : rend 0.
- Sinon : annulation, rend 1.
- Il est appelé au début des phases 0 et 1 des 8 pilotes de tomes, jamais en phase 2.
- Conséquence : tout changement d'animation du héros pendant le lancer (blessure `0x31`, dialogue, etc.) annule le sort au tick suivant.

**Annulation `0x800350C0`**
- Appelée en `0x80031BFC` (branche verrouillée), `0x80032758` (cas par défaut des états sans gestionnaire), `0x80032770` (branche `+0x188 & 0x80`), `0x800327B4` (mort), et par les pilotes (13 appels du garde, 3 des petits pilotes).
- Table `0x800233EC`, puis `[0x80127164] = 0` :

| Objet | Action |
|---|---|
| `0x1F` | `0x80034ACC` |
| `0x23` | `0x80034B54` |
| `0x2C` | `0x80034BDC` |
| `0x2D`, `0x2E` | `0x80034C54` |
| `0x2F` | `0x80034D2C` |
| `0x30` | `0x80034E08` |
| `0x32` | `0x80034EC4` |
| `0x2B`, `0x31` | neutre (les rochers et les frappes déjà nés continuent) |

- Effet des gestionnaires d'annulation (vérifié [V] à l'octet près, puis [M]) :

| Gestionnaire | Effet |
|---|---|
| `0x80034BDC` (terre 2) | les entités de type 17 au statut 2 passent à statut 3, `+0x20=0`, anim 1 |
| `0x80034C54` (eau) | effet 26 puis suppression sans effet de bris des type 19 |
| `0x80034D2C` (feu 1) | effet 19 (variante 2) puis suppression du type 20 ; type 23 à statut 3 |
| `0x80034E08` (feu 2) | rétablit le bloc de tremblement, type 21 à statut 3 |
| `0x80034EC4` (vent 2) | type 24 à statut 3, plus 16 effets 18 avec 3 tirages chacun |

**Reset.** L'initialisation du héros (`0x80031A48` appelle `0x8003295C`) remet `[0x80127164]` à 0.

**Autres lecteurs de `[0x80127164]`.** `0x8002BC90` refuse l'ouverture du menu (FAIT). `0x8002BB94` appartient au test `hold == 0x900` (Start+Select), probablement une remise à zéro douce (INFÉRENCE). Les états de cape (`0x800324D0`, `0x8003252C`) et la fonction terrain `0x80037568` (test `== 0x20`) ne concernent pas les tomes.

**États du héros pendant le sort.**
- Les états `0x32/0x33` partagent le gestionnaire `0x800325A4`, état 0x34 le gestionnaire `0x80032680`. Ils appellent `TryUseItem`.
- Les états `0x32` et `0x33` restent tant que `[0x80127164]-0x2B < 8`. Sinon ils passent à Idle (sol) ou `0x2D` (air).
- L'état `0x34` tient sans condition : il n'appelle que `TryUseItem`.
- Un Rond pendant le sort donne donc le son 3, car l'objet est déjà « en cours ».
- Animations, d'après `data-extracted` et l'export `alundra_0` :
  - `0x32` dure 32 ticks et chaîne vers `0x33` ;
  - `0x33` boucle ;
  - `0x34` dure 26 ticks et chaîne vers 0.
  - [M] confirme : `0x33` au tick T0+32, `0x34` posé par le pilote, retour à 0 26 ticks plus tard.
- Le héros ne bouge pas. Aucun état ne lit le pad.

## 2. Coût et contrôles : modèle Python contre le vrai code

Le modèle `H4/oracle/h4_start.py` (`model_try_use_item`) suit la section 1. Les aides (objet courant, arme courante) sont appelées en code réel dans la même machine.

**Résultats.**
- 5000 vecteurs : 0 écart, 142 instructions sur 142 couvertes (`H4/results/vec_start.txt`).
- Le mutant `0x62 -> 0x63` de la borne d'objet est équivalent : la borne `idx >= 0x14` qui suit donne le même observable.
- Mutants : 11 sur 13 détectés, les 2 restants sont équivalents (`h4_start_mut.py`).
  - Le second mutant équivalent est « règle d'objet en cours retirée » : pour un tome, le test `inuse == 0` qui suit donne le même résultat.

**Vérification dans la boucle de jeu réelle** (`H4/results/staff_check.txt`) :
- Bâton (arme 7) à 0 PM : le lancer démarre, MP reste à 0.
- Épée à 0 PM : son 3, rien ne démarre.
- Épée à 2 PM : démarre, MP passe de 2 à 1.
- Bâton à 2 PM : démarre, MP reste à 2.

**Autres faits.**
- La DLL plafonne `MpMax` à 4 (`AlundraPlayerManager.cs:1938`). Ses sauvegardes d'essai donnent `Mp 4 / MpMax 4`, les tomes niveau 1 (43, 45, 47, 49) et HP 4/10 (`AlundraTestSaves.cs:67-72, 96`).
- Le bâton n'est donné que par un script de la carte 134 (balayage du corpus).

## 3. Les huit pilotes

Toutes les tables sont lues dans `ALUN_CD.EXE`. Tout le tableau est vérifié [V], 6000 vecteurs, 0 écart, couverture quasi totale (voir §9). Détails à `H4/lst/drv_*.txt`.

**Tick réel.** `compteur c` vaut `tick − T0 − 1`. Une naissance au compteur `c` a lieu au tick `T0 + 1 + c`.

| Item | Pilote | Tables et paramètres |
|---|---|---|
| `0x2B` terre 1 | `0x80035320` | Table `0x8002343C` : 32 entrées de 6 octets (tick u16, vitesse Z s16 `<<8`, delta de direction s16), sentinelle 0xFFFF en `0x800234FC`. Ticks 20, 25 … 95, deux apparitions par tick. Vitesse Z 1024 (ticks 20 à 55) puis 1280 (60 à 95), soit +4 puis +5 px/tick. Sprite 16 en bas du héros, direction `(hero+0x8C + delta) & 31`. Phase 1 : à compteur ≥ 100, anim `0x34`. |
| `0x2C` terre 2 | `0x800354D0` | Table `0x80023504` : 15 entrées de 4 octets (tick, p), ticks 16, 32 … 192, deux à 224, un à 240, p de 512 à 7680 par pas de 512. Pour chaque rocher : deux tirages `<32` (r1 puis r2). Position = héros + `(X[r1], Y[r1]) × (p+0xA00)` (tables `0x80023654/94`), Z = héros + `0x1000000` (256 px), direction r2, ForceZ -8 px/tick sans gravité. Détruit sur place (`0x8003A774`) si `heroZ + 0x1000000 < terrain` de la nouvelle entité. Phase 1 : compteur ≥ `0x118`, anim `0x34`. |
| `0x2D` eau 1, `0x2E` eau 2 | `0x80035790` | Tables `0x80023544` (3 entrées) et `0x8002357C` (6 entrées), 14 octets : tick, p0, p1, p2, p3, angle de base, durée. Sprite 19 à héros + 8 px. Champs écrits : `+0x70=0xFE`, `+0x274..+0x27C = p0, p1, p2`, `+0x280 = p3`, `+0x284 = 1`, `+0x288 = 0`, `+0x28C = (base + p3×compteur)` modulo 256 à la C (reste signé), `+0x290 = durée − compteur`. Phase 1 : `[0x80127118]` compte les ticks. À 30 : eau 1 appelle `0x80032F84` (voir §5) puis son `0x31` ; eau 2 appelle `0x80033274` puis son `0x30`. À 60 : anim `0x34`. Phase 2 : attend que l'anim ne soit plus `0x34` ET qu'aucune entité de type 19 ait `+0x290 > 0`. |
| `0x2F` feu 1 | `0x80035A84` | Phase 0 : quand `hero+0x88 == 0x33`, sprite 20 au héros, mémorise pointeur `[0x8012711C]` et identité `[0x80127120]=+4`. Passe à la phase 1 dans le même tick. Phase 1 : tant que l'entité est vivante, si `compteur & 7 == 0`, un tirage `<32` donne la direction d'un sprite 23 à héros + 16 px ; il reçoit ForceZ `0x80000`, `+0x274=0x10`, `+0x278=0`. Quand l'entité meurt : anim `0x34`. |
| `0x30` feu 2 | `0x80035C64` | Quand l'anim est `0x33`, sprite 21 au héros, sauve les 9 mots `0x800E4338..` vers `0x80127138`. Chaque tick vivant : Flag=1 ; LimitX=SpeedX=`c>>6` ; LimitY=SpeedY=`c>>4`. À la mort : restaure les 9 mots, anim `0x34`. |
| `0x31` vent 1 | `0x80035EB0` | Table `0x800235E0` : compteurs 30, 40 … 100 (8). Ne s'exécute que si l'anim est `0x33`. À chaque compteur : requête `0x8003AF70(hero, ClassA, 11)`. Premier candidat qui passe (Z ≤ héros + 80 px, ≠ dernière cible) : si sa distance² ≥ `0x6401` la recherche s'arrête (pas de cible), sinon cible. Cible : sprite 22 à cible + 128 px, ForceZ `0xFFF80000`, mémorise. Sinon : 2 tirages, X = héros + `[0, 192 px) − 96`, Y = héros + `[0, 128 px) − 64`, Z + 128 px ; détruit sans effet si `IsOnGround`, sinon ForceZ `0xFFF80000` ; efface la dernière cible. |
| `0x32` vent 2 | `0x80036218` | Quand l'anim est `0x33`, sprite 24 au héros ; sauve pointeur et identité. Pas de passage dans la phase 1 dans le même tick. Phase 1 : quand l'entité meurt, anim `0x34`. |

**Observé dans le jeu émulé** (graine `0x12345678`, héros en (100,100) px regardant à droite, terrain plat, sans cible ; `H4/results/trace_summaries.txt`) :

| Sort | Naissances | Durées de vie | Fin d'objet en cours |
|---|---|---|---|
| terre 1 | 32 (ticks 22 à 97) | 30 à 34 | tick 129 |
| terre 2 | 15 (ticks 18 à 242) | 49 à 82 | 309 |
| eau 1 | 3 (ticks 42 à 82) | 297 à 342 | 344 |
| eau 2 | 6 (ticks 42 à 142) | 292 à 352 | 394 |
| feu 1 | 1 colonne (tick 34) + 12 boules (ticks 34 à 122) | colonne 89, boules 53 à 81 | 150 |
| feu 2 | 1 (tick 34) | 122 | 183 |
| vent 1 | 7 frappes (ticks 42 à 102) | 38 à 39 | 129 |
| vent 2 | 1 (tick 34) | 182 | 243 |

Les 8 lancers font passer MP de 4 à 3.

## 4. Les gestionnaires natifs

Ce sont les entités des sprites 16, 17, 19 à 24. Registre : (E,3) {16, 23}, (E,13) {17}, (E,28) {21}, (C,63) {23}, (C,65) {24}, (C,75) {19}. Pour les sprites 19, 23 et 24 : A=254 donne une fonction vide (`0x80061BCC`). Les sprites 16, 17, 20, 21 et 22 ont A=0 : le défaut `0x8006174C` écrit `+0x280 = lh(+0x11E)` et personne ne le lit ici. Le sprite 19 a E=0, qui pointe `0x8007ED10`. E1 `0x8007ED10`, déjà inline dans la DLL, fait `DestroyEntity(self, -1)`. Voir `H4/lst/` pour les listings. Tout est vérifié [V] (6000 vecteurs, 0 écart, couverture 100 % des instructions de chaque fonction).

| Gestionnaire | Adresse | Effet |
|---|---|---|
| E3 | `0x8007EDA0` | Si `TargetAnim==1` et `AnimEnd==1` : `DestroyEntity(-1)`. Sinon `TargetAnim=1`, flags `\|= 0x40`, `&= ~0x30`. |
| E13 | `0x8007F30C` | Si `TargetAnim==1` : détruit si `AnimEnd==1`, sinon ne fait rien (≠ E3). Sinon : `[0x800DD874]=2`, puis comme E3. |
| E28 | `0x8007FB58` | 8 fois : un tirage `<0x1801`, `r' = r + 0x320`, effet 19 (variante 2) en (X + `TAB_X[4k]`·r', Y + `TAB_Y[4k]`·r', Z). Puis `DestroyEntity(-1)`. |
| C63 | `0x8007AC60` | Délai `+0x274` (départ 16) : décrémente tant que ≠ 0. À 0 : ForceZ = `0xFFFD0000` (-3 px/tick). Sans cible : requête `(self, ClassA, attaque&0xF)`. Pas de résultat : tirage `<9` dans `+0x274` (nouvel essai plus tard). Résultat : cible = premier, mémorise pointeur (`+0x278`) et identité (`+0x27C`). Cible vivante (statut 2, identité identique) : direction vers elle, tourne de 4 par pas SANS passage par le plus court (comparaison numérique), puis `+0x274 = 2`. Cible morte : l'oublie. |
| C65 | `0x8007AF20` | Requête `(self, ClassA, attaque&0xF)`. Premier résultat dans |dx| ≤ `0x1400000` (320 px) et |dy| ≤ `0xF00000` (240 px) : direction `d` vers lui, indice `(d+0x14)&31`, puis sur la cible `+0xCC = (cos[idx]·3)<<8`, `+0xD0 = sin[idx]<<9`. Une seule cible par tick. |
| C75 | `0x8007BD8C` | Voir ci-dessous. |

**Orbe d'eau (C75).**
- Décrémente `+0x290` seulement si le Rond n'est pas tenu (`[0x80126F28] & 0x20 == 0`).
- Si `+0x290 > 0` : orbite.
  - Le rayon `r` (`+0x288`) tend vers `p(mode)`, avec `mode` en `+0x284` valant 0 pour `p0` et 1 pour `p1`.
  - En approche : `r += p2 + (r<p0 ? 64 : 0)`. En retour : `r -= p2`.
  - À l'arrivée : `r` est calé et `mode` est inversé.
  - Angle `a = (a + p3) & 255`.
  - Poussée `+0xCC = hX + 3·sin256[a]·r + hForceX − X` et `+0xD0 = hY + 2·cos256[a]·r + hForceY − Y`. Le facteur 2 de Y fait l'ellipse 3:2.
  - ForceZ = `hZ − Z + 0x80000`.
- À `+0x290 == 0` : lâcher.
  - Requête `(self, ClassA, 9)`. Si le premier résultat a une distance² < `0x3840` (120 px) : direction vers lui. Sinon un tirage `<32`.
  - `+0x274 = TAB_X[d]<<8`, `+0x278 = TAB_Y[d]<<8`, flags `\|= 0x2130` (impact, coup, gravité, glisse), ForceZ `0xA0000`.
- À `+0x290 < 0` (et après le lâcher) : `+0xCC/+0xD0` reçoivent `+0x274/+0x278` à chaque tick (vol rectiligne à ±3 / ±2 px/tick).

**Requête `0x8003AF70`.**
- Balaie les emplacements 1 à `[0x80134360]-1`.
- Exclut l'appelant.
- Garde les statuts 2 ou 3.
- Exclut `+0x20 != 0`.
- Exige `flags & masque`.
- Exclut `+0x1D0 != 0` ou `+0x1CC != 0`, donc un ennemi tout juste touché disparaît de la liste.
- Exclut `AnimFlags & 0x40`.
- Exige une fiche d'équilibrage.
- Exclut la réponse `(octet[2+type] & 0xC0) == 0x80`.
- Distance² = `dx²+dy²` de positions `>>16`.
- Tri quicksort de Hoare (pivot = moyenne arithmétique non signée des extrémités) seulement à partir de 3 résultats. 91 instructions sur 91 couvertes.

Mutants : 36 sur 36 détectés pour les gestionnaires, 53 sur 54 pour les pilotes (le seul survivant, « remise à 0 de `[0x80127164]` seulement si le résultat vaut 1 », est équivalent car les pilotes ne rendent que 0 ou 1). La liste, avec les mutants relancés avec des vecteurs aux bornes, est dans `H4/out_vec_drivers_mut.txt`, `H4/out_vec_handlers_mut.txt` et `H4/results/mut_*.txt`.

## 5. Ce que chaque sort fait à l'écran et aux ennemis

Les dégâts viennent de la fiche d'équilibrage du sprite (index = type) lue par la passe de coups déjà portée. Tous les sorts ont `HitsClassA` (0x04) seul : ils touchent tout ce qui est ClassA et qui a une fiche (monstres, mais aussi caisses et obstacles). Le héros (ClassB) n'est pas touché. Les boîtes viennent de `map_alundra.json` (durées en ticks : `delay & 0x7F`).

| Sprite | Attaque (type, puissance) | Boîte d'attaque | Remarques |
|---|---|---|---|
| 16 terre 1 | 8, 32 | 42×28×11 en vol ; 42×28×11 + 23×23×15 à l'impact | Anim 0 : 24 ticks (13 images), vitesse 512. Anim 1 (impact) : 12 ticks, son 46 (32 occurrences observées). Lancer en arc (pointe de 14 à 22 px), atterrissage à 36 à 120 px du héros. E3 à l'impact ; détruit à la fin de l'anim 1. Pas `DeactivateOnHit` : traverse les ennemis. |
| 17 terre 2 | 8, 64 | 72×48×39, z -16, dans les deux anims | Anim 0 : 32 ticks, son 55 à la naissance. Anim 1 : 46 ticks, son 45 à l'impact. E13 : coup de caméra Y +2 pour un tick. Rayon X 36 à 120 px, Y 24 à 80 px (ellipse × (p+2560)/65536). |
| 19 eau | 9, 64 | 20×14×23 | Flags 0x34, E=0, effet de bris 8 (11 images, 32 ticks). Rayon d'orbite x: `3·256·r/65536` px, y : `2·256·r/65536`. r=1024 donne 12×8 px, r=3072 donne 36×24, r=3584 donne 42×28. Eau 1 : p0=1024, p1=3072, p2=16, p3=7, décalage angulaire 0/85/170, durée 340 − compteur de naissance. Eau 2, lot 1 : 3584, 3584, 0, 8, 0/85/170, durée 350. Eau 2, lot 2 (compteurs 100, 120, 140) : 2048, 3072, 32, -6, 128/213/42, durée 390 : rotation inverse. Soin : eau 1 `Hp += max(5, HpMax/2)` plafonné + 4 étincelles (effet 14, 2 tirages chacune), son `0x31` ; eau 2 `Hp = HpMax` + 8 anneaux (effet 14 sans tirage), son `0x30`. |
| 20 feu 1 | 10, 64 | 23×16×47, z -18, 87 ticks | Colonne au héros : 87 ticks d'anim, détruite à 89 ticks de vie. 12 boules (sprite 23) : voir ci-dessous. |
| 23 boule de feu | 10, 32 | 23×15×15 | Flags 0x74 : impact, coup, fin d'anim. Anim 0 : 8 ticks puis boucle ; vitesse 256. Pendant les 16 premiers ticks le gestionnaire n'agit pas : la boule monte à +8 px/tick (jusqu'à environ 144 px au-dessus du héros), puis redescend à -3 px/tick avec guidage. Vie de 53 à 81 ticks. |
| 21 feu 2 | 10, 128 | 30×18 qui grossit de 3 ticks en 3 ticks jusqu'à 178×158×127 (tick 54), puis constant jusqu'à 120 | Tremblement de caméra croissant ; à la fin 8 explosions (effet 19 variante 2) à 9 à 81 px (X) et 6 à 54 px (Y). |
| 22 vent 1 | 11, 32 | 36×24×31 de t=8 à t=37 | Tombe de 128 px à -8 px/tick (gravité en plus). 37 ticks d'anim. |
| 24 vent 2 | 11, 128 | 60×40×254 pendant 142 ticks, puis 23×23×15 pendant 2 ticks | 180 ticks d'anim (182 de vie). |

**Règle de dégâts.** Pour une attaque (attribut `attr`, puissance `P`) et une fiche cible : `r = octet[2 + (attr & 0xF)]`. Si `r & 0xC0 == 0x40`, PV à 0. Si `r & 0xC0` est non nul, rien. Sinon dégâts = `max(1, (P·r)>>4)`. Pas de bonus d'équipement (bit 0x80 absent). La comparaison avec `ComputeNewHp` réel donne 0 écart sur 20 480 cas, exhaustifs : 512 fiches × 8 attaques de sorts × 5 PV (`H4/results/damage.txt`).

Sur les 145 fiches de monstres (octet 2 > 0), la réponse à l'élément (octets 10 à 13) se répartit ainsi :

| Élément | r=16 | Immunisés (0x80/0xC0) | r=6 | Autres |
|---|---|---|---|---|
| terre | 93 | 30 | 12 | r=1 : 5 ; r=8 : 3 ; r=0 : 2 |
| eau | 92 | 42 | 5 | r=8 : 3 ; r=0 : 2 ; r=1 : 1 |
| feu | 89 | 30 | 12 | r=1 : 4 ; r=0 : 3 ; mort instantanée : 3 ; r=8 : 3 ; r=2 : 1 |
| vent | 92 | 42 | 5 | r=8 : 3 ; r=0 : 2 ; r=1 : 1 |

**Échantillon en jeu émulé** (`H4/results/targets_inv0.txt` et `targets_inv30.txt`). Six cibles statiques (instances du sprite 25, ClassA, 5000 PV, réponses 16) aux positions (140,100), (100,140), (180,100), (100,60), (60,100), (140,140), sans invulnérabilité. Même graine ; la passe de coups tire dans le générateur à chaque touche, donc le nombre de touches décale les tirages suivants :
- terre 1 : 4, 2, 3, 3, 3, 3 touches de 32.
- terre 2 : 7, 9, 0, 6, 9, 6 de 64.
- eau 1 : 3, 0, 0, 0, 0, 0 de 64 (les 3 orbes visent la même cible la plus proche).
- eau 2 : 4, 1, 0, 0, 1, 0.
- feu 1 : 3, 1, 0, 2, 3, 0 de 32 (boules ; la colonne ne touche personne à 40 px).
- feu 2 : 5, 4, 4, 4, 5, 4 de 128.
- vent 1 : 2, 2, 2, 2, 4, 2 de 32.
- vent 2 : 6, 5, 0, 6, 6, 0 de 128 ; trois cibles tirées vers le héros : (100,140) -> (83,116), (180,100) -> (164,109), (100,60) -> (113,73).

Avec une invulnérabilité de 30 ticks après chaque touche (`+0x1CC` posé par le gestionnaire D de l'ennemi, valeur choisie par moi), les touches diminuent et les orbes d'eau se répartissent sur les cibles encore éligibles.

**Sons observés** (hors piste XA) : 46 (terre 1), 55 et 45 (terre 2), `0x31`/`0x30` (soin d'eau). Les effets 8, 14, 18, 19 (variante 2) et 26 existent dans `effects-global.json` (29 effets). L'interpréteur d'effets a une commande son générique (`0x8003D6C0`) ; si les effets utilisés en portent une est INCONNU (l'export n'en garde pas).

**Tirages du générateur** :

| Sort | Tirages |
|---|---|
| terre 1 | aucun |
| terre 2 | 2 par rocher |
| eau | aucun en orbite ; 1 par orbe au lâcher sans cible proche ; soin 1 : 8 |
| feu 1 | 1 par boule (direction) et 1 par boule à chaque tentative sans cible (`<9`) |
| feu 2 | 8 |
| vent 1 | 2 par frappe aléatoire |
| vent 2 | 0 ; à l'annulation 48 |
| passe de coups | plusieurs par touche (étincelles) |

## 6. Interruptions (vérifié [M])

L'animation du héros posée à `0x31` en cours de sort annule au tick même, et l'objet est libéré (`H4/results/interrupt_check.txt`) :
- terre 2 : les 5 rochers vivants passent à statut 3, anim 1 (ils finissent leur impact puis meurent).
- eau 1 : 3 effets 26, orbes détruites.
- feu 1 : colonne détruite avec un effet 19(2) ; les boules passent à statut 3.
- feu 2 : le statut 3 déclenche aussitôt E28 : les 8 explosions ont lieu quand même.
- vent 2 : effets 18, tornade morte.

Un dialogue (branche verrouillée) appelle la même annulation. Le menu Start est refusé tant que l'objet est en cours (§0 point 6).

## 7. Divergences avec la décompilation

La décompilation est `alundra-datas-analyser/AlundraTools/AlundraEngine/` (`Gameplay/PlayerManager.cs`, `Gameplay/Scripts/FunctionTypeC.cs`, `FunctionTypeE.cs`). Le binaire gagne.

- **D1.** `TryLaunchMagic` (`PlayerManager.cs:2317-2345`) : à 0 PM la décompilation joue le son 3 puis continue (`//goto PlayFailSound` en commentaire), décrémente (sans effet) et lance quand même le sort. Binaire : échec et retour 1.
- **D2.** Le démarrage XA est en commentaire dans la décompilation.
- **D3.** `ProcessEarthMagicLevel2Sequence` : décompilation, X indexé par le second tirage, Y par le premier. Binaire : X et Y par le premier, direction par le second. Le binaire place les rochers sur une ellipse ; la décompilation les disperse dans un rectangle.
- **D4.** Eau : décompilation `Z + 0x00800000` (128 px), binaire `+0x80000` (8 px, `lui v1, 8` en `0x80035864`).
- **D5.** Eau : la décompilation range le produit `p3 × compteur` en `+0x280` ; le binaire y range `p3` seul (le produit ne sert qu'à `+0x28C`). Avec la décompilation, l'orbe tournerait à une vitesse proportionnelle au compteur.
- **D6.** C75 : la décompilation applique le facteur 3 à X et à Y (cercle) ; le binaire applique 3 à X et 2 à Y (ellipse).
- **D7.** Feu 1 et feu 2 : le binaire enchaîne la phase 1 dans le tick de la phase 0 (première boule et premier tremblement au tick de naissance). La décompilation rend 0 et attend le tick suivant.
- **D8.** Vent 1 : au-delà de la distance limite, le binaire arrête la recherche (`beqz` vers la sortie de boucle) ; la décompilation continue avec le candidat suivant. Seul un cas non trié (moins de 3 résultats) diffère.
- **D9.** Vent 1 : la décompilation détruit la frappe et efface la dernière cible si `IsOnGround` dans la branche « cible » aussi ; le binaire ne teste le sol que dans la branche aléatoire.
- **D10.** Tri de la requête : `Array.Sort` (introsort) contre le quicksort de Hoare du binaire ; l'ordre diffère sur les égalités.
- **D11.** `IsFadeActive` est le nom faux de `0x800351B8` (pas de fondu).
- **D12.** Wind lvl 2, rappel `h-g-deps.md:109` (hypothèse de warp) : fausse.
- Hors tomes mais constaté : `ApplyEntityForces` (`PhysicsEngine.cs:1514`) additionne `XForceTable[idx] >> shift`, alors que le binaire fait `(ForceX + table) >> shift` puis ajoute la poussée (`0x80036758-0x80036764`). Sans effet quand `SlideEffectId` vaut 0.

Les gestionnaires E3, E13 (le test `TargetAnim==1` seul de E13 est bien traduit), E28, C63 et C65 de la décompilation sont corrects.

## 8. Ce que la DLL a et ce qu'elle doit ajouter

**Déjà là (avec les lignes actuelles).**
- `AlundraPlayerManager.cs:919` `TryUseItem` (bouchon, rend 1), `:340` appel de `UpdateItemUseDriver`, `:1494` sa définition (vide).
- `:2074` `GetItemIdFromCurrentWeapon`, `:2168` `GetNumberOfItem`, `:2204` `SetItemIdFromCurrentItemId` (rend l'ancien id et promeut comme le binaire), `:1957` `SetPlayerMp`, `:1938` `SetPlayerMpMax` (plafond 4), `:2250` `UseItem`.
- États `0x32/0x33/0x34` : aucun cas dans `MovePlayer`. Le défaut est une absence d'action silencieuse.
- `AlundraWorldProxy.cs:3448` `SpawnEntityBySpriteIndex(parent, false, idx, x, y, z, direction)` : direction, Z + 1, boucle de naissance, fiche d'équilibrage, butin.
- Préfabs `alundra_16..24` exportés avec les en-têtes du binaire (`Data/sprite-records.json`).
- `AlundraWorldProxy.cs:3267` `DestroyEntity(e, fx)` et `:3295` la variante sans effet.
- `AlundraEventProgramRunner.cs:378` E0/E1 inline.
- `AlundraNativeSpriteRegistry.cs:29` registre de production (Beannoïde, Muruta, C61 seulement).
- Statut 3 (« seulement E ») : `AlundraEntityScriptProxy.cs:1709-1745`.
- `AlundraDamage.cs:27` règle de dégâts ; passe de coups portée.
- `AlundraCameraSway.cs` (9 champs, `Step`).
- `AlundraNativeEffects.cs:28` motif d'effets (anneaux, étincelles).
- Test saves `AlundraTestSaves.cs:67-72, 96`.

**À ajouter.**
- L'état d'usage (objet, compteur, phase), le front du Rond, le garde de verrou `!= 0`, le pilote et l'annulation avec leurs 4 sites + le site de blessure (E20.c2b), et le garde du menu (`AlundraInventoryDirector.cs:406`).
- `DecreaseMp` (aucun équivalent aujourd'hui).
- Les états héros `0x32/0x33/0x34` (maintien, sortie vers Idle/`0x2D` hors tome) et le départ XA en bouchon journalisé.
- Les 8 pilotes et leurs tables ; les 6 gestionnaires natifs et leur enregistrement.
- La requête avec son tri exact.
- La poussée `+0xCC/+0xD0` consommée par le moteur de déplacement (ajout à la force XY du tick, puis remise à zéro, bornée à la carte, avec `ForceAdjusted`).
- Un accès 32 bits à `+0x274` : `Bytes` est un `byte[4]` (`:1214`), alors que p0 vaut jusqu'à 3584 et que C75 y range une vitesse de ±196 608.
- `ResyncControllerFromFlags` (`:657`) quand C75 modifie les flags.
- Un coup de caméra Y d'un tick (`[0x800DD874]`, consommé en `0x8002CE6C`) dans le calcul de la cible de caméra (`AlundraCameraMath.cs`).
- Les effets 8, 14, 18, 19, 26 via le réservoir (exists déjà côté banque).
- Les sauvegardes et le HUD n'ont rien à ajouter.

## 9. Preuves et rejeu

- Outils copiés (`exelib`, `mipsdis`) et interpréteur MIPS de D1/D2/E14.b1, tous en lecture seule.
- `H4/oracle/spellworld.py` : page globale de sprites installée en RAM (`0x80126EC0`, 25 records de `map_alundra.json`) pour exécuter `SpawnWarpEntity` réel. Note : l'ordre des pages est l'inverse de ce qu'on croirait, le drapeau 0 choisit `0x80126EC0` (page globale) et un drapeau non nul `0x80126E70` (page de carte, type + 0x100).
- `h4_start.py N` : cadre de départ (5000 vecteurs, 0 écart, 142/142 instructions).
- `h4_vec_drivers.py N` : pilote et annulations (6000 vecteurs, 0 écart ; couverture : pilote 73/93, garde de pente 19/19, terre 1 106/108, terre 2 174/176, eau 187/189, feu 1 118/120, feu 2 145/147, vent 1 216/218, vent 2 75/77, annulations 30/30, 54/54, 55/55, 47/47, 127/127, soins 94/94 et 78/78, requête 94/94, tri 91/91 ; les manquants sont les graines, la cape, la bombe, les phases négatives et le cas par défaut).
- `h4_vec_handlers.py N` : gestionnaires (6000 vecteurs, 0 écart, 100 % de couverture).
- `h4_damage.py` : règle de dégâts (20 480 cas, 0 écart).
- `trace_cast.py ITEM`, `trace_targets.py ITEM X Y …`, `run_targets_all.py`, `staff_check.py`, `interrupt_check.py` : jeu émulé complet.
- Mutants : `h4_start_mut.py` (11/13, 2 équivalents), `h4_vec_drivers_mut.py` (53/54, 1 équivalent), `h4_vec_handlers_mut.py` (36/36).
- Lancer : `python -B <script>` depuis `H4/oracle/`.

## 10. Questions ouvertes

- **Q1.** Pistes XA : jouer (nécessite le décodage de `Alundra (France).bin`, O-E19-36) ou bouchon journalisé comme `0xB9`. Contenu des pistes 0 à 6 : INCONNU.
- **Q2.** Défauts avérés de l'original à reproduire ou corriger (règle de l'auteur : un bug avéré se corrige) :
  - le dernier emplacement utilisé n'est jamais une cible : un ennemi seul dans ce cas n'est jamais visé par C63, C65, C75, ni par le vent 1 ;
  - la première frappe du vent 1 (compteur 30) est perdue : 7 frappes au lieu de 8 ;
  - le guidage de C63 tourne dans le sens numérique, pas par le plus court ;
  - le tri instable de la requête (impact faible).
- **Q3.** Propriétaire de la poussée `+0xCC/+0xD0` : E20.e0k, E14.a, ou une tranche nouvelle avant eau et vent 2.
- **Q4.** Confirmer en jeu que le menu Start est refusé tant qu'un objet est en cours (FAIT d'après le binaire, ~344 ticks pour l'eau).
- **Q5.** Sons des effets 8, 14, 18, 19(2), 26 : INCONNU (commande son possible dans les programmes d'effets, non décodée).
- **Q6.** Unité du coup de caméra E13 : INFÉRENCE, +2 sur le défilement Y (pixels) pour un tick.
- **Q7.** Où le jeu donne les tomes (corpus) : niveau 1 en coffre aux cartes 25 (I43), 76 (I45), 84 (I47), 95 (I49) ; niveau 2 en coffre aux cartes 17 et 439 (I44), 3 (I46), 448 (I48), 431 (I50) ; scripts `0x82` aux cartes 17, 31, 77, 80, 158, 439, 443. Le bâton : script de la carte 134. Lesquelles sont sur la chaîne actuelle : non vérifié.
- **Q8.** E3/E13 : registre commun avec E14 (E3 sert 98 sprites de cartes, E13 un). Le registre refuse deux enregistrements de la même clé.

## 11. Découpage proposé de E20.h0 et E20.h4

| Tranche | Contenu | Prérequis | Preuve |
|---|---|---|---|
| **h0a** cadre | État d'usage, front du Rond, verrou `!= 0`, table de départ avec échecs (son 3) et bouchons nommés pour les objets des tranches h1 à h3, pilote avec compteur et libération, annulation avec ses sites (verrou, état par défaut, branche `+0x188`), garde de pente, garde du menu. Site de blessure et de mort : E20.c2b et E18. | E20.c2a ✅ | Vecteurs de `h4_start.py` et du pilote ; arc : Rond sans objet joue le son 3. |
| **h4a** départ et pose | `DecreaseMp`, contrôles du tome (porté, anim < 2, rien en cours, bâton, PM), états `0x32/0x33/0x34`, XA bouchon, pilote à tables vides (anims seules). | h0a | Arc : PM-1, pose 32+…+26 ticks, objet libéré ; bâton ; échecs. |
| **h4b** briques communes | Requête avec tri exact, poussée (si Q3 la place ici), coup de caméra, accès 32 bits, helper de registre pour E3, sauvegarde/restauration du bloc de tremblement. | E20.e0k, E20.e0a5, Q3 | Vecteurs `h4_vec_*` rejoués contre le code C#. |
| **h4c1** terre 1 | Pilote, E3, direction relative à `+0x8C`. | h4a, f6a (types 16) | Vecteurs ; trace de `trace_cast.py 0x2B` ; arc avec cibles. |
| **h4c2** terre 2 | Pilote, E13, détruit-au-spawn, coup de caméra. | h4a, h4b, f6a (17) | Idem `0x2C`. |
| **h4d1** feu 2 | Pilote, E28, tremblement. | h4a, h4b | Idem `0x30`. |
| **h4d2** feu 1 | Pilote, C63, E3 (type 23). | h4a, h4b, f6a (23) | Idem `0x2F`. |
| **h4e1** vent 1 | Pilote, requête, tirages. | h4a, h4b, Q2 | Idem `0x31`. |
| **h4e2** vent 2 | Pilote, C65, poussée. | h4a, h4b | Idem `0x32`. |
| **h4f1** eau 1 | Pilote, C75, `0x80032F84`, poussée, flags modifiés. | h4a, h4b | Idem `0x2D`. |
| **h4f2** eau 2 | Table de 6 orbes, `0x80033274` (partagé avec la Potion L de h1). | h4f1, h1 | Idem `0x2E`. |

h4 n'exige pas E20.g. À corriger dans l'enveloppe : la ligne 11 (tomes sans E20.g) et O-E20-20 (corps de C63, C65, C75, E3, E13, E28 maintenant lus).

Chaque tranche peut reprendre les scénarios de `trace_cast.py` et `trace_targets.py` comme références (mêmes graine, position du héros, cibles), car l'émulateur et la DLL partagent le même générateur et la passe de coups est la même.