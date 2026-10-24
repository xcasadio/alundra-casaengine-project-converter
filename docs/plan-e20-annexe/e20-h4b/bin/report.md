# Rapport disc-h4b/bin : lecture au binaire des briques communes de h4b

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/bin`. Il contient `h/` (scripts), `out/` (listings, tables, synthèse) et `vec/` (vecteurs et `README.txt`).

Ce message est le rapport. La règle de session interdit les fichiers de rapport, donc `report.md` n'a pas été écrit. Rien n'a été écrit hors du dossier de travail.

**Légende.**
- FAIT : lu dans le binaire (adresse), dans la DLL (fichier:ligne) ou dans le corpus.
- [V] : comparé au vrai code MIPS sur vecteurs aléatoires.
- INFÉRENCE : déduit, avec la source dite.
- INCONNU : non établi.

**Binaire lu.** `ALUN_CD.EXE` France, SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (recalculé).

**Méthode.**
- Désassemblage capstone et interpréteur MIPS du harnais `e20-h4` (copié dans `h/`).
- Une machine "TM" trace chaque lecture et écriture de donnée du vrai code.
- Le rejeu part d'une mémoire vide (`vtrace.SparseRun`) : l'instruction vient de l'image, et toute lecture de donnée absente du vecteur est une erreur. Une lecture manquante dans le vecteur est donc détectée.

## 0. Résultats d'ensemble

Tous les modèles sont écrits par moi depuis le désassemblage (`out/dis_*.txt`), graine 20261018 (`out/run_summary.txt`).

| Brique | Fonction | Exécutions | Écarts | Couverture du vrai code | Mutants distingués |
|---|---|---|---|---|---|
| Poussée | `0x800366FC` | 3000 | 0 | 58/58 | 12/12 |
| Passe de forces | `0x80036828` | 600 | 0 | 153/244 | poussée retirée : distinguée |
| Passe des emplacements | `0x80038634` | 600 | 0 | 39/39 | 3/3 |
| Passe des listes | `0x800384F4` | 600 | 0 | 80/80 | 2/2 |
| Relèvement à la naissance | `0x80039D04` (jusqu'à `0x80039D40`) | 600 | 0 | n/a | n/a |
| Requête et tri | `0x8003AF70` et `0x8003AE04` | 1500 | 0 | 94/94 et 91/91 | 12/13 |
| Pas du tremblement | `0x8002C894` | 1500 | 0 | 83/83 | 1/1 |
| Défilement de caméra | `0x8002CDA0` (jusqu'à `0x8002CF38`) | 1500 | 0 | n/a | coup retiré : distingué |
| E13 | `0x8007F30C` | 1500 | 0 | n/a | n/a |
| Pilote du feu 2 | `0x80035C64` (avec garde et annulation) | 1500 | 0 | pilote 145/147, garde 19/19, annulation `0x80034E08` 47/47 | 1/1 |

- **Mutant de la requête non distingué (`signed_cmp`).** Il est équivalent dans le jeu : il faudrait des distances² ≥ 2³¹, c'est-à-dire |dx| = |dy| = 32768 px.
- **Couverture de la passe de forces.** Les 91 instructions non couvertes sont la branche Z du héros, le recalcul de `0x80036614` et la mise à l'échelle glace/eau du héros.
- **Vecteurs rejouables.** 770 vecteurs dans `vec/vec_*.json`, tous rejoués depuis une mémoire vide, 0 échec (`h/replay_all.py`). Le format est décrit dans `vec/README.txt`. Les tables sont dans `vec/tables.json`.

## 1. Faits qui changent le plan

1. **La poussée `+0xCC/+0xD0` n'est pas propre aux tomes.**
   - FAIT : elle est écrite par C49, C51, C65, C75, C89, C96, et remise à zéro par D43.
   - FAIT : C51 (garde d'écriture `+0x144`/`+0xB4`, `0x80077888-0x800778A4`) et C89 (`0x80062994`) poussent le héros (`0x80127D30`). C51 pousse aussi son fils `+0x284`.
   - FAIT : le héros consomme la poussée comme les autres entités (§2.2).
   - Corpus : C49 sur 2 cartes, C51 sur 1, C89 sur 1 enregistrement, D43 sur 1. C96 n'a aucun enregistrement.
2. **Le serrage à la boîte de la carte lève `ForceAdjusted`, et ce drapeau tue des projectiles.**
   - FAIT : `0x800366FC` pose `+0x13C = 1` quand il serre le pas (`0x8003679C`, `0x800367D4`).
   - FAIT : la passe d'événements `0x800386D0` passe en statut 3 toute entité dont `+0x6C & 0x10` est posé et dont `ForceAdjusted` ou `IsOnGround` est non nul (`0x800387D4-0x80038834`). Le programme E tourne ensuite.
   - Le sprite 19 (orbe d'eau) a `MoreFlags 0x34`, qui contient 0x10, et E = 0 (destruction).
   - INFÉRENCE : une orbe qui touche un mur ou le bord de la boîte se brise.
3. **Le plus haut emplacement.**
   - FAIT : sur les 61 lectures de `[0x80134360]` en code, les 23 fins de boucle sont `slt count, idx`, donc inclusives (idx ≤ count).
   - FAIT : la requête `0x8003AF70` est l'unique borne exclusive (`slt t3, count`, `0x8003AF98`).
   - FAIT : les bornes de la passe des listes, des annulations des tomes et de la phase 2 de l'eau sont inclusives.
4. **Le tri n'est pas appliqué pour 2 résultats.**
   - FAIT : `0x8003B0B4` (`slti s0, 3`). Avec 2 candidats l'ordre est celui des emplacements.
   - Mesuré sur des tirages synthétiques : le premier résultat n'est pas le plus proche dans environ 48 % des cas à n = 2, et dans 0 % des cas à n ≥ 3.
5. **Le gabarit d'entité vide n'est pas entièrement nul.**
   - FAIT : `0x8003B21C` écrit −1 à `0x801343B0`, soit gabarit + 0x48. `spellworld.py` du harnais le met à zéro (simplification).
   - INFÉRENCE : le reste du gabarit est du BSS à zéro. Aucune autre écriture directe n'a été trouvée (`refrange`).
6. **Tables 256.**
   - FAIT : seuls C65 (`0x8007AF74`, `0x8007AF7C`) et C75 (`0x8007BE54`, `0x8007BE78`) les lisent.
   - FAIT [V] : `sin256[i] = trunc(256·sin(2πi/256))` pour les 256 entrées, et `cos256[i] = sin256[(i+64)&255]`.
7. **Coup de caméra.**
   - FAIT : le coup X `[0x800DD870]` n'est jamais écrit (seulement remis à zéro).
   - FAIT : le coup Y `[0x800DD874]` n'est écrit que par E13, par affectation de 2. Deux rochers au même tick donnent 2, pas 4.
   - FAIT : l'unité est le pixel de défilement. Vie du coup : +2, puis −1, puis −1, puis 0.
8. **Bloc de tremblement.** Il est partagé avec 10 gestionnaires natifs et deux opcodes. Le tampon `0x80127138` n'est touché que par le pilote du feu 2 et par son annulation.
9. **Accès 32 bits.**
   - FAIT : `+0x274` est accédé en mot par au moins 11 familles de gestionnaires et 5 aides.
   - FAIT : les mots `+0x280..+0x290` sont aussi accédés par C90, C97, C2, C8, C16, C21, C46, C51, C54, C55, C56, C86 et d'autres.
   - Ce n'est donc pas une particularité des tomes (§8).
10. **Bit 0x2000 de `+0x6C`.**
    - FAIT : `0x800379BC-0x800379C8` teste le bit, puis saute à `0x80037D54`, qui pose `ForceAdjusted = 1` et s'arrête sans retenter.
    - Cela tranche l'INCONNU de `e20-h3 check.md` §5 : le bit est bien "pas de glissement le long de l'obstacle", comme le dit la DLL (`EntityFlags.cs`).
11. **Sur les huit tomes, cinq n'ont besoin d'aucune poussée** (§9).

## 2. Brique 1 : la poussée

### 2.1 Sémantique exacte de `0x800366FC`

Fonction feuille, `a0` = entité. FAIT [V] 3000 vecteurs.

- **Indice et tables.**
  - L'indice est `idx = (+0x188) & 0xF` (`0x80036710-0x8003671C`).
  - Les tables sont `0x80028B94` pour X (`a2`) et `0x80028BD4` pour Y (`a1`), des mots signés.
  - X non nul aux indices 4-6 (−61440), 7 (−983040), 8-10 (+61440), 11 (+983040).
  - Y non nul aux indices 1, 5, 9, 13 (−40960) et 2, 6, 10, 14 (+40960).
- **Décalage.** Il vient de `lb a0, 8([0x800E4334])` (`0x8003673C-0x80036744`), le champ `SlideEffectId` de la carte. `srav` n'utilise que les 5 bits bas : `sh = octet & 31`. Corpus : 481 cartes valent 0 et 2 cartes valent 1.
- **Calcul.**
  - `adjX = ((ForceX +0xC4 + tabX[idx]) >> sh) + push X`, avec `+0xCC` lu en `0x80036714`.
  - `adjY = ((ForceY +0xC8 + tabY[idx]) >> sh) + push Y`, avec `+0xD0` lu en `0x80036718`.
  - Le décalage est arithmétique et précède l'ajout de la poussée (`0x80036758-0x80036764`, `0x8003677C`). Le pas de la poussée n'est donc pas décalé. La décompilation fait `ForceX + (table >> sh) + push`, ce qui diffère dès que `sh ≠ 0`.
  - Les deux poussées sont remises à zéro en `0x80036750` (D0) et `0x80036754` (CC), après lecture et avant le serrage.
- **Serrage par axe, comparaisons signées strictes.**
  - X : minimum `+0x108`, maximum `+0xFC`. Y : minimum `+0x10C`, maximum `+0x100`.
  - Si `Pos + adj < min`, alors `adj = min − Pos`. Sinon si `max < Pos + adj`, alors `adj = max − Pos`. Dans les deux cas `+0x13C = 1`.
  - Arriver exactement sur une borne n'est ni serré ni signalé.
  - Le drapeau n'est jamais remis à zéro dans cette fonction.
  - Sorties : `+0xDC` = adjX, `+0xE0` = adjY.
- **Bornes par entité.**
  - FAIT : elles sont posées à la naissance par `0x80039C40`.
  - Minimum = −(OffsetX << 16), maximum = `0x4E00000 − ((OffsetX + SizeX) << 16)` pour X.
  - Même forme pour Y avec `0x3C00000` en maximum.
  - La boîte est fixe : 1248 × 960 px, soit 52 × 60 cases de 24 × 16, moins la boîte de l'entité. Ce n'est pas la taille réelle de la carte.
  - Cohérent avec la borne de défilement `0x39F`/`0x2CF`.

### 2.2 Quelles entités

- **Ordre du tick `0x8003B388`.** FAIT.
  - Garde : si `[0x800DC4B8] & 0x48`, seule la passe des listes tourne.
  - Sinon, dans l'ordre : emplacements `0x80038634`, événements `0x800386D0` (MovePlayer d'abord), compteurs `0x80038998`, listes `0x800384F4`, `0x80038E18`, physique `0x80038364`, effets `0x80038E84`, coups `0x80039300`.
  - Les gestionnaires (producteurs de poussée) tournent donc avant la passe de forces, dans le même tick.
- **Liste physique (`0x80127B28`, compte `0x80127D28`).** FAIT [V] 600 vecteurs, 80/80. Boucle sur les emplacements 0 à `[0x80134360]` inclus (`0x80038548-0x80038584`). Entrent les entités de statut 2 ou 3 avec `+0x20 == 0`. Les newborns (statut 1), les statuts 0 et 4, et celles avec `+0x20 ≠ 0` n'entrent pas.
- **Début de physique (`0x80038394-0x800383DC`).** Pour chaque entité de la liste, remise à zéro de `+0x13C` (`0x800383B8`), `+0x140` et `+0x168`. `ForceAdjusted` posé au tick N est donc vu par la passe d'événements du tick N+1, puis remis à zéro.
- **Passe de forces `0x80036828`.** FAIT [V].
  - Le héros (pointeur égal à `0x80127D30`) prend la branche `0x80036884-0x80036A80`. Il n'a pas de test `+0x28` : il consomme toujours la poussée. Mon premier modèle s'était trompé là-dessus, d'où la correction.
  - Toute autre entité de la liste : si `+0x28 ≠ 0` (porteur), `+0xB8`, `+0xC4`, `+0xC8`, `+0xDC`, `+0xE0`, `+0xE4`, `+0xE8`, `+0xEC` sont mis à zéro. **La poussée n'est ni consommée ni remise à zéro** (`0x80036A88-0x80036AB4`).
  - Sinon : Z, puis `0x80036614`, puis `IncrementForce` `0x800367E4` sur `+0xC4/+0xC8` vers `+0xBC/+0xC0` par pas `+0xD4/+0xD8`, puis `0x800366FC` (seul site, `0x80036B9C`), puis copie `+0xDC/+0xE0/+0xB8` vers `+0xE4/+0xE8/+0xEC` (`0x80036BA4-0x80036BB8`).
  - Les forces finales `+0xE4/+0xE8` sont consommées par le déplacement avec collision (`0x80037730`, qui pose aussi `ForceAdjusted`). Le porteur `0x80037364` ajoute `+0xDC/+0xE0` du porteur à son passager.
- **Gravité.** FAIT [V]. L'indicateur `+0x6C & 0x100` et `+0xF8` ne modifient que `+0xB8/+0xEC` (pour un non-héros : cas `+0xF8 = 0`, `0x8000` sans gravité, sinon `+0xF8 << 8`). Le résultat XY est indépendant. Le héros ajoute les mises à l'échelle glace et eau sur cible et pas, hors poussée.

### 2.3 Qui écrit `+0xCC/+0xD0`

Balayage de toutes les instructions à déplacement 0xCC/0xD0 (`out/scan_off_cc_d0.txt`). Seuls ces écrivains existent (les trois adresses de pile et l'octet de `0x8005709C` ne sont pas des entités).

| Gestionnaire | Sites | Cible et valeur |
|---|---|---|
| C49 `0x80075A3C` | `0x80075C64`, `0x80075CE0`, `0x80076824/28` | soi ; valeurs gardées par des bornes, ou lues en `0x801911E0/E4` |
| C51 `0x80077734` | `0x800778D4/FC`, `0x80078790-C4` | le héros (`a0` = `0x80127D30`) avec table 32 directions × 192 ; le fils `+0x284` et soi × 48 |
| C65 `0x8007AF20` | `0x8007B004/18` | le candidat touché ; `(cos256[8·idx]·3) << 8` et `sin256[8·idx] << 9`, `idx = (dir+0x14)&31` |
| C75 `0x8007BD8C` | `0x8007BEB8/D4`, `0x8007C000/04` | soi |
| C89 `0x80061EB8` | `0x80062118`, `0x80062994/9A0`, `0x80062B80/94` | soi (table × octet `+0x275` / 11) ; le héros depuis `[s1], [s1+4]` ; soi si non nul |
| C96 `0x800647B0` | `0x8006486C` | soi, `+0x278` |
| D43 `0x8007EBA8` | `0x8007EBBC/C4` | remise à zéro de soi |

- C65 : D13 confirmé indépendamment. Le binaire donne 196608 en X pour la direction 0, la décompilation 393216.
- Écriture directe uniquement : INCONNU pour les écritures par déplacement calculé (opcodes génériques de la VM).

### 2.4 Quand la poussée est remise à zéro

- À chaque passage dans `0x800366FC` : entités de la liste, hors porteurs `+0x28`.
- Par D43.
- À la recopie du gabarit sur un emplacement de statut 4, au passage `0x80038634`.
- Les affectations remplacent la valeur. Il n'y a pas d'accumulation, mais une valeur reste en place tant que l'entité est hors liste.

### 2.5 État de la DLL

- `PreviousAdjustedForceX/Y` : `Entities/EntityScriptProxy.cs:341-342`, sans consommateur.
- `RunOneKinematicTick` (`Physics/ScriptedMotion.cs:614-691`) pose `AdjustedForceX = ForceX` : pas de table de glissement ni de poussée.
- `ClampStepToMapEdge` (`:741`) est déjà le serrage du binaire, strict, pour les seules entités `HasLogicalZ`.
- Le héros passe par la même fonction (`IsPlayer`).
- INFÉRENCE : le point d'insertion de la poussée est avant le serrage.

## 3. Brique 2 : le plus haut emplacement `[0x80134360]`

- **Disposition.** 64 emplacements à partir de `0x80127D30`, pas de `0x294`. L'emplacement 0 est le héros.
- **Passe `0x80038634`.** FAIT [V], appelée à `0x8003B3A0` seulement si `[0x800DC4B8] & 0x48 == 0`. Pour i de 0 à 63 :
  - statut 4 : recopie de `0x294` octets du gabarit `0x80134368`, puis `+0 = i` ;
  - statut ni 0 ni 4 (donc 1, 2, 3 ou toute autre valeur) : `t2 = i` ;
  - fin : `[0x80134360] = t2` (`0x800386CC`).
  - Un emplacement recyclé ne compte pas.
- **Relèvement à la naissance.** FAIT [V], `0x80039D24-0x80039D3C` : `si count < [ent+0], count = [ent+0]`, comparaison stricte. L'emplacement vient de `0x80039AD0` : premier statut 0 de 1 à 63, sinon message et pointeur nul.
- **Remise à zéro au chargement de carte.** FAIT : `0x8003B2D8` (délai de `jal 0x80031974`). L'initialisation recopie le gabarit sur les 64 emplacements, pose `+0 = i`, puis fait naître les placements dans l'ordre.
- **Autres écritures.** Aucune ; il y en a trois en tout (recalcul, relèvement, remise à zéro).
- **Lecteurs.** 61 lectures (`out/highest_slot_readers.txt`, `out/highest_slot_tracking.txt`), 24 fonctions.
  - Gardes `blez` (boucle de 1 à count) : passe d'événements (2 boucles), les 5 annulations des tomes (`0x80034BDC`, `C54`, `D2C`, `E08`, `EC4`), la phase 2 du pilote d'eau `0x80035790`, la recherche de départ d'objet `0x80034320`, et une partie de la recherche par type `0x8003C954`.
  - Gardes `bltz` (boucle de 0 à count) : passe des listes, compteurs `0x80038998`, effets `0x80038E84`, recherche d'interaction du héros `0x8002EEAC`, les aides `0x8003A648`, `0x8003AD30`, `0x8003ADAC`, `0x8003B0E8`, `0x8003B51C`, la VM `0x8003DA70`, C98 `0x80064D90`, E17 `0x8007F420`, et le reste de `0x8003C954`.
  - Les 36 gardes se prouvent soit par suivi de registre (13 cas), soit par une relecture de fin de boucle (les 23 autres).
- **Passe des listes `0x800384F4`.** FAIT [V]. Boucle inclusive de 0 à count. Trois listes :
  - physique (statut 2 ou 3, `+0x20 == 0`) ;
  - collisionnable (`+0x6C & 0x80`, `+0xB4 & 0x80 == 0`, `+0x28 == 0`) ;
  - affichage (statut 2 ou 3, `(+0x1CC & 3) != 3`).
- **DLL.**
  - `HighestLiveSlot()` est privé (`Maps/WorldProxy.cs:182`) et ne sert qu'à une sonde. Chaque entité porte `Slot`, et la liste est triée par emplacement.
  - `EntitySlotTable` prend le plus bas emplacement libre, comme `0x80039AD0`. Un emplacement détruit est tenu jusqu'au recyclage.
  - INFÉRENCE : le maximum à la demande après recyclage reproduit le compte du binaire, y compris pour les entités détruites dans le tick. À tester avec `vec_slotpass`, `vec_listspass` et `vec_birthraise`.

## 4. Brique 3 : la requête `0x8003AF70` et le tri `0x8003AE04`

**Requête.** FAIT [V], 1500 vecteurs, 94/94.

Signature : `a0` appelant, `a1` masque, `a2` type d'attaque, `a3` tableau de pointeurs, 5ᵉ argument `[sp+0x10]` tableau de clés. Elle rend n.

Boucle sur les emplacements 1 à count−1. Elle saute :
- l'appelant ;
- les statuts ∉ {2,3} ;
- `+0x20 ≠ 0` ;
- `(+0x6C & mask) == 0` ;
- `+0x1D0 ≠ 0` ;
- `+0x1CC ≠ 0` ;
- `+0xB4 & 0x40` ;
- `+0x1C4 == 0` ;
- `(octet[+0x1C4 + attr + 2] & 0xC0) == 0x80`.

La clé est `dx² + dy²` modulo 2³², avec `dx = (PosX_e − PosX_appelant) >> 16` (la différence 32 bits, puis décalage arithmétique). Le héros (emplacement 0) n'est jamais candidat.

**Tri.** FAIT [V], 91/91.
- Il n'est appelé que si n ≥ 3.
- Hoare, clés non signées, pivot = (clé[lo] + clé[hi]) mod 2³² >> 1, échange des deux tableaux, récursion sur (lo, i−1) et (j+1, hi).
- Instable : avec n = 2 appelé en direct, le vrai tri permute même deux clés égales.

**Appelants.** Quatre sites `jal`, aucune table de pointeurs (balayage des mots de données). Masque 1 (`ClassA`), tableaux globaux `0x800CC058` (pointeurs) et `0x800CC158` (clés).

| Site | Appelant | Attaque (`a2`) |
|---|---|---|
| `0x80035FA4` | pilote du vent 1 `0x80035EB0` | héros, constante 0xB |
| `0x8007ACB4` | C63 | `octet[+0x1C8] & 0xF` |
| `0x8007AF5C` | C65 | `octet[+0x1C8] & 0xF` |
| `0x8007BF20` | C75 | `octet[+0x1C8] & 0xF` |

**Mutants.** 12/13 distingués (inclusif, tri dès 2, statut 2 seul, ignorer `+0x20`/`+0x1D0`/`+0x1CC`/`+0xB4`, masque en égalité, immunité large, distance sans décalage, tri stable, appelant inclus). 202 vecteurs avec clés égales ; le tri stable diffère sur 135.

**Tirages synthétiques.** Même avec n ≥ 3 le premier résultat est le plus proche à 100 %. Parmi les cas où le plus proche est à égalité, Hoare diffère d'un tri stable dans environ 50 % des cas.

**DLL.** `EntitySearchService.cs:99` (`GetMatchingEntitiesBySearchType`) est un autre algorithme, sans tri de Hoare.

## 5. Brique 4 : tables

Dumpées dans `vec/tables.json`.
- `sin256` à `0x800204F0` (256 demi-mots). Valeurs de départ : 0, 6, 12, 18, 25, 31, 37, 43, 49, 56, … ; pic 256 à l'indice 64.
- `cos256` à `0x800206F0`, démarrant à 256.
- Vérifié en [V] sur 256 entrées : `sin256[i] = trunc(256·sin(2πi/256))` (arrondi ou plancher : faux), `cos256[i] = sin256[(i+64)&255]`, symétrie impaire à +128.
- Tables 32 directions : X `0x80023654` (±768 en 8 et 24) et Y `0x80023694` (±512 en 0 et 16). Y vaut `round(512·cos(2πd/32))`. X n'a **pas** de forme close, car `trunc` et `round` échouent (par exemple d = 3 donne −422 pour −426,7 attendu). Les deux tables sont identiques à celles de la DLL (`Entities/AnimationTables.cs:71,82`).
- Les tables 256 sont indépendantes des tables 32 directions. Rapport de forme 3:2 commun.
- Utilisateurs : 256 par C65 et C75 seulement, 32 directions par une cinquantaine de sites.
- DLL : aucune table 256 (`rg` vide, `e20-h3 check.md` C2). Je préconise de les prendre littéralement, car la formule `trunc` reste sensible à l'implémentation.

## 6. Brique 5 : le coup de caméra `[0x800DD874]`

- **Écrivain.** FAIT : E13 `0x8007F30C`, `sw 2` en `0x8007F350`, sur la première passe de statut 3 (`+0x88 ≠ 1`). Il pose aussi `+0x88 = 1` et `+0x6C = (+0x6C | 0x40) & ~0x30`. Si `+0x88 == 1` et `+0xAC == 1`, il détruit via `0x8003A59C`. [V] 1500 vecteurs.
- **Corpus.** E13 : enregistrement global 17, plus 1 enregistrement et 1 placement dans une carte (carte 321, sprite 213).
- **Consommateur.** FAIT : le pas de caméra `0x8002CDA0` (appelé de `0x8002BDB8`, dans `0x8002BD60`, lui-même appelé à `0x8002C3FC`).
  - Premier appel après le chargement (`[0x800E42B8] ≠ 0`) : `scrollX = tx − 0xA0`, `scrollY = (ty − tz) − 0x88`. Le coup n'est pas appliqué mais il est effacé.
  - Cas normal : `scroll += ((cible − (scroll + demi-écran)) >> 4) + décalage du tremblement + coup`.
  - Le coup est lu en `0x8002CE6C`. Les remises à zéro sont en `0x8002CE98` et `0x8002CD7C` (chargement de carte), et `0x8002CE8C`/`0x8002CD8C` pour X.
  - Bornes du défilement : [0, 0x39F] en X et [0, 0x2CF] en Y.
- **Unité.** FAIT : les registres `0x800E4328/32C` sont des entiers en pixels. Les cibles sont `PosX>>16`, `PosY>>16`, `PosZ>>16` du héros (`0x8003B454-0x8003B474`). L'unité du coup est le pixel de défilement. Cela résout Q-H4-6 (+2 px de défilement Y, sur un tick de caméra).
- **Vie du coup.** [V] sur le modèle : +2, puis −1, puis −1, puis 0 (le décalage arithmétique de −2 donne −1).
- **Temporalité.**
  - FAIT : dans la boucle `0x8002C3F4` à `0x8002C45C`, l'ordre est `0x8002AC2C`, caméra `0x8002BD60`, tick `0x8002BAEC(0)`, puis attente.
  - INFÉRENCE : le coup écrit au tick N est consommé par le pas de caméra qui précède le tick N+1. Il est visible à l'image d'après, en même temps que le déplacement de cible de ce tick.
- **DLL.** `CameraMath.cs:216-228` ne contient aucun coup (`DD874` absent). `OffsetY` et le coup s'additionnent dans la même somme du binaire. INFÉRENCE : il faut les additionner avant l'appel, avec le même signe que `swayOffsetY`. Le contrôle est à faire avec `vec_camera`.

## 7. Brique 6 : le bloc de tremblement `0x800E4338`

- **Disposition.** FAIT, 9 mots :
  - `+0x00` Flag ;
  - `+0x04` LimitX, `+0x08` LimitY ;
  - `+0x0C` SpeedX, `+0x10` SpeedY ;
  - `+0x14` OffsetX, `+0x18` OffsetY ;
  - `+0x1C` ReachX, `+0x20` ReachY.
  - C'est la disposition de `CameraSway.cs:33-41`.
- **Pas par tick.** FAIT [V], `0x8002C894`, appelé en tête du pas de caméra.
  - Flag = 0 : Limit, Speed, Offset à zéro (Reach intact).
  - X : `LimitX == 0` ou `SpeedX == 0` donne `OffsetX = 0`.
  - Y : seul `LimitY == 0` efface. Une vitesse Y nulle fige le décalage.
  - Triangle par axe. Reach 0 : `Off −= Speed`, et si `−Limit ≥ Off` alors Off = −Limit et Reach = 1. Reach 1 : `Off += Speed`, et si `Off ≥ Limit` alors Off = Limit et Reach = 0.
  - Cela correspond à `CameraSway.Step` (`:65`).
- **Écrivains du bloc.**
  - Opcodes 0x8E (`0x8004053C`) et 0x8F (`0x8004059C`).
  - Remise à zéro de Flag au chargement (`0x8002CD60`).
  - Gestionnaires natifs : C39, C40, C43, C49, C50, C51, C88, C89, C90, E24, plus le pilote du feu 2.
  - Corpus : C39 (2 cartes), C40 (3), C43 (3), C49 (2), C50 (1), C51 (1), C88 (1), E24 (3 enregistrements).
- **Pilote du feu 2 `0x80035C64`.** FAIT [V], 1500 vecteurs.
  - Globales : phase `[0x80127160]`, compteur `[0x8012715C]` (incrémenté après le pilote par `0x8003634C`), pointeur `[0x80127124]`, identité `[0x80127128]`, tampon `0x80127138` (9 mots, juste avant le compteur).
  - Phase 0 : garde `0x800351B8`. Si l'animation du héros est `0x33`, naissance du sprite 21 à la position du héros par `0x80039F58` (le pointeur est rangé avant le test de nullité ; nul rend 1). Copie du bloc vers le tampon, phase 1, identité mémorisée, puis enchaînement immédiat dans la phase 1.
  - Phase 1 : garde (qui annule et restaure si elle dit non). Si l'identité de l'entité est inchangée : `Flag = 1`, `LimitY = SpeedY = c>>4`, `LimitX = SpeedX = c>>6`. Sinon : restauration du tampon, `0x80127DB8 = 0x34`, phase 2.
  - Phase 2 : rend 0 si l'animation vaut 0x34, sinon 1.
  - Annulation `0x80034E08` : restauration inconditionnelle des 9 mots, puis les entités de type 0x15 au statut 2 passent à 3 avec `+0x20 = 0`. Boucle 1 à count inclus.
  - La restauration se fait sans garde : un tampon jamais sauvegardé (annulation avant naissance) remet le bloc à zéro ou à une valeur périmée.
  - Le tampon n'est lu et écrit que par `0x80034E14` et `0x80035D04/D88/E28`.
- **DLL.** `CameraSway` existe, session unique. Il n'y a ni tampon ni capture/restauration. INFÉRENCE : un tampon de 9 entiers initialisé à zéro, par session et hors sauvegarde.

## 8. Brique 7 : utilisateurs de chaque brique

| Brique | Utilisateurs dans le binaire | Tomes concernés |
|---|---|---|
| Requête et tri | les 4 appelants (§4) | feu 1 (C63), vent 1, vent 2 (C65), eau 1 et 2 (C75) |
| Plus haut emplacement | 24 fonctions (§3) | les 5 annulations ; phase 2 de l'eau |
| Poussée | écrivains du §2.3 ; seul consommateur `0x800366FC` | vent 2, eau 1 et 2 |
| `sin256/cos256` | C65, C75 | vent 2, eau |
| Coup Y | E13 seul | terre 2 |
| Coup X | personne | aucun |
| Bloc de tremblement | opcodes 0x8E/0x8F, 10 gestionnaires natifs, feu 2 | feu 2 |
| Tampon `0x80127138` | feu 2 seul | feu 2 |
| Mots 32 bits `+0x274` | A20, A255, C62, C63, C72, C74, C75, C76, C255, F1, F255, aides `0x80032B90`, `0x80032C7C`, `0x80035790`, `0x80035A84`, `0x8003453C` | feu 1 (C63), eau |
| Mots 32 bits `+0x280..+0x290` | C75, C255, A255, C90, C97, C2, C8, C16, C21, C46, C51, C54, C55, C56, C86, D20, F255 | eau |
| E3 | enregistrements globaux 16, 23, 150-153 ; 98 enregistrements de carte dans 75 cartes ; 19 placements dans 5 cartes | terre 1 (16), feu 1 (23) |
| E28 | global 21 seul | feu 2 |

Détail des mots 32 bits : la liste vient de `out/fields_274_290.txt`. La DLL a `Bytes` de 4 octets à `+0x274` et `AIValues` de 10 demi-mots à `+0x280..+0x293`, qui recouvrent exactement les 5 mots. Précédent de composition : `Ai/Objects/SaveBook.cs:164-171`.

## 9. Découpage proposé de h4b

La poussée n'est liée qu'à deux tomes (vent 2, eau). Cinq tomes sur huit n'en dépendent pas. Toutes les tranches restent inertes en production (D-H-3), via des coutures de test.

| Tranche | Contenu | Prérequis | Tomes servis | Preuve |
|---|---|---|---|---|
| **h4b1a** | Plus haut emplacement exposé (recalcul à la porte du tick, relèvement à la naissance, zéro au chargement), requête, tri de Hoare exact, tampon partagé | h4a ; décisions Q-B3 à Q-B5 | feu 1, vent 1 | `vec_slotpass`, `vec_listspass`, `vec_birthraise`, `vec_query` |
| **h4b1b** | Coup de caméra Y (E13 l'écrit, la caméra le consomme), tampon du tremblement | rien au-delà du `CameraSway` et de `CameraMath` existants | terre 2, feu 2 | `vec_camera`, `vec_sway`, `vec_e13`, `vec_fire2` |
| **h4b2a** | Consommation de la poussée dans `RunOneKinematicTick`, avant le serrage ; remise à zéro ; héros inclus ; cas porteur ; drapeau de serrage | Q-B1 | vent 2, eau | `vec_push`, `vec_physpass` |
| **h4b2b** | Tables 256 littérales, vues 32 bits sur `+0x274..+0x27C` et `AIValues` | Q-B8 | vent 2, eau | `tables.json`, accès en mot |

- **Recommandation.** Sortir l'inscription commune d'E3 de h4b : elle suit la première tranche d'élément qui en a besoin (terre 1, feu 1), et dépend du registre cumulatif d'E14.b0 (Q-H4-8). La clé `(E,3)` ne peut avoir qu'un enregistrement.
- **Dépendances par tome.**
  - terre 1 : aucune brique de h4b (E3 seul).
  - terre 2 : h4b1b.
  - feu 2 : h4b1b.
  - feu 1 et vent 1 : h4b1a.
  - vent 2 : h4b1a, h4b2a, h4b2b.
  - eau 1 et 2 : h4b1a, h4b2a, h4b2b, plus les soins déjà prévus.

## 10. Questions ouvertes

**Q-B1 (Q-H4-3 : propriétaire de la poussée).** FAIT : la poussée est une brique de moteur partagée avec des ennemis et des boss, qui poussent aussi le héros.
- Option A, E20 la porte maintenant (h4b2a) : quelques lignes dans `RunOneKinematicTick` et une couture. Aucun effet visible ailleurs tant qu'aucun producteur n'est porté.
- Option B, attendre E14 : l'eau et le vent 2 attendent.
- En jeu sans poussée : les orbes d'eau restent au point de naissance sans orbiter ni voler, et la tornade n'attire personne.
- INFÉRENCE : A est la moins chère.

**Q-B2 (confirmer en jeu).** Les orbes d'eau se brisent-elles contre un mur ou au bord de la boîte ? Binaire : oui par `0x10 + ForceAdjusted`. Si l'auteur ne le veut pas, retirer le bit coûte un écart documenté ; le conserver ne coûte rien.

**Q-B3 (Q-H4-2 a : le dernier emplacement n'est jamais visé).**
- En jeu : l'ennemi du plus haut emplacement est ignoré tant qu'aucune entité n'occupe un emplacement plus haut.
- INFÉRENCE : c'est surtout vrai quand des trous laissent les entités du sort naître sous le dernier placement de la carte.
- Reproduire = borne exclusive (coût nul). Corriger = borne inclusive (coût nul). La règle de l'auteur "un bug avéré se corrige" penche vers corriger, mais le comportement change dans des cas rares.

**Q-B4 (Q-H4-2 b : pas de tri avec 2 résultats).**
- En jeu : avec exactement deux ennemis éligibles, l'orbe ou la tornade vise l'ancien emplacement, soit le plus loin dans environ 48 % des cas (tirages synthétiques).
- Reproduire = seuil 3. Corriger = seuil 2 (le vrai tri gère bien n = 2).
- Pour le vent 1, l'ordre complet des résultats compte, pas seulement le premier.

**Q-B5 (tri instable).** Les égalités exactes de distance² sont rares, et le résultat dépend de l'ordre de Hoare. Je recommande de porter l'algorithme exact, à coût nul et déterministe, sans trancher entre "corriger" et "reproduire".

**Q-B6 (coup de caméra).** En jeu : une secousse d'environ 2 px sur une image à chaque impact de rocher. L'ignorer ne se verrait presque pas. L'implémenter coûte un champ et un contrôle de signe avec `vec_camera`.

**Q-B7 (E3 et E13).** Ils sont partagés avec 98 sprites d'ennemis dans 75 cartes. Si h4 enregistre `(E,3)` pour {16, 23} seulement, E14 devra étendre la même clé. Option : une inscription d'union gérée par E14.b0.

**Q-B8 (vues 32 bits).**
- Option : des accesseurs de composition sur les tableaux existants (little-endian) ; aucun utilisateur existant ne change. Ils profitent aussi à E14 (11 familles de gestionnaires et des aides).
- Option : des accesseurs propres aux tomes, avec duplication ensuite.
- INFÉRENCE : les valeurs de C63 (≤ 16) tiennent dans un octet, mais pas celles de l'eau (jusqu'à 3584 et ±196608).

**Q-B9 (tables 256).** Littérales (2 × 512 octets) ou formule `trunc` : les deux donnent les mêmes valeurs, mais la formule dépend de la précision du calcul.

**Q-B10 (délai de ForceAdjusted).** `ForceAdjusted` posé au tick N par le serrage ou par la collision est vu par la passe d'événements du tick N+1, puis remis à zéro au début de la physique. La DLL le pose déjà après `SlideAlongWall` (`ScriptedMotion.cs`, E20.e0k3). Ce n'est pas une question, mais un point à épingler en test.

## 11. Corrections aux annexes précédentes et INCONNU restants

**Corrections.**
- Le gabarit d'entité vide a `+0x48 = −1` (le harnais le met à zéro).
- Le serrage de la poussée se fait à la boîte fixe 52 × 60 cases, pas à la carte réelle.
- Le héros n'a pas de cas porteur dans la passe de forces.
- Le coup X est du code mort.
- Le bit 0x2000 est "pas de glissement" (tranché au binaire).
- Le tampon `0x80127138` n'a pas d'autre utilisateur que le feu 2.
- D13 est confirmé.
- "Seul le plus haut emplacement est ignoré" est confirmé.

**INCONNU.**
- Écritures de `+0xCC/+0xD0` par déplacement calculé (opcodes génériques de la VM).
- Corps de C62, D30, E12, C73, E21.
- Placement du sprite 154 dans les cartes.
- Sons des effets.
- La passe de forces n'est couverte qu'à 153/244 (hors branche Z du héros et glace/eau).
- Rendu visuel exact du coup de caméra : non vérifié en jeu.

## 12. Fichiers et rejeu

- `h/`
  - `tools.py`, `funcs.py`, `scan_off.py`, `handler_map.py`, `vtrace.py` : outils.
  - `b1_push.py`, `b1b_physpass.py`, `b2_slots.py`, `b2_readers.py`, `b2_track.py`, `b3_query.py`, `b3_firstpick.py`, `b4_tables.py`, `b5_camera_shake.py`, `b7_corpus.py`, `b7_fields.py` : modèles et vecteurs.
  - `run_all.py`, `replay_all.py` : relance et rejeu. Les lancer avec `python -B` depuis une copie.
- `out/`
  - Listings `dis_*.txt`.
  - `highest_slot_readers.txt`, `highest_slot_tracking.txt`, `scan_off_cc_d0.txt`, `scan_off_13c.txt`, `scan_off_4360.txt`, `fields_274_290.txt`, `corpus_users.json`, `handler_map.txt`.
  - `run_summary.txt`.
- `vec/`
  - `vec_push_800366FC.json`, `vec_physpass_80036828.json`, `vec_slotpass.json`, `vec_listspass.json`, `vec_birthraise.json`.
  - `vec_query_8003AF70.json`, `vec_camera_8002CDA0.json`, `vec_sway_8002C894.json`, `vec_e13_8007F30C.json`, `vec_fire2_80035C64.json`.
  - `tables.json`, `README.txt`.