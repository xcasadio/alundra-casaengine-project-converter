# disc-e20g3 : solidité des soulevables, contact, barricade de la 392 (binaire ALUN_CD.EXE France)

**`report.md` n'a PAS été écrit** : l'environnement refuse qu'un sous-agent écrive un fichier rapport. Ce message est le rapport complet. Le reste est dans le dossier demandé : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20g3/bin/` (scripts, `out/`, `lst/`, `MANIFEST.txt` avec le SHA-1 de chaque fichier, 270 lignes).

Découverte en lecture seule. Rien n'a été écrit dans le dépôt, les sous-modules ni le projet exporté : les scripts tournent depuis mon dossier avec `python -B -I`, le dépôt n'est lu que par `cat`/`cp`. `git status` montre ` m CasaEngineMonogame` et ` m alundra-datas-analyser`, déjà présents quand j'ai regardé la première fois ; je n'y ai rien touché. Binaire : SHA-1 recalculé `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Données lues : `data-extracted/data/map_392.json`, `map_14.json`, `map_22.json`, `map_alundra.json`, `alundra-project/Data/sprite-records.json`, `alundra-project/Entities/*/*.entity`.

Convention : FACT = désassemblage (capstone), exécution dans la machine MIPS des annexes E20 (copiée dans `g1a/orc/`, jamais modifiée), ou lecture de données. INFERENCE = déduit, base indiquée. UNKNOWN = non établi. Listings : `lst/phys_36400_38000.txt` (0x80036400 à 0x80038000) et `lst/phys_37f28_38e18.txt` (0x80037F28 à 0x80038E18). Contrôle de l'outillage : j'ai relancé `g9_lift_rules`, `g13_onto_crate`, `g6_thrown_on_entity` et `g18_c4_stone` depuis ma copie ; leurs sorties ont le même SHA-1 que celles du dépôt (`docs/plan-e20-annexe/e20-g1a/work/orc/out/`).

---

## 0. Réponses courtes

- **A1.** Le test entité contre entité est `FindEntityCollisionCandidate` `0x80036F34`, appelé seulement par le déplacement XY `0x80037730` (sites `0x800378C4` et `0x80037908`). Il n'écarte que l'entité elle-même. Tout le reste se joue à l'entrée de la **liste des collidables** (`0x80127C28`, bâtie par la passe `0x800384F4`) : une entité y est si `flags & 0x80` (Collidable), si l'octet d'animation `+0xB4 & 0x80` est nul et si `+0x28 == 0` (non porté). **Aucun test de statut, de genre de ramassage, de classe, ni d'état du héros.** Un soulevable bloque donc le héros toujours, sauf : porté ; lâché par C23 ou C70 (qui effacent `0x80`) ; détruit (hors liste dès le tick suivant) ; ou héros en l'une de ses 6 animations marquées `0x80`. Il bloque tout mover qui est lui-même Collidable (PNJ oui ; la plupart des monstres et les projectiles non). Preuves : 39 657 appels par fonction sur 5 fonctions, 0 écart, 100 % des instructions ; 526 mutants, 438 tués, 88 survivants classés (86 équivalents, 2 non tués sans portée).
- **A2.** Le contact `héros.+0x130` est la valeur de retour du déplacement XY, écrite par `MoveEntity` en `0x80037F08`, lue au tick suivant par `PlayerTryAction` `0x8002EEAC` (lecture `0x8002EF08`) et `CheckEntityInteraction` `0x8002E918`. Un Carré sans contact posé au tick précédent ne soulève rien (mesuré). Le support `+0x12C` est remis à 0 par la passe des compteurs (`0x800389DC`) puis réécrit par la physique ; le code du héros ne lit pas le sien ; `TryLift` lit seulement le `+0x12C` du soulevable. Être debout dessus passe par la même liste et la descente `0x80036BFC`.
- **A3.** Salle est (arrivée) et salle ouest (portail) ne communiquent que par une porte de 3 rangées (x 648 à 696, y 288 à 336), fermée par une colonne de caisses en x = 636. **Le héros passe par-dessus rec5 si et seulement si sa position Y est dans [295,0 ; 297,0] px** (2,0 px continus, 3 lignes entières 295, 296, 297, bornes comprises), mesuré à l'unité 16.16 : bord haut des deux côtés, bord bas côté ouest (côté est, le mur de la porte ramène y < 295 à 295). Même fenêtre pour **soulever rec5 puis passer par le trou**. Les deux routes sont fermées de bout en bout sur le vrai code, de la saillie d'arrivée (720, 216) à la fenêtre du portail (x 538 à 541, y 375 à 376). Pas de Gant de puissance (genre 1, inventaire de départ sans l'objet 0x3B). Si la caisse du bas d'une pile de 2 est soulevée, celle du haut tombe de 16 px en 8 ticks et devient une caisse de rang 1 ordinaire. On peut aussi soulever la caisse du haut depuis le dessus de rec5.
- **A4.** 17 en-têtes soulevables : 16 de genre 1, 1 de genre 3 (Pierre très lourde, 46x34x34, Gant requis), aucun de genre 2. 16 sont Collidables ; **la bombe ne l'est pas** (MoreFlags 0x4E). Caisse et Cruche générique ont des en-têtes identiques à l'octet près ; Cruche n°2 ne diffère que par `OffsetY` (-5 au lieu de -7) et `Contents`. À la libération, C23 (caisse, cruche, herbe, glace, rocher tortue, pierre très lourde) et C70 (torche) sortent de la liste ; **C4 (pierre, tonneau, tronc, 4 « objets») et C73 (haricots) y restent, en vol et au sol**.

---

## A1. Le test d'obstacle entité contre entité

### A1.1 Fonctions et appelants (FACT)

| Fonction | Adresses | Rôle | Listing |
|---|---|---|---|
| Passe des listes | `0x800384F4`-`0x8003862C` | bâtit la liste active (`0x80127B28`, compte `0x80127D28`), celle des collidables (`0x80127C28`, compte `0x80127D2C`) et celle des frappables (`0x80134250`, compte `0x8013435C`) | `phys_37f28_38e18.txt:372-428` |
| `FindEntityCollisionCandidate` | `0x80036F34`-`0x800370BC` | premier collidable qui recouvre la boîte | `phys_36400_38000.txt:718-816` |
| Descente `CheckEntityCollisionDown` | `0x80036BFC`-`0x80036D8C` | plus haut dessus d'entité sous les pieds | `:512-613` |
| Montée `CheckEntityCollisionUp` | `0x80036D94`-`0x80036F2C` | plus bas dessous d'entité au-dessus de la tête | `:614-717` |
| Hauteur du sol avec entités | `0x80037F28`-`0x80038060` | `+0x134` = max(terrain+1, dessus+1 des entités sous les pieds) | `phys_37f28_38e18.txt:1-62` |
| Déplacement Z | `0x800375E0`-`0x80037728` | appelle `0x80036D94` (`0x80037618`) ou `0x80036BFC` (`0x800376A0`) | `phys_36400_38000.txt:1145-1228` |
| Déplacement XY | `0x80037730`-`0x80037E2C` | appelle `0x80036F34` en `0x800378C4` (test de marche montante) et `0x80037908` (blocage) | `:1229-1677` |
| `MoveEntity` | `0x80037E34`-`0x80037F20` | branche « porté » ou `0x800375E0` puis `0x80037730` ; écrit `+0x130` en `0x80037F08` | `:1678-1738` |

Appelants (balayage de toute l'image, `xref.py`, `out/xref_phys.txt`) : `0x80036F34` seulement en `0x800378C4` et `0x80037908` ; `0x80036BFC` seulement en `0x800376A0` ; `0x80036D94` seulement en `0x80037618` ; `0x80037730` seulement en `0x80037F00` ; `0x800375E0` seulement en `0x80037EF8` ; `0x80037E34` en `0x80037648`, `0x800376D0`, `0x80037E6C` (récursions) et `0x8003847C` (boucle de la physique). Aucune table de fonctions ne contient ces adresses. Lecteurs de la liste des collidables : `0x800364C8` (support), `0x80036BFC`, `0x80036D94`, `0x80036F34`, `0x80037F28`, la boucle de rideurs `0x80038404`, la passe elle-même, et la clé de tri de profondeur `0x80039880` (ordre de dessin, pas un obstacle).

**La même routine sert tous les mouvers** : la passe physique `0x80038364` appelle `MoveEntity` pour toute entité active qui n'a pas encore bougé (`0x8003847C`, `phys_37f28_38e18.txt:328-348`). Seul le test de terrain change pour le héros (`0x80037488` au lieu de `0x800373E4`, choix en `0x80037764`-`0x80037780`). Le test d'entité est identique.

### A1.2 Règle exacte (FACT)

1. **Appartenance à la liste des collidables** (`0x80038584`-`0x800385BC`, pour chaque slot 0 à `[0x80134360]` inclus) : `lw +0x6C` puis `andi 0x80` (`0x8003858C`, saute si nul) ; `lw +0xB4` puis `andi 0x80` (`0x800385A0`, saute si non nul) ; `lw +0x28` (`0x800385AC`, saute si non nul) ; sinon ajout. Le statut `+0x10` n'est lu qu'avant (`0x80038548`-`0x80038558`), pour la liste **active** seulement (`(statut-2) < 2` et `+0x20 == 0`). La liste des collidables ne le lit pas.
2. **Porte du sujet** dans `0x80036F34` : le même triple (`+0x6C & 0x80` en `0x80036F74`, `+0xB4 & 0x80` en `0x80036F88`, `+0x28` en `0x80036F94`), plus une exception du seul héros (slot `0x80127D30`) : si `[0x800DC058] < 0` (bit 31, transition d'écran en cours) et `[0x800DC05C] & 0x80000000`, retour 0 (`0x80036F3C`-`0x80036F64`). Le test de terrain du héros a la même exception (`0x80037490`-`0x800374AC`).
3. **Boucle des candidats** (`0x80036FE0`-`0x800370B0`) : le seul saut est `beq a0, a2` (le candidat est le sujet, `0x80036FE8`). Puis recouvrement sur X (`0x80036FF0`-`0x8003702C`), Y (`0x8003702C`-`0x80037068`), Z (`0x80037068`-`0x800370A4`), bords semi-ouverts : delta négatif contre `Width+1` du candidat, delta positif contre `Width+1` du sujet (`Width = (taille<<16)-1`). Le premier candidat qui recouvre les trois axes est renvoyé. Aucun test sur les drapeaux du candidat.
4. **Effet dans le mouvement** (`0x80037908`-`0x80037938`) : candidat trouvé = pas refusé, position restaurée, pas divisé par 2, nouvelle tentative (boucle `0x800377C0`). **L'entité l'emporte sur le terrain** : le test de terrain (`jalr` en `0x80037928`) n'est fait que si aucun candidat. Pas de règle de sortie : seul le nouveau point est testé.

### A1.3 Ce qui est écarté

| Écarté parce que | Où | Remarque |
|---|---|---|
| c'est le sujet | `0x80036FE8` | seul saut de la boucle |
| `flags & 0x80 == 0` | liste `0x8003858C` | bombe, projectiles, la plupart des monstres |
| `+0xB4 & 0x80` | liste `0x800385A0` | octet 0xD de la fiche d'animation courante, copié à chaque départ d'animation (`0x80038B68`-`0x80038B70`) |
| `+0x28 != 0` (porté) | liste `0x800385AC` | dès le tick du `TryLift` (la passe vient après `MovePlayer`) |
| héros pendant une transition | `0x80036F3C`-`0x80036F64` | deux bits 31 |
| **jamais écarté** | | statut 2, 3, 4 ou autre ; `+0x20` ; genre `0x600` ; classes A/B/C ; état du héros ; `+0x12C` |

### A1.4 Preuves

**Vecteurs** (`a1_vec.py`, `out/a1_vec_6000.txt`). Modèles Python de la passe (listes active, collidables, frappables), de `0x80036F34`, `0x80037F28`, `0x80036BFC`, `0x80036D94`, comparés au vrai code sur entités aléatoires : statut 0 à 4, `+0x20`, `+0x28`, `+0x6C`, `+0xB4`, boîtes en pixels entiers ou en 16.16, un axe plat ou décalé d'une unité avec les deux autres recouverts, `n = -1` à 12, mots de transition, pointeur périmé après la fin de la liste. Résultat : 6 000 vecteurs de liste, 39 657 appels de chacune des quatre fonctions de test, **0 écart**. Compteurs du modèle : porte refusée par `0x80` absent 4 641, par `+0xB4 & 0x80` 6 106, par `+0x28` 2 355, par transition 991 ; recouvrement renvoyé 8 245, aucun 17 319. Instructions exécutées : liste 79/79, `0x80036F34` 99/99, `0x80037F28` 79/79, `0x80036BFC` 101/101, `0x80036D94` 103/103.

**Mutants** (`a1_mut.py`, `out/a1_mut.txt`, `out/a1_mut_classify.txt`) : 526 mutants d'un mot (sens de branche, `slt`/`sltu`, immédiats +-1, masques, déplacements +4, `subu`/`addu`, store supprimé), 300 vecteurs chacun. **438 tués, 88 survivants** : 39 copies de registre (`move` vers `subu`/`negu`), 25 `slt` vers `sltu` (opérandes toujours positifs dans le domaine), 20 mots de cadre de pile, 2 `move` vers zéro de registres déjà nuls, **2 non tués** (`0x80037FF0`, `0x80036D00` : incrément de compteur en délai de saut, son omission ne fait lire qu'un pointeur périmé après la fin de la liste). Aucun survivant ne porte sur une porte, un bord semi-ouvert ou un champ lu.

**Scénarios de tick complet** (`a1_scen.py`, `a1_overlap.py`, `a1_destroyed.py`, `a4_flight.py`) :
- Un Beannoïde (carte 14 sprite 79, MoreFlags 0x03, animation 7 par le harnais) marche vers l'ouest sur rec5 : en-tête tel quel, **il traverse la caisse** (x = 615,6 au tick 45) ; avec `+0x6C |= 0x80`, **il s'arrête à x = 657,0, contact = rec5**, au tick 23.
- Porté : liste `[0, 1, 2, 3]` avant le Carré, `[0, 1, 3]` au tick 29 (rec5 sort le jour même, `+0x28 = 0x80127D30`). Lancé : flags `0x3323E4 -> 0x332374` au tick 90, toujours hors liste. Détruit : tick 120 statut 0, flags 0.
- Détruit (`out/a1_destroyed.txt`) : après le vrai `DestroyEntity` `0x8003A59C` (statut 4, flags inchangés), la passe `0x800384F4` **garde la caisse dans la liste** ; après `0x80038634` du tick suivant : statut 0, flags 0, hors liste.
- Chevauchement (`out/a1_overlap.txt`) : héros enfoncé de 0,5 à 1,2 px dans rec5 : sort au tick 1 ; de 1,3 et 2,0 px : bloqué au tick 1 puis sort ; de 5 et 10 px : **bloqué à jamais**. Pas de règle de sortie ; il suffit qu'un pas (au plus 2,4375 px) dégage.
- Lancé C23 contre une autre entité (`g6` relancé) : la caisse lancée n'est ni arrêtée ni posée sur la cible. Lancé C4 (`out/a4_flight.txt`, `g18` relancé) : la pierre garde `flags 0x3323E4`, dans la liste ticks 61 à 83 et au sol.

### A1.5 Un soulevable bloque-t-il le héros toujours ? Exceptions

Oui, tant qu'il est dans la liste. Sorties de la liste : (a) porté, du tick du `TryLift` à la libération ; (b) libéré par C23 (`0x8006B8A8`-`0x8006B8BC` : `flags = (flags | 0x30) & ~0x80`) ou C70 torche (`0x8007B810`-`0x8007B824` : `(flags | 0x34) & ~0x80`) : hors liste dès le tick du lancer, et le projectile n'est plus sujet non plus (il traverse tout) ; (c) détruit : hors liste dès le tick suivant ; (d) sans `0x80` de naissance : la bombe. **Ne sortent pas** : C4 (`0x800669D0`-`0x800669E8` n'efface `0x80` que si `t24 == -1 && +0x80 == 2`) et C73 haricots (`0x8007BB6C` : `flags | 0x10`). Côté héros, aucun état n'intervient (porte, lève, saute, blessé : même `+0x6C`), sauf ses 6 animations dont l'octet `0xD` a le bit `0x80` : **ids 32, 33, 34, 35, 36 et 39** (octets 0xD0, 0xD0, 0xD4, 0xD1, 0xD0, 0xD0 ; fiche `alundra_0`, 94 fiches), états `0x20`-`0x24` et `0x27` de la table `0x80022D3C` (gestionnaires `0x8003251C`, `0x800324B8`, `0x80032680`, `0x80032758`) ; pendant elles le héros n'est ni dans la liste ni sujet. UNKNOWN : leur nom (ce ne sont pas les animations de nage 0x0F, 0x1D, 0x28 de `PlayerManager.cs:133-139`).

### A1.6 PNJ, monstres, projectiles

Un mover n'est arrêté que s'il est lui-même Collidable ; le candidat soulevable est alors toujours pris. Recensement (`a4_census.py`, `out/a4_census.txt`) :
- 395 prefabs : 239 Collidables, 156 non. PNJ et décors mobiles sont presque tous `MoreFlags 0x80` (villageois, chariots, boîtes, blocs, objets lâchés).
- Classe A (monstres, bit 0), non soulevables : 109 prefabs, **82 non Collidables** (ils traversent les caisses), 27 Collidables (interrupteurs, tourelle, murs à boule de fer, piliers, couvercles, bosses Ronan et Nirude).
- Banque commune (`map_alundra.json`) : projectiles et magies, types 3 à 24 et 27 à 29 (MoreFlags 0x14 à 0x74) : **aucun Collidable** ; types 25 et 26 (0x80) le sont ; objets lâchés 30 à 119 le sont ; le héros (type 0) est 0x8C.
UNKNOWN : si un opcode ou un gestionnaire bascule `0x80` d'un monstre en cours de partie. Les écritures `~0x80` repérées (`addiu -0x81`) sont aux adresses `0x8002AD00`, `0x80032C10`, `0x80032D04`, `0x80041D20`, `0x80047598`, `0x80061C1C`, `0x8006229C`, `0x80063DE8`, `0x800648E0`, `0x80064DD8`, `0x80065348`, `0x800669D0`, `0x8006B8A8`, `0x80078FA8`, `0x8007B810`, `0x8007B8E8`, `0x8007E8D8`, `0x8008F67C` ; je n'ai lu que C4, C23, C70.

### A1.7 Écart avec la DLL à noter en g3 (FACT)

`Alundra/Physics/EntitySupport.cs:53-55` affirme que la liste du binaire « ne prend aucune entité marquée pour destruction (son statut n'est pas 2 ou 3) » et filtre `Status != FlagToDestroy` (ligne 55). C'est vrai de la liste active, **faux de la liste des collidables** (aucun test de statut en `0x800385A4`-`0x800385BC`, vecteurs, `a1_destroyed.py`). Effet : un collidable détruit pendant la passe des événements reste un obstacle jusqu'à la fin de la physique de ce tick, la DLL le retire tout de suite. Impact faible (une image).

---

## A2. Contact, dessus, ordre des tests

### A2.1 Anatomie d'un tick (FACT ; ordre de `0x8003B388` repris de `docs/plan-e20-annexe/d1-oracle-moveplayer/notes.md` §3, adresses relues ici)

1. `0x80038634` slots : un slot de statut 4 est réinitialisé (flags à 0, observé).
2. `0x800386D0` événements : `MovePlayer` `0x80031B50` d'abord (`0x800386EC`), puis les autres entités.
3. `0x80038998` compteurs : `+0x1C++`, `+0x1CC--`, `+0x1D0--`, et remise à 0 de `+0x224`, `+0x12C`, `+0x220` (`0x800389D8`-`0x800389E0`). **`+0x130` n'y est pas remis à 0.**
4. `0x800384F4` listes, construites sur l'état du moment : le soulevable que `MovePlayer` vient de lever n'y est déjà plus.
5. `0x80038E18` animations (`0x80038AB4` charge `+0xB4`).
6. `0x80038364` physique : (a) `+0x168`, `+0x140`, `+0x13C` à 0 et recalcul des boîtes (`0x80038394`-`0x800383E0`) ; (b) support `0x800364C8` ; (c) forces `0x80036828` ; (d) rideurs `0x80037364` ; (e) `MoveEntity` de chaque entité non encore bougée (le héros est le slot 0) : **Z d'abord `0x800375E0`, puis XY `0x80037730`**, donc le test XY voit la hauteur du tick ; (f) post-passe `0x80038064` (`+0x134` sol, `+0x144`).
7. effets, 8. coups.

Dans `MovePlayer`, Idle et Moving (`0x80031E3C`-`0x80031EB0`) : `TryUseItem` -> `PlayerTryAction` -> `CheckEntityInteraction` -> `PlayerTryAttack` (épée). Les états de saut n'appellent pas `CheckEntityInteraction`. Sites de `PlayerTryAction` : `0x80031E50` (états 00, 01), `0x80031E98` (02, 2B, 2C, 2D), `0x80031F64` (29), `0x80031FF4` (03), `0x80032128` (2A), `0x800321E4` (0E, 35). `CheckEntityInteraction` : `0x80031E60`, `0x800326A8`.

### A2.2 Le contact `héros.+0x130`

- **Écriture** (FACT) : `0x80037F08` = retour de `0x80037730` ; `0x80037E80` le met à 0 pour un porté. Réécrit chaque tick, pour toute entité active. Sémantique du retour = candidat de l'avant-dernière tentative de la boucle de division du pas (`0x80037DFC` relit `0x34(sp)`) : INFERENCE de lecture ; le comportement ci-dessous est mesuré.
- **Lecteurs utiles** : `PlayerTryAction` `0x8002EF08`, `CheckEntityInteraction` `0x8002E918`. Autres, non lus : aide de côté `0x8003AC9C` (appelée seulement de `0x8007C074`), aides de scripts `0x8003CCD0`, `0x800411BC`, `0x8004124C`, IA `0x800772D8`, `0x800773AC` (C50), `0x8007B71C`, `0x8007B730`, `0x8007C03C`, `0x8007F790`.
- **Mesures** (`a2_contact.py`, `out/a2_contact.txt` ; héros vers l'ouest en y = 296 contre rec5) : contact posé au premier tick bloqué (27) et réécrit à chaque tick tant qu'on pousse ; avec LEFT tenu jusqu'au tick 33, il reste posé au tick 34 (la force décroît) et retombe à 0 au tick 35. **Carré au tick 27** (premier tick bloqué) : épée (anim 16), pas de soulèvement ; **Carré au tick 28** : rec5 portée au tick 28, geste anim 5. Carré seul ou direction + Carré trois ticks après avoir cessé de pousser : épée. Règle : le Carré doit tomber au moins un tick après un tick où le héros, en poussant, a été bloqué par la caisse.
- **Identité** (`a3_contactmap.py`, `out/a3_contactmap.txt`) : c'est le **premier de la liste dans l'ordre des slots**, pas le plus proche. Par la face est (x = 657), selon y : [295 ; 311[ -> rec5, [311 ; 327[ -> rec6, [327 ; 328,5] -> rec7. Dans une pile, c'est la caisse du bas tant que les deux recouvrent les pieds du héros en Z.

### A2.3 `PlayerTryAction`, `TryLift`, `CheckEntityInteraction` (FACT ; `lst/pta_8002eeac.txt`, `lst/trylift_8002edbc.txt`, `lst/cei_8002e910.txt`)

1. Héros porte déjà (`+0x2C != 0`) : `+0x88 = 0x0C` ou `0x30`, renvoie 1 (`0x8002EED0`-`0x8002EF04`).
2. Sinon, `+0x130 != 0` : avec front de Carré (`[0x80126F2A] & 0x80`), renvoie `TryLift(+0x130)` **même si c'est 0** (`0x8002EF2C`) ; sans front, on passe en 3.
3. Boucle sur les slots 0 à `[0x80134360]` : statut 2 ou 3, `+0x20 == 0`, `+0x12C == héros` -> `TryLift(slot)` (`0x8002EF9C`), premier non nul gagne.
4. `TryLift` : genre `(+0x6C & 0x600) >> 9` ; 0 -> 0 ; 1 -> lève ; 2 et 3 -> exige `GetNumberOfItem(0x3B)` ; lever : `héros.+0x2C = objet`, `objet.+0x28 = héros`, état 5/0x0B (ou 0x0C/0x30 si `objet.+0x12C == héros`). Aucun test de distance ni de regard.
- `CheckEntityInteraction` avec une caisse : `+0x60 == 0` et `+0x84 == 0` (aucun programme), retour 0 (`out/a2_cei.txt`). Pas de conflit de priorité.

### A2.4 Le dessus (`+0x12C`)

- Écrit par `0x800364C8` (`0x800365C0`, `0x800365E0` : pour `flags & 0x4100 == 0x100`, le collidable dont le dessus +1 égale le pied et qui recouvre XY) ; remis à 0 par les compteurs ; lu par `0x80037364` (le rideur reçoit `+0xDC/+0xE0` du support, et sa force Z si `+0xF8 == 0`).
- Héros (flags 0x3118C) et caisses (0x3323E4) sont rideurs : une pile se porte elle-même. Mesure : rec10 posée sur rec6 a `support = rec6`.
- L'atterrissage utilise `0x80036BFC` et `0x80037F28` (liste des collidables, aucun drapeau du candidat). Le pied se pose à `dessus + 1 unité 16.16`, ce qui est **à plat** pour le test Z (delta 16,0000153 contre `Depth+1 = 16,0`) : debout sur la caisse, il ne la recouvre pas (`g13` relancé : support = caisse, z = 16 sur une caisse de 16).
- Le code du héros ne lit pas son propre `+0x12C`. Autres lecteurs, non lus : opcodes `0x8003CBE4`, `0x8003F444`, `0x8003F4D0` ; gestionnaires `0x8007B8F4` (C71), `0x8007BD24` (C74), `0x8007F7DC` (E21).

### A2.5 Autres dépendances à la solidité

(1) arrêt XY du héros ; (2) atterrissage, sol, `+0x144`, support ; (3) plafond `0x80036D94` ; (4) contact puis soulèvement ; (5) piles et gravité des caisses (`CanPickup` bit 0 = `0x100`) ; (6) tout mover Collidable ; (7) ordre de dessin `0x80039880` ; (8) opcodes et IA qui lisent `+0x130`/`+0x12C`. Les coups (`0x80039300`) passent par la liste des frappables et ne dépendent pas de la solidité.

---

## A3. La barricade de la 392

### A3.1 Géométrie (FACT : `map_392.json`, spawner réel, `map392_ascii.py`, `a3_hug.py`)

- Carte 52 x 60 cellules de 24 x 16 px ; constantes égales à celles de l'oracle (gravité 128, viscosité 4096, glissement 0, niveau 2, C 7, D 0). Sol de hauteur 4 = z 64 px. Murs (marche `0x41`) aux colonnes 27-28, rangées 9 à 17 et 21 à 24. **Seule liaison entre la salle est (où le héros arrive : B1 le pose en (720, 216, 64), `docs/plan-e19-opcodes.md` ~2144) et la salle ouest (portail 0 en (22, 23), `Info.Portals[0]`) : la porte colonnes 27-28, rangées 18 à 20 (x 648 à 696, y 288 à 336).** Le héros doit donc passer de l'est vers l'ouest. Rangées 22-23 : le mur n'est ouvert qu'aux colonnes 22 (portail) et 33.
- 12 enregistrements, tous sprite 36 (caisse : `MoreFlags 0xE4`, `CanPickup 0x23`, genre 1), codes d'événements nuls. Le spawner donne X = 12*XPos+12, Y = 8*YPos+8 (`c1world.target_px`), Z = 8*Height (observé 64 et 80). Boîte = position + (-10, -7, 0), taille 21 x 15 x 16.
- Barricade en x = 636 (X de 626 à 647) :

| rec | Y centre | Y de ... à (exclu) | Z |
|---|---|---|---|
| 4 (bas), 8 (haut) | 280 | 273 à 288 | 64 à 80, 80 à 96 |
| **5** | 296 | 289 à 304 | 64 à 80 (**seule caisse de rang 1**) |
| 6 (bas), 10 (haut) | 312 | 305 à 320 | 64 à 80, 80 à 96 |
| 7 (bas), 11 (haut) | 328 | 321 à 336 | 64 à 80, 80 à 96 |

  Les rangées sont séparées de 1 px (288, 304, 320) : le héros (boîte 21 x 15 x 32, Y de PosY-7 à PosY+8) n'y passe pas. rec0 à rec3 : deux piles au nord (x 612 et 636, y 184). rec9 : caisse isolée en (576, 288), salle ouest. Plateau de hauteur 5 (z 80) aux colonnes 20 à 22, rangées 16 à 18, hors passage.
- Dans la porte, les murs nord et sud limitent PosY à [295 ; 328]. **Un héros qui pousse vers le haut contre le mur nord s'arrête à y = 295,000 exactement et glisse vers l'est de 0,75 px par tick** (`out/a3_hug.txt`) ; un départ à y = 291 est ramené à 295. En diagonale gauche + haut, il reste coincé au seuil (x = 694,5, y = 295 ; `out/a2_contact.txt` n°7), sans conséquence ici.
- Vitesses mesurées : 2,4375 px par tick en X (1,2188 au premier tick), 1,625 px en Y.

### A3.2 Fenêtre en Y du saut sur rec5 (FACT)

- Le héros passe au-dessus de rec5 à z de 80 à 91,5, sous le haut (96) des piles voisines : il ne doit recouvrir ni rec4/rec8 (y < 288) ni rec6/rec10 (y >= 305). Donc `PosY-7 >= 288` et `PosY+8 <= 305` : **295,0 <= PosY <= 297,0**.
- Mesure (`a3_sweep.py`, `a3_edges.py`, `out/a3_edges.txt`, `out/a3_sweep_east_summary.txt`, `out/a3_sweep_west_summary.txt`) : course vers rec5 depuis l'est (720, y) et l'ouest (560, y), Croix + direction au tick `tj` de 8 à 26. Réussite (support = rec5) pour **tout PosY de 19333120 unités (295,000) à 19464192 unités (297,000), bornes comprises** ; aucune réussite à 297,0000153 (deux côtés) ni, côté ouest, à 294,9999847. Hors fenêtre : 0 timing sur 19 ; dedans : 15 (est) ou 16 (ouest) sur 19.
- **Où appuyer** (`a3_jumpx.py`, `out/a3_jumpx.txt`, y = 296) : depuis l'est, Croix avec le héros entre x = 694,4 et x = 657,0 (collé à la caisse : bloqué quelques ticks puis monte) ; depuis l'ouest, entre x = 582,1 et x = 615,0. Plus loin (696,8 à l'est, 579,8 à l'ouest) : retombe avant la caisse. Pose sur rec5 18 ticks après la Croix, z = 80,000 (+1 unité). Route R2 : Croix au tick 14, pose tick 32, quitte la caisse tick 46, sol à l'ouest (x = 562, z = 64). Route R6 (ouest vers est) : atterrit puis traverse la porte (x final 724,8, z 64).
- Précise la note E19 R4 (`docs/plan-e19-opcodes.md` ~9572, « fenêtre de 3 px en Y sur la caisse 5 ») : 3 lignes entières de pixels, soit 2,0 px continus bornes comprises.

### A3.3 Soulever rec5 (FACT)

- Genre 1 : pas de Gant (`GetNumberOfItem(0x3B) == 0` dans l'inventaire de nouvelle partie, `a3_glove.py` ; le soulèvement réussit). Les 7 caisses de la barricade ont `flags 0x3323E4`.
- Contact par la face est au tick 27 depuis (720, y). Carré au tick 28 (le premier possible) : rec5 portée au tick 28, geste d'état 5 de **36 ticks** (héros immobile, ticks 28 à 64), puis état 12. Portée (z de la caisse = pieds + 32), rec5 n'est plus dans la liste ; le héros marche vers l'ouest à y = 296 sans contact : **même fenêtre [295 ; 297]**. À y = 298 (R5) : rec5 reste le contact (premier de la liste) et se soulève, mais le héros reste ensuite bloqué par rec6 (x = 657).
- Lancer (`a3_r3b.py`, `out/a3_r3b.txt`) : Carré au tick 65 ; la caisse part à (654, 296, 100), non collidable en vol, casse (anim 2) au tick 88 à x = 588, slot libéré au tick 91 ; le héros passe x <= 615 au tick 102 sans contact (75 ticks après le premier blocage), x <= 562 au tick 124. Pas de « poser » : Carré lance toujours.

### A3.4 Les piles de deux (FACT)

- Depuis le sol, le contact d'une pile est la caisse du bas. **Rec6 levée (tick 29), rec10 tombe** : z 80 -> 79,5 -> 78,5 -> 77 -> 75 -> 72,5 -> 69,5 -> 66 -> 64 au tick 36 (8 ticks), reste en place, `flags 0x3323E4` inchangés, `support 0` : caisse de rang 1 ordinaire (R4).
- Caisse du haut soulevable sans avoir levé celle du bas : héros debout sur rec5 (z = 80) poussant vers le sud contre la pile 19 : y limité à 297,0 (bord plat avec 305), contact = **rec10** (rec6 ne recouvre plus des pieds à 80,0000153), Carré la soulève (R7, `out/a3_routes.txt`).
- Libérer les trois rangées demande environ 5 soulèvements-lancers (INFERENCE, non exécutés).

### A3.5 Routes de bout en bout (FACT, `a3_fullroute.py`, `out/a3_fullroute.txt`)

Un « joueur » scripté lit l'état de la machine avant chaque tick et choisit la manette ; rien n'est écrit après le départ. Départ : saillie d'arrivée (720, 216, z 64), monde réel de la 392, 8 caisses (rec4 à rec11).
- **SAUT** : droite jusqu'à x >= 752, bas 49 ticks (y = 295,62), gauche jusqu'à x <= 692, Croix + gauche, pose sur rec5, descente à l'ouest, bas, gauche, bas : cellule (22, 23) au tick 203, à (538,00 ; 376,00), **dans la fenêtre du portail** (x 538 à 541, y 375 à 376, `docs/plan-e19-opcodes.md` ~2148).
- **SOULEVER** : même approche, gauche jusqu'au contact, Carré (tick 105), geste, gauche porté jusqu'à x <= 600, Carré (lancer), puis bas, gauche, bas : cellule (22, 23) au tick 259, à (538,31 ; 376,00), **dans la fenêtre**.

### A3.6 Quelle route l'original autorise ? (réponse)

Les deux, et une troisième (soulever la caisse du haut depuis le dessus de rec5). Établi : pas de Gant ; la porte est le seul passage ; rec5 est la seule caisse de rang 1 ; le mur nord de la porte ramène un héros qui y pousse au bord de la fenêtre ; saut et lancer exigent la même fenêtre Y de 2,0 px (sauf à vider d'autres caisses). **Ce que l'original « attend » : INFERENCE seulement** (rec5 est la seule caisse basse au milieu de piles de 2 ; aucun événement de la 392 ne mentionne les caisses ; rien dans les données ne désigne une route). UNKNOWN : l'intention des concepteurs.

---

## A4. Sprites soulevables

### A4.1 Les 17 en-têtes (FACT ; `out/a4_census.txt`, `out/a4_census_maps_g1a_rerun.txt`)

| Famille (C / E) | Prefab | Genre | MoreFlags | Collidable | À la libération |
|---|---|---|---|---|---|
| C23 / E2 | Caisse en bois générique, Cruche générique, Cruche n°2, Herbe transportable, Rocher tortue non ennemi | 1 | 0xE4 | oui | efface `0x80` (`0x8006B8A8`) |
| C23 / E2 | Bloc de glace transportable | 1 | 0xC4 | oui | idem |
| C23 / E2 | Pierre très lourde (commun 234, 46x34x34) | **3** | 0xE4 | oui | idem |
| C4 / E2 | Pierre générique, Tonneau générique, Tronc (transportable) | 1 | 0xE4 | oui | **garde `0x80`** |
| C4 / E0 | Objet eau, lune, soleil, étoile | 1 | 0x84 | oui | garde `0x80` |
| C70 / E20 | Torche | 1 | 0xE2 | oui | efface `0x80` (`0x8007B810`) |
| C73 / E21 | Haricots de Jack | 1 | 0x80 | oui | garde `0x80` (`flags | 0x10`, `0x8007BB6C`) |
| C62 / E12 | Bombe (commun 2) | 1 | **0x4E** | **non** | sans objet |

Sur les 395 enregistrements : 378 de genre 0, 16 de genre 1, 1 de genre 3 ; **aucun de genre 2**.

### A4.2 Réponses

- **Un soulevable non solide dans le binaire ?** Oui : la bombe (bit 7 absent, jamais dans la liste ; elle n'est atteinte que par l'objet, voie `0x800345F8` selon g1a). Après le lancer, C23 et la torche sont non solides en vol ; C4 et haricots restent solides.
- **Caisses et jarres pour A1 : identiques.** `Caisse en bois générique` et `Cruche générique` ont les mêmes 16 octets d'en-tête et les mêmes 5 fiches d'animation (vitesses 0, 256, 0, 128, 384, impulsion 1024, octet `0xD` nul) ; seule la banque graphique diffère (36 contre 35). `Cruche n°2` : `OffsetY -5` (boîte Y de PosY-5 à PosY+10) et `Contents 248` au lieu de 130. Aucune de leurs fiches n'a le bit `0x80` à l'octet `0xD`.
- Placements (g1a relancé) : 2 244 de genre 1 sur 237 cartes (caisses et cruches confondues, même signature : 1 910 ; pierres et tonneaux 103 ; torches 57 ; troncs 56 ; cruches n°2 42 ; objets 32 ; rochers 21 ; glace 15 ; herbes 8) ; avec la pierre très lourde (7) et la bombe (1) du banc commun : 2 252, accord avec g1a.

---

## Faits utiles au plan de g3 et g4

**g3 (rendre les soulevables solides)**
1. Le binaire n'a **aucune règle propre aux soulevables**. Retirer le masque `PickupKindMask` de `MovementObstacleProbe.cs:51` suffit côté obstacle, si la liste des collidables de la DLL suit A1.2 (Collidable, anim `0x80`, non porté) et si **C23 et C70 effacent `0x80` à la libération pendant que C4 et C73 ne l'effacent pas**. Une DLL qui retire les C4 lancés de la liste s'écarte du binaire (`out/a4_flight.txt`).
2. Le contact (`XCollisionEntity`) doit être **le premier candidat dans l'ordre de la liste** (ordre des slots = ordre des enregistrements pour les caisses d'une carte) : la caisse du bas d'une pile, rec5 avant rec6 (A2.2).
3. Le soulèvement exige un contact du tick **précédent** : Carré le jour du premier blocage = épée ; le contact tombe dès qu'on cesse de pousser (A2.2).
4. Support caisse sur caisse (piles) : lever une caisse fait tomber celle du dessus ; la DLL doit réévaluer le support au tick du soulèvement (A3.4).
5. Pas de règle de sortie en cas de chevauchement (A1.4) : un héros enfoncé de plus d'un pas (2,4375 px) y reste.
6. Écart DLL : le filtre `Status != FlagToDestroy` de `EntitySupport.cs:55` (A1.7).
7. Valeurs de remesure : héros arrêté à x = 657,000 (face est) et 615,000 (face ouest) en y = 296 ; contact au tick 27 depuis (720, 296) ; geste de 36 ticks ; pose sur rec5 à (Croix + 18) à z = 80 ; fenêtre 19333120 à 19464192 ; chute de rec10 en 8 ticks. Conventions de la machine : pas de 2,4375 px (1,2188 au premier tick) en X, 1,625 en Y, repos à terrain + 1 unité en Z (la DLL pose à terrain, D-E19-59).
8. Portée : 2 244 + 8 placements, dont 219 sur 18 des 30 cartes de la chaîne (g1a, `report.md` §5), changent de statut d'obstacle.

**g4 (barricade de la 392)**
1. Les deux routes sont fermées de bout en bout (A3.5) et exigent la même fenêtre Y de 2,0 px, sauf à vider d'autres caisses (A3.4). Aucune ne demande le Gant.
2. Le mur nord de la porte cale un héros qui pousse vers le haut à y = 295,000 (A3.1) : le joueur n'a pas à viser la fenêtre. Dans la DLL, c'est le glissement de mur du contrôleur qui doit reproduire ce calage (UNKNOWN : comportement actuel de la DLL).
3. Critères d'acceptation possibles : (a) depuis (720, 216), un script d'entrées atteint la cellule (22, 23) dans la fenêtre du portail par SAUT et par SOULEVER ; (b) à y = 298,0 le saut échoue, rec5 se soulève mais le passage reste fermé ; (c) rec6 levée : rec10 tombe en 8 ticks ; (d) rec10 soulevable depuis le dessus de rec5.
4. O-E20-5 : l'original permet les deux. Aucune donnée ne dit laquelle il attend ; décision à l'auteur. Pratique : implémenter les deux, elles sortent de la même règle de solidité sans code propre à la 392.

---

## Inconnus

1. La route attendue par les concepteurs (A3.6) : INFERENCE seulement.
2. Les noms des animations 32 à 36 et 39 du héros (bit `0x80`) et des états `0x20`-`0x24`, `0x27`.
3. Si un opcode ou un gestionnaire (hors C4, C23, C70) bascule `0x80` d'un monstre en cours de partie (A1.6).
4. La valeur de retour de `0x80037730` dans tous les cas limites : contact mesuré, boucle de division du pas non modélisée par des vecteurs.
5. Rôles des autres lecteurs de `+0x130` et `+0x12C` (A2.2, A2.4) : adresses listées, non lus.
6. L'état réel du jeu à l'arrivée sur la 392 (direction, pad, boucle de roulis de B1) : le monde de l'oracle part d'un héros posé par le vrai `InitializeEntity`, pas d'un état capturé. L'absence du Gant est une INFERENCE (la 392 est au début de l'histoire).
7. Les cadences de 1 à 3 ticks par image de la DLL ne sont pas mesurées (le binaire est à 1 tick).
8. Deux mutants non tués (A1.4) : compteur de boucle en délai de saut, sans effet sur les conclusions.

---

## Fichiers (dossier `.../disc-e20g3/bin/`)

Scripts écrits ici : `xref.py`, `scan_off.py`, `handlers.py`, `dumpall.py`, `mipsdis.py`/`exelib.py` (copiés de g1a), `lib392.py` (monde réel de la 392), `a1_vec.py`, `a1_mut.py`, `a1_mut_classify.py`, `a1_scen.py`, `a1_overlap.py`, `a1_destroyed.py`, `a2_contact.py`, `a2_cei.py`, `a3_sweep.py`, `a3_edges.py`, `a3_jumpx.py`, `a3_routes.py`, `a3_r3b.py`, `a3_contactmap.py`, `a3_hug.py`, `a3_glove.py`, `a3_fullroute.py`, `a4_census.py`, `a4_flight.py`, `map392_ascii.py`, `t0.py` à `t5.py` (sondes de mise au point). Sorties : `out/`. Listings : `lst/`. Outils d'oracle copiés : `g1a/` (non modifiés). `MANIFEST.txt` : SHA-1 de chaque fichier. Pour rejouer, depuis ce dossier : `python -B -I a1_vec.py 6000`, `python -B -I a1_mut.py 300`, `python -B -I a3_routes.py`, `python -B -I a3_sweep.py east 294.5 299 0.25 8 26 720`, `python -B -I a3_edges.py`, `python -B -I a3_fullroute.py`.