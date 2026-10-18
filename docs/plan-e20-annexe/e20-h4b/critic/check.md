# check.md : critique adversariale de `disc-h4b/bin` (briques communes de h4b)

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/critic`.
Le même texte est écrit dans `check.md` de ce dossier.
Binaire : `ALUN_CD.EXE` France, SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (recalculé par `cmips.py`, qui refuse tout autre fichier).
Rien n'a été écrit hors de ce dossier. Les fichiers de l'autre agent (`bin/`) n'ont pas été modifiés. Mon verdict prévaut là où il diffère.

Légende : FAIT (vu au binaire à l'adresse dite, dans la DLL, ou dans le corpus) · INFÉRENCE (déduit, source dite) · INCONNU. Verdicts : CONFIRMÉ · RÉFUTÉ · NON ÉTABLI.

## 0. Verdict d'ensemble

- **Tous les faits de comportement du binaire qui portent une décision sont CONFIRMÉS.** Cela couvre :
  - la poussée et son ordre, la passe de forces ;
  - la passe du plus haut emplacement et les bornes `<` / `<=` ;
  - la requête et le tri, les tables ;
  - le coup de caméra, le bloc de tremblement, la restauration du feu 2.
  Chacun est re-dérivé par un modèle écrit par moi (aucun import de ses modèles ni de son interpréteur). Chaque modèle est exécuté contre le vrai code dans **mon propre interpréteur MIPS** (`cmips.py`, environ 250 lignes, écrit ici).
- **Un point est RÉFUTÉ.** L'INFÉRENCE du rapport §3 dit que « le maximum à la demande après recyclage reproduit le compte du binaire, y compris pour les entités détruites dans le tick ». C'est faux (démonstration au vrai code, §3.4). Le champ stocké du §9 du rapport est donc nécessaire, pas facultatif.
- **Des manques** (§7) :
  - la tranche de la poussée est inséparable de la table de glissement et de l'octet `SlideEffectId`, et la DLL n'a ni l'un ni l'autre ;
  - l'ordre caméra / bloc d'entités est inversé dans la DLL : le coup doit être consommé dans le même tick ;
  - `+0x28` est « porté par », pas « porteur » ;
  - un écrivain de `ForceAdjusted` n'est pas listé (`0x80037E78`) ;
  - la portée d'écran de C65 est absente ;
  - les mots 32 bits de l'eau sont mal attribués.
- **Les vecteurs sont rejouables depuis une mémoire vide.** Mon interpréteur rejoue les 770 vecteurs, avec « lecture de donnée non listée = erreur ».
  - C'est plus strict que son README, qui n'impose pas cette règle aux portages.
  - Le lot ne distingue les mutants « borne inclusive » et « ignorer `+0x20` » que si la mémoire creuse du portage **échoue** sur une lecture non listée.
  - J'ajoute 29 vecteurs complémentaires, dont des vecteurs **sentinelles** (§6).
- **Le découpage** est acceptable dans son principe ; je le corrige en quatre points (§8).

## 1. Méthode et preuves (tout est rejouable : `python -B run_all.py`, résultats dans `run_summary.txt`)

| Script | Rôle | Résultat |
|---|---|---|
| `cmips.py` | interpréteur MIPS-I, mémoire stricte (une lecture de donnée absente = `Incomplete`), journal des lectures et écritures | base de tout |
| `cdis.py`, `c0_scan.py` | désassemblage capstone ; balayages de tout le texte : appelants `jal`, accès par déplacement, constructions d'adresse `lui`+`addiu`/`lw`/`sw`, mots de données | voir §2 à §5 |
| `c0_loaddelay.py` | aléa de délai de chargement (une instruction qui lit le registre chargé juste avant) | 5 dans tout le texte, **aucun** dans les 12 fonctions étudiées : mon interpréteur (comme le sien) n'a pas à modéliser le délai |
| `c1_push.py` | modèle de `0x800366FC` | 4000 exécutions, 0 écart (valeurs extrêmes 32 bits incluses) |
| `c1b_physpass.py` | modèle de `0x80036828` + `0x80036614` + `0x800367E4` + `0x800366FC` | 800 mondes, 0 écart, **238/238** instructions couvertes (lui annonce 153/244 : la branche Z du héros et la glace/eau sont couvertes ici), mutants distingués 393 à 767 mondes |
| `c2_loops.py` | bornes des boucles par exécution réelle (§3.2) | tableau §3.2 |
| `c3_query.py` | modèle de la requête et du tri de Hoare | 4000 mondes, 0 écart, 94/94 et 91/91, 211 mondes à clés égales ; tri direct sur 20 000 tableaux, 0 écart |
| `c4_slots.py` | modèles de `0x80038634`, `0x800384F4`, `0x80039D04` + démonstration du §3.4 | 800 mondes chacun, 0 écart, 39/39 et 80/80 |
| `c5_camera.py` | modèles de `0x8002C894` + `0x8002CDA0` | 3000 essais, 0 écart, 184/185 (la 185e est l'adresse d'arrêt) |
| `c6_fire2.py` | modèle du pilote du feu 2 + garde + annulation | 4000 exécutions, 0 écart |
| `c9_replay.py` | rejeu de **ses** 770 vecteurs depuis une mémoire vide | 0 échec |
| `c9b_vecpower.py` | force du lot comme spécification | §6 |
| `c10_extra_vectors.py` | mes 29 vecteurs, dans `vec_extra/` | rejoués, 0 échec |

Une erreur de mon premier modèle de la poussée a été rattrapée par **son** lot de vecteurs, ce qui plaide pour le lot.
- `(ForceX + table)` est une addition 32 bits qui déborde (`addu`, `0x80036758`) **avant** le décalage arithmétique.
- Mon modèle sans repli 32 bits divergeait sur 13/200 de ses vecteurs.
- Corrigé (`s32(fx + TX[i]) >> sh`), 0 écart ensuite.
- Le portage doit donc utiliser une addition `unchecked` sur 32 bits avant `>>`.

## 2. La poussée `+0xCC/+0xD0` : verdicts

| Fait du rapport | Verdict | Preuve |
|---|---|---|
| `0x800366FC` : `adj = ((F + table[idx]) >> sh) + poussée`, `idx = (+0x188) & 0xF`, `sh` = octet signé `[[0x800E4334]+8]` (`srav`, 5 bits bas), poussée **non décalée** | CONFIRMÉ | désassemblage `0x80036710-0x80036764` ; `c1_push.py` 4000/0 écart ; mutant « poussée décalée » distingué par 70/200 de ses vecteurs |
| `+0xCC` et `+0xD0` remis à zéro en `0x80036754`/`0x80036750`, après lecture, avant le serrage | CONFIRMÉ | idem ; le vecteur d'écriture montre les deux zéros |
| Serrage par axe, comparaisons signées **strictes**, bornes X `+0x108`/`+0xFC`, Y `+0x10C`/`+0x100`, `+0x13C = 1`, jamais remis à zéro dans la fonction ; sorties `+0xDC`/`+0xE0` | CONFIRMÉ | `0x80036768-0x800367E0` ; arrivée exactement sur une borne ni serrée ni signalée (vérifié par mes vecteurs complémentaires, §6) |
| Tables de glissement `0x80028B94` (X) / `0x80028BD4` (Y) | CONFIRMÉ | X non nul aux indices 4-6 (-61440), 7 (-983040), 8-10 (+61440), 11 (+983040) ; Y non nul aux indices 1, 5, 9, 13 (-40960) et 2, 6, 10, 14 (+40960) |
| Octet `sh` : 481 cartes à 0, 2 cartes à 1 | CONFIRMÉ | recompté sur `data-extracted/data/map_*.json` : `Info.SlideEffectId` vaut 0 pour 481 cartes, 1 pour les cartes 159 et 160 (cohérent avec `docs/plan-e19-opcodes.md` O-E19-31) |
| Bornes par entité posées par `0x80039C40` ; boîte fixe 1248 x 960 px (0x4E00000 / 0x3C00000) | CONFIRMÉ | seul écrivain de `+0xFC/+0x100/+0x108/+0x10C` (balayage des `sw` de ces déplacements) ; trois appelants : `0x80039E78`, `0x8007CB40`, `0x8007D284`. **NON ÉTABLI** : ce que font les deux derniers (ils reposent les bornes d'une entité dont la taille change) |
| Passe de forces : le héros (pointeur `0x80127D30`) consomme toujours la poussée, sans test de `+0x28` | CONFIRMÉ | `0x80036884-0x80036A80` ne lit jamais `+0x28` ; mutant « le héros ne consomme pas » distingué par 393/800 mondes |
| Passe de forces : toute autre entité de la liste, si `+0x28 != 0` : `+0xB8,+0xC4,+0xC8,+0xDC,+0xE0,+0xE4,+0xE8,+0xEC` à zéro, **poussée ni consommée ni effacée** | CONFIRMÉ | `0x80036A84-0x80036AB4` ; mutant « le porté consomme » distingué par 398/800 |
| Sinon (`+0x28 == 0`) : Z, `0x80036614`, `IncrementForce` x2, `0x800366FC` (seul site `0x80036B9C`), copie `+0xDC/+0xE0/+0xB8` vers `+0xE4/+0xE8/+0xEC` | CONFIRMÉ | modèle complet de 238 instructions, 0 écart |
| Gravité : n'influence pas le XY | CONFIRMÉ | modèle |
| Ordre du tick `0x8003B388` : garde `[0x800DC4B8] & 0x48` ; puis `0x80038634`, `0x800386D0`, `0x80038998`, `0x800384F4`, `0x80038E18`, `0x80038364`, `0x80038E84`, `0x80039300` | CONFIRMÉ | `0x8003B388-0x8003B3F0` |
| Début de physique `0x80038364` : `+0x13C`, `+0x140`, `+0x168` remis à zéro pour chaque entité de la liste (`0x800383B8`) ; `ForceAdjusted` du tick N vu par la passe d'événements du tick N+1 | CONFIRMÉ | `0x80038394-0x800383DC` ; la passe d'événements précède la physique dans le tick |
| Écrivains de `+0xCC/+0xD0` : C49, C51, C65, C75, C89, C96, D43 ; **seul lecteur** `0x80036714/18` | CONFIRMÉ (écritures directes) | mon balayage indépendant des déplacements `0xCC`/`0xD0` donne les mêmes 15 sites hors piles ; rattachés à leurs gestionnaires par la table maîtresse `0x80098F4C` (C49 `0x80075A3C`, C51 `0x80077734`, C65 `0x8007AF20`, C75 `0x8007BD8C`, C89 `0x80061EB8`, C96 `0x800647B0`, D43 `0x8007EBA8` : adresses égales aux entrées de table). **INCONNU** : écritures par déplacement calculé (VM) |
| Le héros est poussé par C51 (garde `+0x144 != 0` et `+0xB4 & 0x40 == 0`) et C89 (`0x80062994/9A0`) | CONFIRMÉ | `0x80077880-0x800778FC`, `0x80062950-0x800629A0` |
| C65 : `(cos256[8·idx]·3) << 8` en X, `sin256[8·idx] << 9` en Y, `idx = (dir+0x14)&31` ; D13 | CONFIRMÉ | `0x8007AFE0-0x8007B018` |
| Une valeur de poussée reste en place tant que l'entité est hors liste physique ; la recopie du gabarit à la naissance et au recyclage la met à zéro | CONFIRMÉ | le gabarit est recopié par `0x8003864C` (passe des emplacements), `0x8003A074` et `0x8003A0E8` (naissances), `0x8003B264` (chargement de carte) ; jamais écrit sauf `0x8003B21C` |

## 3. Le plus haut emplacement `[0x80134360]`

### 3.1 Les trois écritures et la naissance : CONFIRMÉ
- Trois `sw` seulement : `0x800386CC` (recalcul), `0x80039D3C` (relèvement), `0x8003B2D8` (remise à zéro au chargement). Balayage de toutes les paires `lui`/`sw` : 3.
- Recalcul `0x80038634`, pour i de 0 à 63 :
  - statut 4 : recopie des 0x294 octets du gabarit, puis `+0 = i` ;
  - statut ni 0 ni 4 : `max = i`.
  - `slot` / `lists` / `birth` : 800 mondes chacun, 0 écart, 39/39 et 80/80 instructions.
- Relèvement `0x80039D24-0x80039D3C` : `count < [ent+0]` strict, relèvement à `[ent+0]`.
- Toute naissance passe par `0x80039D04` (statut 1 écrit en `0x80039D94`). Seul le héros pose son statut 2, à `0x800319FC`.
- Aucun autre écrivain de statut vers un emplacement libre. Dans le balayage de tous les `sw .., 0x10(base)` précédés d'un `addiu rt,$zero,1..4` :
  - seul `0x80039D94` écrit 1 dans une entité ; les autres `sw 1,0x10(base)` écrivent le bloc de tremblement (base `0x800E4338`) ;
  - les `sw 4` sont des destructions (`0x8003A5BC`, `0x8003A6DC`, `0x8003A77C`) ;
  - les `sw 3` sont des transitions d'entités déjà vivantes.
- Appelée seulement si `[0x800DC4B8] & 0x48 == 0` (`0x8003B394-0x8003B3A0`).
- Le gabarit est du BSS. Le point d'entrée `0x8008B538-0x8008B554` met à zéro `0x800CA0E8..0x801F7F20`, qui couvre `0x80134368`. Il n'est que **copié** (jamais écrit sauf `+0x48 = -1` en `0x8003B21C`). Le rapport disait INFÉRENCE ; c'est un FAIT.

### 3.2 Bornes de boucle : CONFIRMÉ par exécution réelle (pas seulement par lecture de `slt`)
Chaque fonction est lancée avec `count = K` et tous les emplacements au statut 2. On note les emplacements dont un octet est lu (`c2_loops.py`).

| Fonction | emplacements lus (K=10) | bornes |
|---|---|---|
| événements `0x800386D0` (deux boucles) | 1 à 10 | de 1 à K **inclus** |
| compteurs `0x80038998`, effets `0x80038E84`, listes `0x800384F4`, interaction du héros `0x8002EEAC` | 0 à 10 | de 0 à K **inclus** |
| annulations `0x80034BDC`, `0x80034C54`, `0x80034D2C`, `0x80034E08`, `0x80034EC4` | 1 à 10 | de 1 à K **inclus** |
| **requête `0x8003AF70`** | **1 à 9** | de 1 à K−1 : **seule borne exclusive** |

Par lecture en plus : la recherche de départ `0x80034320` (`0x800344F0-0x80034504`, `slt count,idx`) est inclusive, et la phase 2 du pilote de l'eau (`0x80035A0C-0x80035A58`) aussi. Mon balayage de références retrouve 55 des 65 références du rapport (le reste exige un suivi de registre plus large). Il n'en trouve **aucune** que le rapport ait ratée.

### 3.3 Passe des listes : CONFIRMÉ
- Physique : statut 2 ou 3 et `+0x20 == 0`.
- Collision : `+0x6C & 0x80`, `+0xB4 & 0x80 == 0`, `+0x28 == 0`.
- Affichage : statut 2 ou 3 et `(+0x1CC & 3) != 3`.
- Le mutant « borne exclusive » est distingué par 267/800 mondes.

### 3.4 RÉFUTÉ : « le maximum à la demande reproduit le compte du binaire »
Le compte n'est recalculé qu'à la passe `0x80038634` (porte du tick). Une mort dans le tick met le statut à 4 (`0x8003A5BC`) mais **ne baisse pas** le compte avant la passe du tick suivant.

Exécution réelle de `0x8003AF70` (`c4_slots.py`, fin), avec les emplacements 1 à 5 vivants et l'emplacement 6 mort (statut 4) :
- compte stocké 6 (binaire) : la requête rend **5 résultats**, emplacements 1 à 5 ;
- compte « plus haut emplacement vivant » = 5 (calcul à la demande) : **4 résultats**, emplacements 1 à 4. L'emplacement 5, visé par le binaire, disparaît.

Seul un **champ stocké** reproduit donc le binaire. Il est :
- recalculé dans `RecycleDestroyedEntities` (`WorldProxy.cs:3265`, déjà gardé par `PlayerControlFlags` comme `0x8003B394`) ;
- relevé à la création ;
- remis à zéro au chargement.

Le §9 du rapport (« recalcul à la porte du tick, relèvement à la naissance, zéro au chargement ») est juste. L'INFÉRENCE du §3 est fausse.

Conséquence utile, déduite du code. L'invariant est que tout emplacement non 0/4 est ≤ compte : le compte est le maximum des vivants à la passe, relevé à chaque naissance. Les 23 boucles inclusives équivalent donc à « toutes les entités vivantes », et **seule la requête dépend de la valeur exacte du compte**. Les annulations de terre 2 et de feu 2 peuvent itérer toutes les entités vivantes sans l'indice. Le rapport les rattachait à h4b1b seul, ce qui est correct à cette condition.

## 4. Requête `0x8003AF70` et tri `0x8003AE04` : CONFIRMÉ

- Le modèle couvre les filtres, la clé et le héros jamais candidat :
  - filtres, statuts `{2,3}`, immunité `(octet[+0x1C4 + attaque + 2] & 0xC0) == 0x80` ;
  - clé `dx²+dy²` modulo 2³², avec `dx = (PosX_e − PosX_appelant) >> 16` (différence 32 bits d'abord) ;
  - 4000 mondes, 0 écart, 94/94 instructions, 211 mondes à clés égales.
  - Mutants de mon modèle distingués : borne inclusive (234), statut 2 seul (832), ignorer `+0x20` (533), tri dès 2 résultats (157).
- Précision sur les arguments :
  - l'argument d'attaque `a2` des trois gestionnaires est `octet pointé par [ent+0x1C8] & 0xF` (`0x8007ACA0-0x8007ACB8`, `0x8007AF48-0x8007AF60`, `0x8007BF0C-0x8007BF24`) ;
  - vent 1 passe la constante 0xB (`0x80035FA0`) avec les tableaux `s5 = 0x800CC058` et `s6 = s5 + 0x100` (`0x80035F70-0x80035F74`).
- Appelants : exactement quatre `jal` (`0x80035FA4`, `0x8007ACB4`, `0x8007AF5C`, `0x8007BF20`). Aucun mot de donnée n'est égal à `0x8003AF70`.
- Tri : variante de Hoare **à pivot fixé à l'entrée**.
  - Pivot `(K[lo]+K[hi]) mod 2³² >> 1` ; balayages `K[i] < pivot` et `pivot < K[j]` non signés.
  - Échange seulement si `i < j` (strict) ; récursion `(lo, i−1)` et `(j+1, hi)`.
  - 20 000 tableaux (n de 2 à 16, avec égalités) comparés au vrai code : 0 écart.
  - Il trie **correctement** (300 000 tirages dans la plage du jeu : 0 tri faux, premier = minimum à 100 %).
  - Il est **instable** : avec égalités, l'ordre diffère d'un tri stable dans la grande majorité des cas à égalité.
  - Appelé directement avec n=2, il permute même deux clés égales (vérifié sur `0x8003AE04` réel : `[5,5]` donne `[200,100]`).
  - La requête ne l'appelle que si n ≥ 3. Avec n=2, l'ordre est celui des emplacements, et le premier résultat n'est pas le plus proche dans environ 48 % des cas (analytique : ½ moins les égalités).
- Les indices du tri peuvent sortir du tableau si la somme de deux clés dépasse 2³² (pivot replié). C'est inatteignable : les positions sont dans la boîte 1248 x 960 px, donc la clé reste inférieure à environ 2,5 millions. Le mutant `signed_cmp` du rapport est bien équivalent.

## 5. Tables, coup de caméra, tremblement : CONFIRMÉ

- **Tables 256** (`0x800204F0`, `0x800206F0`) :
  - `sin[i] = trunc(256·sin(2πi/256))` et `cos[i] = trunc(256·cos(2πi/256)) = sin[(i+64)&255]` pour 256/256 (flottant 64 bits).
  - Valeurs vérifiées à la main : 256·sin(2π/256) = 6,28 → 6 ; i=2 → 12,56 → 12 ; i=5 → 31,4 → 31 ; i=9 → 56,09 → 56.
  - Utilisateurs : toute construction d'adresse dans `[0x800204F0, 0x800208F0)` est dans C65 (`0x8007AF70-0x8007AF7C`) ou C75 (`0x8007BE50-0x8007BE78`). Aucun pointeur de donnée.
  - Tables 32 directions `0x80023654/94` : identiques à `AnimationTables.cs:71,82` (32/32 pour X et Y). X n'a pas de forme close (d=3 : table −422, exact −426,68).
- **Coup de caméra** :
  - Écrivain unique `0x8007F350` (`sw 2, [0x800DD874]`, dans E13 `0x8007F30C`, sur la première passe avec `+0x88 != 1`). Lecteur unique `0x8002CE6C`.
  - Remises à zéro : `0x8002CE98` (Y) et `0x8002CE8C` (X) à chaque pas, `0x8002CD7C` et `0x8002CD8C` au chargement. Le coup X n'est jamais écrit non nul (balayage `lui`).
  - Premier appel après chargement (`[0x800E42B8] != 0`) : le coup est **effacé sans être appliqué**.
  - Pas : `scroll += ((cible − (scroll+demi)) >> 4) + décalage_tremblement + coup`, donc signe **+** sur le défilement.
  - Vie du coup sur une caméra centrée : `[302, 301, 300, 300]`, soit +2, −1, −1, 0 (`−2 >> 4 = −1`).
  - 3000 essais, 0 écart, tous registres comparés.
- **Calendrier du binaire** :
  - La boucle `0x8002C3F4-0x8002C45C` enchaîne `0x8002AC2C`, la caméra `0x8002BD60` (qui appelle `0x8002CDA0` en `0x8002BDB8`), puis `0x8002BAEC(0)`.
  - Cette dernière appelle `0x8002E058` en `0x8002BC4C`, qui appelle `0x8003B388` en `0x8002E108`.
  - Le coup écrit au tick N est donc consommé par la caméra qui précède le tick N+1. **Voir §7.2 pour la DLL.**
- **Bloc de tremblement** : dispositions et pas par tick CONFIRMÉS (`c5_camera.py`). Le tampon `0x80127138` n'est référencé qu'en `0x80034E14`, `0x80035D04`, `0x80035D88`, `0x80035E28`.
- **Pilote du feu 2** (`c6_fire2.py`) :
  - Phase 0 : garde nulle, animation 0x33, naissance du sprite 0x15. Le pointeur est rangé avant le test de nullité. Puis copie du bloc vers le tampon, phase 1, identité `[ptr+4]`, enchaînement sur la phase 1.
  - Phase 1 :
    - garde non nulle : restauration, rend 1 ;
    - identité inchangée : `Flag=1`, `LimitY=SpeedY=c>>4`, `LimitX=SpeedX=c>>6`, rend 0 ;
    - identité changée : restauration, `hero.+0x88 = 0x34`, phase 2, rend 0.
  - Phase 2 : rend 1 si `hero.+0x88 != 0x34`, sinon 0.
  - L'annulation `0x80034E08` restaure les 9 mots **sans garde**. Elle passe ensuite les entités de `+0x68 == 0x15` et statut 2 au statut 3 avec `+0x20 = 0` (boucle de 1 à compte inclus).
  - Compteur `[0x8012715C]` remis à zéro au départ (`0x80034A98`) et incrémenté après le pilote (`0x8003649C`, `0x8003634C`).
  - La garde ne fait **pas** avancer la phase quand elle rend 1.

## 6. Les vecteurs : rejouabilité et force comme spécification

**Rejouabilité : CONFIRMÉE.** `c9_replay.py` rejoue les 770 vecteurs avec mon interpréteur depuis une mémoire vide.
- **Toute** lecture de donnée doit tomber sur un octet listé, y compris les lectures des tables de l'EXE.
- Résultat : 0 échec. Écritures par (adresse, taille) et valeurs de retour identiques, sauf E13 et les fonctions à bouchon (ci-dessous).
- Les bouchons (`0x80039F58` pour le feu 2, `0x8003A59C` pour E13) sont documentés dans le champ `note`. Leurs appels sont journalisés (`spawn_calls`, `journal_destroy`).

**Réserves sur le lot (chacune corrigée ou signalée) :**
1. **La règle « lecture non listée = erreur » doit devenir normative pour le portage.**
   - Un vecteur ne contient que ce que le vrai code lit. Les mutants « borne inclusive » et « ignorer `+0x20` » lisent des octets que le vrai code ne lit pas.
   - Évalués avec « absent = 0 », ils passent sur les 120 vecteurs de requête (mesuré : 0/120 distingués). Évalués avec « absent = erreur », ils sont distingués (107/120 et 40/120).
   - Le README dit « loading reads into a sparse memory » sans imposer l'échec.
2. **Vecteurs sentinelles** (mon `vec_extra_query.json`).
   - Un candidat vivant et éligible est placé à l'emplacement `count`. Il est **listé dans `reads` bien que le vrai code ne le lise pas** (champ `sentinel_slot`).
   - Le résultat réel l'exclut (3 résultats au lieu de 4).
   - Avec lui, le mutant inclusif est distingué même par une mémoire creuse qui répond 0 aux octets absents.
3. **Trous du lot** (mesurés sur ses vecteurs) :
   - `sort_from2` n'est distingué que par 4/120 vecteurs (7 vecteurs à n=2).
   - Arrivée exactement sur la borne : X-bas 2, X-haut 1, Y-bas **0**, Y-haut 2.
   - Aucun vecteur n'a le serrage simultané sur X et Y.
   - 15 vecteurs à égalités avec n ≥ 3.
   - Mes vecteurs complémentaires, poussée : Y-bas exact, Y-haut exact, X-bas exact, X et Y serrés ensemble, drapeau déjà à 1 qui reste à 1, poussée non décalée avec `sh=1`, `idx=5` avec `sh=1`, `idx=7`, `sh = 0x21` (&31 = 1), `sh = 0xFF` (octet signé −1, &31 = 31), débordement 32 bits avant le décalage, poussée remise à zéro seule.
   - Requête : sentinelle ; compte conservé après la mort d'un emplacement (rend l'emplacement 5) ; n=2 dans le mauvais ordre ; n=2 à clés égales ; n=3 à clés toutes égales (ordre de Hoare `[3,2,1]`) ; 4 résultats avec égalités (`[2,3,4,1]`) ; octets d'immunité 0x80 (écarté) et 0xC0 / 0x40 / 0x00 (gardés).
   - Caméra : coup appliqué et effacé, premier appel après chargement (coup effacé non appliqué), coup X hypothétique.
   - Passe des emplacements : statut 4 au-dessus du plus haut vivant (non compté, recyclé), statuts 1, 3 et 5 comptés, héros seul.
   - Annulation du feu 2 avant toute sauvegarde (le tampon nul est recopié).
4. Le champ `ret` d'E13 n'a pas de sens (la fonction ne définit pas v0, la valeur dépend du bouchon). Les autres `ret` sont significatifs. Un portage ne doit pas contrôler `ret` pour E13.
5. **Piège de portage (NON ÉTABLI comment le traiter).** Le lot est au niveau de l'image mémoire (`+0x114`, `+0xCC`...), alors que le portage C# manipule `EntityScriptProxy`. Il faudra un adaptateur image ↔ proxy (décoder les lectures dans un proxy, exécuter, ré-encoder les écritures). Le README suggère l'inverse (« charger les lectures dans une mémoire creuse »). À trancher dans h4b.

## 7. Ce que le rapport a manqué ou formulé de travers

1. **La table de glissement et l'octet `SlideEffectId` ne sont pas séparables de la poussée.**
   - `0x800366FC` fait en un seul bloc table + décalage + poussée + serrage. 188 de ses 200 vecteurs de poussée ont `idx != 0`, 100 ont `sh != 0`.
   - La DLL n'a ni l'indice `+0x188` (les courants : `plan-e20-heros.md` O-E20-16 et Q-I1, 16 cartes) ni l'octet de carte (aucun consommateur, O-E19-31, cartes 159 et 160 : le héros va deux fois moins vite).
   - La tranche de la poussée (h4b2a) doit dire quelle issue elle prend :
     - porter la table et le décalage maintenant (une vingtaine de lignes, mais deux décisions d'auteur déjà ouvertes) ;
     - ou n'accepter que les vecteurs `idx = 0, sh = 0`, et le consigner.
2. **L'ordre caméra / bloc d'entités est inversé dans la DLL.**
   - Binaire : caméra puis tick. DLL : `RunEntityBlock` (`WorldProxy.cs:2582`) puis `UpdateCameraFollow` (`WorldProxy.cs:2716`, commentaire « C1 : AFTER ... the original's own look-at update is the LAST thing UpdateEntities does »).
   - Les deux ordres sont équivalents pour les cibles (INFÉRENCE, d'après ce commentaire et la lecture du binaire).
   - Pour le coup, la phrase du rapport (« consommé par le pas de caméra qui précède le tick N+1 ») vaut pour l'ordre du **binaire**. Dans la DLL, le coup écrit au tick N doit être consommé par l'avancée de caméra **du même tick N**, celle qui suit le bloc d'entités. Le lire au tick suivant ajouterait un tick de retard.
   - `AdvanceCameraSmoothing` fait `ticksThisFrame` pas après tous les ticks de la trame. Le coup doit donc être **rangé par tick** (précédent : `_effectFrozenOfTick`) pour tomber sur le bon pas.
   - Signe : `CameraMath.cs:223` ajoute `(swayOffsetX, −swayOffsetY)` à l'espace de rendu, donc le coup se soustrait en Y comme `swayOffsetY`.
3. **`+0x28` est « porté par », pas « porteur ».**
   - Le pointeur est dans l'objet porté, vers son porteur. `DestroyEntity` écrit `[+0x28] + 0x2C = 0` (`0x8003A62C`) ; `hero.+0x2C` est l'objet porté.
   - Le lien plateforme / passager est `+0x12C` (`0x80037364`, `RidingEntity` côté DLL).
   - La ligne h4b2a « cas porteur » doit se lire « cas porté (`+0x28 != 0`) ».
4. **Écrivain de `ForceAdjusted` non listé : `0x80037E78`.**
   - `0x80037E34` (déplacement des entités portées, appelé pour `+0x168 == 0`) pose `+0x168 = 1`, `+0x140 = 1`, `+0x13C = 1`. Il recopie la position du porteur (`+0x30/+0x34/+0x38`) à chaque tick.
   - Une entité portée qui porte le bit `0x10` verrait donc `ForceAdjusted != 0` à chaque passe d'événements. INFÉRENCE : non vérifié sur un objet porté réel, dont les drapeaux peuvent différer.
   - Les autres `sw` vers `0x13C` (`0x8002BB60`, `0x800426AC`, `0x8004436C`) ne sont pas des entités (structures de l'écran).
5. **« ForceAdjusted tue les projectiles » : CONFIRMÉ, et déjà dans la DLL.**
   - Chemin `0x80038764` : bit `0x10` de `+0x6C` et (`+0x13C != 0` ou `+0x144 != 0`) → statut 3 et programme E (`a1 = 4`, `0x80038834-0x8003883C`).
   - Sprite 19 : `MoreFlags` 0x34 (52) et `ProgramDeactivate` 0, qui est `0x8007ED10` = `DestroyEntity(ent, -1)`. Le bit `0x10` est aussi dans les sprites 16 (0x14, terre 1) et 23 (0x74, feu 1).
   - DLL : `EntityScriptProxy.cs:1718` (`DeactivateOnImpact && (ForceAdjusted != 0 || IsOnGround != 0)`).
   - Le serrage n'agit que sur les entités `HasLogicalZ` (`ScriptedMotion.cs`, doc de `ClampStepToMapEdge` et de `RunOneKinematicTick`). `SpawnEntityBySpriteIndex` pose ce marqueur (`WorldProxy.cs:3573`), donc les orbes nées par un sort l'ont.
   - Q-B2 se ramène donc bien à « conserver ne coûte rien » pour elles.
6. **Portée d'écran de C65.** Seul le **premier** candidat (dans l'ordre trié) dont `|dx| <= 0x140<<16` (320 px) et `|dy| <= 0xF0<<16` (240 px) reçoit la poussée, puis la fonction s'arrête (`0x8007AF98-0x8007B02C`). Absent du rapport ; cela compte pour la fidélité du vent 2.
7. **Mots 32 bits de l'eau.**
   - C75 ne touche en mot que `+0x274` et `+0x278` (`0x8007BDE0`, `0x8007BFC8-0x8007C09C`).
   - Les mots `+0x280..+0x290` sont écrits par le **pilote** de l'eau `0x80035790` (`0x80035894-0x80035900`). La phase 2 lit le mot `+0x290` (`0x80035A38`).
   - Le tableau §8 du rapport attribue ces mots à C75. À corriger, sans conséquence sur la brique : `+0x278` et `+0x27C` sont déjà des `int` dans la DLL (`EntityScriptProxy.cs:1228-1229`). Seuls `+0x274` (`byte[4]`) et `+0x280..+0x290` (`short[10]`) demandent une composition.
8. **Le gabarit « -1 en `+0x48` »**, donné par le rapport comme correction, est déjà connu de la DLL : `RecycleDestroyedEntities` remet `EntityRefId = -1` (doc de `WorldProxy.cs:3252-3262`).
9. **Recyclage dans la DLL.** `RecycleDestroyedEntities` est gardé par `GameplayBlockedMask`, comme le binaire par `0x48`. C'est le bon endroit pour recalculer le champ stocké (§3.4).

## 8. Jugement du découpage proposé (§9 du rapport)

Le principe est bon : séparer ce que nécessitent deux tomes de ce que nécessitent huit. Corrections :
1. **h4b1a** : à scinder en deux.
   - (i) une fonction pure `requête + tri de Hoare` paramétrée par `count`. Elle se vérifie seule par `vec_query` et mes vecteurs sentinelles, sans toucher à la boucle du monde.
   - (ii) le champ stocké du plus haut emplacement, câblé dans `RecycleDestroyedEntities`, la création et le chargement. C'est la seule partie qui touche la boucle.
   - Les annulations n'ont pas besoin de (ii).
2. **h4b1b** : à dissoudre.
   - Le coup Y a un seul écrivain (E13, h4c2) et le tampon un seul utilisateur (feu 2, h4d1). Chacun se réduit à un champ et un contrôle de signe.
   - Aucune autre tranche n'en dépend. Si on les garde en briques, elles ne bloquent personne d'autre.
   - Le coup doit en outre être rangé par tick (§7.2).
3. **h4b2a** (poussée) : à ne lancer qu'une fois tranché le périmètre table de glissement / octet de carte (§7.1), et à renommer le cas « porté ».
   - Q-B1 option A est la moins chère, mais elle ne porte que le **consommateur**. Les producteurs C49, C51, C89 sont des gestionnaires d'ennemis (E14).
   - Vecteurs : seulement ceux compatibles avec le périmètre retenu.
4. **h4b2b** : les tables 256 ne servent qu'à C65 et C75 ; elles peuvent suivre la première des deux (h4e2) sans tranche propre. Les accesseurs 32 bits servent le feu 1 (C63 lit `+0x274` en mot, `0x8007AC74`) et l'eau (C75 et son pilote). Le vent 2 n'en a pas besoin : les rattacher aux tranches feu 1 et eau.
5. **Sortir l'inscription d'E3 de h4b** : d'accord. 98 enregistrements de carte dans 75 cartes, 19 placements dans 5 cartes, enregistrements globaux 16, 23, 150-153 : recomptés, identiques.

Dépendances par tome (corrigées) :
- terre 1 : aucune brique ;
- terre 2 et feu 2 : leur coup ou tampon seulement (leurs annulations itèrent les vivants) ;
- feu 1 et vent 1 : requête pure + champ stocké ;
- vent 2 : requête pure + champ stocké + poussée + tables ;
- eau : tout cela, plus les mots 32 bits et les soins.

## 9. Avis sur les questions du rapport

- **Q-B1** (propriétaire de la poussée) : option A, avec la réserve du §7.1.
- **Q-B2** : voir §7.5. Le binaire détruit ; garder le bit n'exige que le marquage `HasLogicalZ` déjà posé à la création par indice de sprite.
- **Q-B3** (le dernier emplacement n'est jamais visé) : reproduire = borne exclusive **avec champ stocké** (sinon voir §3.4). Corriger = borne inclusive ; le champ n'a alors plus d'importance pour la requête.
- **Q-B4** (pas de tri à n=2) : corriger = seuil 2, le vrai tri gère n=2 (vérifié sur le vrai code). Pour le vent 1, l'ordre complet compte.
- **Q-B5** : porter la variante exacte (pivot figé, échange strict, récursion `(lo,i−1)` / `(j+1,hi)`). Elle ne coûte rien et reproduit l'ordre des égalités.
- **Q-B6** (coup de caméra) : implémentable en quelques lignes. Unité = pixel de défilement, +2 puis −1, −1 (§5). Ordre à respecter (§7.2).
- **Q-B8, Q-B9** : tables littérales, composition little-endian sur les tableaux existants ; c'est neutre.
- **Q-B10** : confirmé, à épingler en test (`ForceAdjusted` vu au tick N+1, remis à zéro au début de la physique du tick N+1).

## 10. INCONNU restants

- Écritures de `+0xCC/+0xD0` par déplacement calculé (VM), et écritures du coup X par adresse calculée.
- Ce que font `0x8007CB40` et `0x8007D284` (nouvel appel de `0x80039C40`).
- Les corps de C62, D30, E12, C73, E21 (non lus).
- Rendu visuel exact du coup de caméra (non vérifié en jeu).
- Comment le portage rejoue des vecteurs d'image mémoire (§6.5).
- Si les orbes de l'eau sont portées (`+0x28`) : aucune écriture directe de `+0x28` dans C63, C65, C75 ni dans le pilote de l'eau. La fonction de naissance `0x80039D04` range le propriétaire en `+0x0C` / `+0x08`, pas en `+0x28`.

## 11. Fichiers

Tous sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/critic` :
- `check.md` (ce rapport) ;
- `cmips.py` (interpréteur), `cdis.py`, `c0_scan.py`, `c0_loaddelay.py` ;
- `c1_push.py`, `c1b_physpass.py`, `c2_loops.py`, `c3_query.py`, `c4_slots.py`, `c5_camera.py`, `c6_fire2.py` ;
- `c9_replay.py`, `c9b_vecpower.py`, `c10_extra_vectors.py` ;
- `run_all.py`, `run_summary.txt` ;
- `handlers.json` (tables de gestionnaires lues dans l'EXE) ;
- `vec_extra/vec_extra_*.json` (29 vecteurs, même format que `bin/vec`).

Lancer : `python -B run_all.py` depuis ce dossier (lecture seule sur le dépôt et sur `bin/`).