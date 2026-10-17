# Rapport g1a — porter et lancer un objet (binaire, modèles, DLL, tâches)

Rien n'a été modifié dans le dépôt ni dans le binaire. Aucun build, aucun test du dépôt, aucun git qui écrit.

Le dépôt est à `2010ec3` (branche `chantier/script-types-full-name`). Le binaire est ALUN_CD.EXE France, SHA-1 `a705f8da…`. Tout ce que j'ai produit est dans le dossier `S/` :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/g1a/`

`git status` montre ` m alundra-datas-analyser` (un `AlundraTools/log.txt` non suivi, `MGUI` modifié). Ce n'est pas de mon fait : je n'ai fait que des lectures dans ce sous-module.

Convention : FACT = lu dans le binaire, exécuté sur la machine MIPS, ou lu dans le code du dépôt. INFERENCE = déduit. UNKNOWN = non établi.

## 0. À retenir

1. **Le portage est lu et modélisé en entier.** Les décisions du plan §1.24 sont toutes confirmées, avec des modèles Python alignés sur le code réel.
   - Fonctions couvertes : `StepTowards`, `TryLift`, `PlayerTryAction`, `UpdatePlayerCarriedEntity` (3 modes), C23, E2, `DestroyEntity` à un argument, la branche « porteur » de `MoveEntity`, les moitiés « portées » de C4, C70 et C73, et le groupe d'états de portage de `MovePlayer`.
   - Résultat : **0 écart sur plus de 100 000 vecteurs**, 100 % des instructions couvertes (détail au §2).
2. **Corrections au plan et au texte de la tâche** (détail au §3) :
   - E2 est à **0x8007ED30** ; 0x8007ED10 est E0/E1, un simple `DestroyEntity(self, −1)`.
   - La « règle des 5 ticks » concerne un objet **porté** pendant un verrou de contrôle ou une case de sortie. Elle ne concerne pas un objet lancé.
   - La marche avec un objet **démarre sans rampe** : les fiches 5, 6, 7, 46, 47, 48 ont `Acceleration` 0, alors que la marche libre a 1.
3. **O-E20-19 est tranchée par le binaire.** Un objet lancé blesse une cible de classe A à l'impact (démonstration au §1.9). Un objet porté ne blesse personne.
4. **Conséquence de D-E20-39 (INFERENCE).** Aucun chemin de production ne met un objet dans les mains avant g3 et g1b. Il en va de même avant h3 pour la bombe et les haricots. g1a et g2a sont donc inertes en production et se valident par des tests et des vecteurs de référence tirés du binaire. D-G-7 (`LiftContactEntity`) devient sans objet.

## 1. Binaire — faits

### 1.1 TryLift 0x8002EDBC (FACT, `S/lst/trylift.txt`)
- Le genre vaut `(+0x6C & 0x600) >> 9` (0x8002EDD4-0x8002EDD8).
  - Genre 0 : retour 0.
  - Genre 1 : soulève.
  - Genres 2 et 3 : appel de `GetNumberOfItem(0x3B)` (0x8004E428, en 0x8002EDF8). Retour 0 si le héros ne possède pas l'objet.
  - Le `sltiu 4` de 0x8002EDEC est mort, car le masque ne donne que 0 à 3.
- L'objet 0x3B est l'item 59, « Gant de puissance » (prefab `I59_Gant de puissance`).
- Écritures à la levée :
  - `héros.+0x2C = objet` (0x8002EE0C) et `objet.+0x28 = héros` (0x8002EE1C, exécutée dans tous les cas).
  - `héros.+0x88` : si `objet.+0x12C == héros`, alors `+0x144 ? 0x0C : 0x30` ; sinon `+0x144 ? 5 : 0x0B`.
  - `héros.+0x30/34/38 = objet.pos − héros.pos` sur les trois axes. Retour 1.
- Aucun test de distance, de regard, de statut, de taille ni de Z.
- `TryLift` n'est appelée que par `PlayerTryAction`, en 0x8002EF2C et 0x8002EF9C.

### 1.2 PlayerTryAction 0x8002EEAC (FACT)
- Si `héros.+0x2C != 0` : `héros.+0x88 = +0x144 ? 0x0C : 0x30`, retour 1. Cela se produit à chaque tick où l'on porte et où cette fonction est appelée.
- Sinon, si le contact `héros.+0x130` est non nul :
  - avec un front de Carré (`lhu 0x80126F2A & 0x80`, le mot « pressé » du pad), la fonction renvoie directement `TryLift(contact)`, même si c'est 0 ;
  - sans front de Carré, elle passe à la boucle.
- La boucle parcourt les cases 0 à `[0x80134360]` incluses (le héros compris). Elle retient celles de statut 2 ou 3, avec `+0x20 == 0` et `+0x12C == héros`, sans aucun bouton. Le premier `TryLift` non nul gagne. Un compte négatif sort tout de suite avec 0.
- Appelants (6 sites) : 0x80031E50 (Idle et Moving), 0x80031E98 (groupe des sauts), 0x80031F64 (état 0x29), 0x80031FF4 (état 0x03), 0x80032128 (état 0x2A), 0x800321E4 (échelle 0x0E et 0x35).
- `TryUseItem` (0x8002ED64) renvoie 1 sans front de Rond. Avec un front de Rond, il appelle 0x8003499C (ou joue le son 3 si `[0x800DC4B8] != 0`).

### 1.3 UpdatePlayerCarriedEntity 0x8002F884 et StepTowards 0x8002F854 (FACT)
- `StepTowards(cur, tgt, pas)` : si `|tgt − cur| < pas` (comparaison non signée), elle renvoie `tgt`. Sinon `cur + pas` si l'écart est strictement positif, `cur − pas` sinon.
- L'objet est `héros.+0x2C`. Sa fiche d'équilibre est `objet.+0x1C4`, et l'octet `+0xE` donne `AttackCount`. Le compteur est `[0x80098F28]`. Si `héros.+0x2C == 0`, le compteur est remis à 0 et la fonction rend la main.
- `AttackCount == 0` transforme le mode 1 en mode 0.
- **Mode 0** (chaque tick libre) :
  - Décalages lus sur les positions : `dx, dy, dz = objet − héros`.
  - `m = max(|dx|, |dy|)` et `e = |dz − 0x200000|`.
  - Si `m < e`, seul Z avance de 0x10000 vers 0x200000.
  - Sinon (égalité comprise), X, Y et Z avancent chacun de 0x10000.
  - Les décalages sont écrits dans `héros.+0x30/34/38`, et le compteur est remis à 0.
- **Mode 1** (verrou, case de sortie) :
  - Compteur `< 5` (signé) : compteur + 1, rien d'autre.
  - Sinon, si les trois `|d|` valent au plus 0x1FFFF : `DestroyEntity(1 arg)` (0x8003A774), compteur gardé.
  - Sinon : chaque axe avance de 0x20000 vers 0, et le compteur est gardé.
- **Mode 2** (blessure, pente 4, mort) : `objet.+0x24 = −1`, `objet.+0x8C = héros.+0x8C`, position de l'objet = héros + (0, 0, 0x200000), compteur à 0. `héros.+0x2C` n'est **pas** effacé.
- Appelants et modes :

| Site | Mode | Contexte |
|---|---|---|
| 0x80031BF4 | 1 | verrou (`[0x800DC4B8] & 0x34`) |
| 0x80031C64 | 2 | blessure |
| 0x80031D78 | 0 | tick libre |
| 0x8003261C | 2 | pente 4 (nage) |
| 0x80032778 | 1 | case de sortie (`+0x188 & 0x80`) |
| 0x800327AC | 2 | mort |

- Série du mode 0 rejouée par le code réel et par mon modèle, offsets (x, y, z) en px depuis l'objet à l'est (+21, 0, 0) : z monte de 1 par tick, x commence à baisser au tick 12, et (0, 0, 32) est atteint au tick 32. Pour le nord et le sud (±15), Y bouge au tick 18.
- Mode 1 : l'objet meurt au **22ᵉ appel**, soit 21 ticks après le premier (5 appels de compteur, 16 de traction à 2 px, 1 de destruction). Avec `AttackCount = 0`, il ne meurt jamais.
- Relevé des fiches d'équilibre faites avec le vrai spawner (`S/orc/out/sheets-census.txt`) :

| Famille | AttackCount | Conséquence en verrou |
|---|---|---|
| Caisses et jarres (C23, indices 291, 292, 294, 438…) | 1 (attaque 1/1/128) | avalée |
| Bloc de glace (304) | 1 (7/7/128) | avalé |
| Pierre et tonneau (C4/E2, 290) | 1 | avalé |
| Tronc (373) | 7 | avalé |
| Torche (393) | 1 (6/6/1) | avalée |
| Pierre très lourde (234) | 1 (1/1/1) | avalée |
| Bombe (2) | 1 (4/4/5) | avalée |
| **Herbe transportable (297)** | **0** | **gardée** |
| **« Objet (transportable) » (C4/E0, 363)** | **0** | **gardé** |
| Haricots (1) | 0 | gardés |

### 1.4 « PutInHands » : trois sites, un noyau (FACT)
- Le noyau est : `héros.+0x2C = obj`, `obj.+0x28 = héros`, `héros.+0x88 = (+0x144 ? 5 : 0x0B)` (ou 0x0C / 0x30 si l'objet était sur la tête, TryLift seulement), offsets = `obj.pos − héros.pos`.
- Les trois sites d'écriture :
  - **TryLift** (0x8002EE0C / 0x8002EE1C).
  - **Haricots** (0x80034298 / 0x8003429C). Le départ refuse si `héros.+0x2C != 0`, si `+0x18C == 4`, ou si `[0x80127164] != 0`. Il crée l'objet par `0x80039F58`. Il renvoie 0 en succès et 1 en refus (avec le son 3).
  - **Bombe** (0x800345F8 / 0x800345FC). Il refuse en plus s'il y a déjà au moins 3 bombes (`+0x68 == 2` sur les 64 cases). Il écrit `bombe.+0x274 = 0xF0` avant de lier.
- Le départ d'une magie (0x800348F8) et le pilote d'objet (0x80035214, 0x800352D4) lisent aussi `héros.+0x2C`, pour refuser quand on porte.

### 1.5 États du héros (FACT, table de la machine `S/lst/state_table.txt`, code `S/lst/moveplayer_carry.txt`)

| État | Rôle | Fiche (anim) | Transition |
|---|---|---|---|
| 0x05 | geste de levée au sol (son 44) | 5, Chain→12 | pas d'objet → 0x2D ; en l'air → 0x30 ; sinon reste |
| 0x0B | geste de levée en l'air (son 44) | 11, Chain→48 | pas d'objet → 0 ; au sol → 0x0C |
| 0x0C | porter immobile | 12, boucle | voir groupe |
| 0x07 | marche avec objet | 7, boucle, vitesse 208, Acceleration 0 | voir groupe |
| 0x06 | saut en marche avec objet | 6, Chain→47, impulsion 1280, son 10 | voir groupe |
| 0x2E | saut sur place avec objet | 46, Chain→48, impulsion 1280 | voir groupe |
| 0x2F | air en marche avec objet | 47, boucle, vitesse 208 | voir groupe |
| 0x30 | air immobile avec objet | 48, boucle | voir groupe |

**Groupe de portage** (0x06, 0x07, 0x0C, 0x2E, 0x2F, 0x30 ; entrée en 0x8003232C), dans l'ordre :
1. `héros.+0x8C = table[0x80022C6C + 4·(hold>>12)]` (ou l'ancien +0x8C si la table donne −1).
2. `TryUseItem`.
3. Si pas d'objet : sol → 0 (0x80031E7C), air → 0x2D.
4. Front de Carré :
   - si `héros.+0x180 & 0x2000`, rien (tick terminé) ;
   - sinon **lancer** : `objet.+0x8C = mot[0x80023644 + 4·héros.+0x98]` (table {0, 0x10, 8, 0x18} : bas, haut, gauche, droite), position de l'objet = X, Y du héros et Z + 0x200000.
   - Au sol : héros 0x09, `objet.+0x24 = 1` (sans direction) ou 2 (avec).
   - En l'air : héros 0x0A, `+0x24 = 2` (sans) ou 3 (avec).
5. Objet de genre 3 (`flags & 0x600 == 0x600`) : 0x0C au sol, 0x30 en l'air, et rien d'autre.
6. Sol :
   - front de Croix (`pressé & 0x40`) : refusé si `+0x180 & 0x4000`, sinon 0x06 (direction tenue) ou 0x2E ;
   - sinon 0x07 (direction tenue) ou 0x0C.
7. Air : 0x2F (direction tenue) ou 0x30.

Il n'y a **pas de « poser »** : Carré lance toujours (D-E20-41 conforme). Les états 0x09 et 0x0A sont dans les groupes d'attaque (0x80031E18 et 0x80031DF8) : 0x09 en l'air → 0x2D, 0x0A au sol → 0.

Garde d'échelle : 0x80031D54 lit `héros.+0x2C`. L'entrée en 0x0E est refusée si l'on porte.

### 1.6 Écritures et effacements de +0x28 / +0x2C (FACT)
Je l'ai établi par balayage de l'image entière (`S/scan_off.py`, `S/lst/scan_off_nosp.txt`) et par lecture des sites de l'espace des entités. Les autres écritures de ces offsets (écrans, boîtes, structure de warp 0x800DC4C0) ne sont pas des entités.

- **Trois écritures non nulles seulement** (dans tout l'espace des entités) : TryLift, haricots, bombe (§1.4).
- **Effacements de `héros.+0x2C`** :

| Site | Adresse |
|---|---|
| C23 | 0x8006B8A0 |
| C4 | 0x80066A14 |
| C62 (bombe), deux sites | 0x8007AAB8 et 0x8007AAF0 |
| C70 | 0x8007B808 |
| C73 | 0x8007BB60 |
| annulation d'objet haricots / bombe | 0x80034B10 et 0x80034B98 (appelle ensuite le destroy à 1 argument) |
| `DestroyEntity` : trois queues | 0x8003A62C, 0x8003A74C, 0x8003A7A4 |

- Les queues de `DestroyEntity` n'ont aucun test d'identité. Elles existent dans les trois variantes : 2 arguments (0x8003A59C), récursive sur les enfants (0x8003A648), 1 argument (0x8003A774).
- **Effacements de `objet.+0x28`** : C23 0x8006B8AC, C4 0x80066A1C, C62 0x8007AABC et 0x8007AAF8, C70 0x8007B814, C73 0x8007BB68.
- **Tables de libération** :
  - C23, C4 et C70 : `+0x24` ∈ {1→anim 3, 2→1, 3→4, −1→3}, les trois tables étant identiques (0x80027400, 0x800270C0, 0x800281DC).
  - C73 (haricots) : anim 2 dans tous les cas.
  - C62 (bombe) : anim 2.
- **Drapeaux à la libération** :
  - C23 : `(flags | 0x30) & ~0x80`.
  - C70 : `(flags | 0x34) & ~0x80`.
  - C73 : `flags | 0x10`.
  - C4 : drapeaux inchangés, sauf `t24 == −1 && +0x80 == 2` → `| 0x10 & ~0x80`.
- **Lecture du cas pur** : le rendez-vous « la mort d'un objet porté » des familles C4/C70/C73 se comporte comme C23 sur ce point. La différence est seulement la table d'animations et les drapeaux.

### 1.7 Comment l'objet suit le héros et une plateforme horizontale (FACT)
- `MoveEntity` 0x80037E34, branche `objet.+0x28 != 0` :
  - `+0x168 = 1`, puis récursion sur le porteur si `porteur.+0x168 == 0` ;
  - `+0x140 = +0x13C = 1`, `+0x130 = 0`, `+0x134` et `+0x138` copiés du porteur ;
  - position = porteur.pos + `porteur.+0x30/34/38` ;
  - la boîte en cache `+0x1D8/1DC/1E0` est recalculée. Aucune physique propre.
- Autres lieux qui lisent `+0x28` :
  - 0x80036A84 met les forces de l'objet à zéro ;
  - 0x80036C60, 0x80036E08 et 0x80036F94 sautent l'objet dans les tests de collision entre entités ;
  - 0x80037F54 l'exclut de la liste des collidables ;
  - 0x8003981C trie l'objet au moins comme son porteur (la clé est relevée à celle du porteur tant que l'objet est collidable) ;
  - 0x80031498, dans la fonction d'arrivée dans la même carte 0x80031340, décale l'objet du même vecteur que le héros.
- Ordre de la passe physique (0x80038364) : les entités sont d'abord remises à zéro (`+0x168`, `+0x140`, `+0x13C`), puis passes 0x800364C8 et 0x80036828, puis `UpdateRidingEntity` (0x80037364) pour chaque collidable portant un appui, puis `MoveEntity` pour tout ce qui n'a pas encore bougé.
- Plateforme horizontale : le héros reçoit le déplacement de la plateforme via l'appui `+0x12C` (0x80037364 ajoute `+0xDC/+0xE0` du support aux forces `+0xE4/+0xE8` du porteur). L'objet suit le héros par la copie de position. L'objet n'a ni appui ni collision propres.
- Oracle `g5` : `héros.support == plateforme` dès t55, écart X héros − plateforme constant à 0, caisse − héros constant à 0.

### 1.8 Objet lancé : vol, atterrissage, casse (FACT)
- Au tick du lancer (T), C23 passe en une fois la table : `+0x24 → anim (3, 1 ou 4)`, `+0x24 = 0`, liens effacés, `flags 0x3323E4 → 0x332374`.
- Fiches de la caisse (export `map_392` record 36) : anim 3, vitesse 128 (1,5 px/tick) ; anim 1, vitesse 256 (3,0) ; anim 4, vitesse 384 (4,5). Impulsion 1024 pour les trois, avec boîtes d'attaque (24×16×16).
- L'objet bouge dès T : X + 1,5 au tick T, arc de Z 36 ; 39,5 ; 42,5 ; 45 ; … jusqu'à 0 à T+22. Pose à T+22 (état 3 + E2 à T+23, `0x332344`, son 27), état 4 à T+25, case libre à T+26.
- Choix du programme (0x80038764-0x8003883C), statut 2 → 3 (programme E) si :
  - `flags & 0x10` et (`+0x13C` ou `+0x144`) ;
  - ou `flags & 0x20` et `+0x220` ;
  - ou `flags & 0x40` et `+0xAC`.
- Destructions annexes dans le même choix :
  - `flags & 0x100000` et pente 4 → `DestroyEntity(e, 6)` (eau) ;
  - `flags & 0x200000` et `+0x180 & 0x8004` → `DestroyEntity(e, −1)`.
- **E2 (0x8007ED30)** :
  - Si `(anim == 2 && +0xAC == 1) || (+0x184 & 4)` → `DestroyEntity(e, −1)` (loot 0x80032B90, effet = octet d'en-tête `+0x1E`, 10 pour une caisse).
  - Sinon : `anim = 2`, `flags = (flags | 0x40) & ~0x30` (`0x332374 → 0x332344`).
- Table E : E0 = E1 = 0x8007ED10 (destroy pur), E2 = 0x8007ED30, E3 = 0x8007EDA0 (variante anim 1), E4 = 0x8007EE04 (variante anim 4), E5 = 0x8007EE68 (variante anim 1 avec effet 8).
- Mur : arrêt à X = 133, E2 à T+9, libre à T+12. Marche de 16 px : pose dessus puis E2 à T+21. Beannoïde touché en X = 165 : `+0x220` à T+21, E2 à T+22.
- **Règle des 5 ticks** : c'est le mode 1 (verrou ou case de sortie, §1.3), pas un objet lancé. Voir §3.

### 1.9 Un objet lancé blesse-t-il au contact ? (O-E20-19, FACT)
- Passe de coups 0x80039300 :
  - Elle ne considère un attaquant que si `+0x1D4 != 0` et si l'octet pointé par `+0x1C8` est non nul (une boîte d'attaque active dans la trame courante).
  - Le masque de classes d'une caisse (flags 0xE4) ne contient que « touche la classe A ». Le héros (0x8C) n'est pas de classe A : **il n'est pas blessé par ses propres caisses**.
  - Sur un coup, la cible reçoit `+0x224 = attaquant` et `+0x1D0 = 0x19`, et l'attaquant fait `+0x220 += 1`.
- La caisse porte `+0x1D4 = 0` tout le temps qu'elle est portée et pendant l'animation de casse (t11-t60 puis t84 en oracle). Elle porte `+0x1D4 ≠ 0` du lancer (t61) jusqu'à l'atterrissage (t84).
- Le prefab exporté a des `collision_keyframes` pour les animations 1, 3 et 4 seulement (P-G-1 : confirmée côté données).
- Les dégâts sont appliqués par la cible : `ComputeNewHp` 0x8003A374 lit `cible.+0x224`, et l'attaque courante de l'attaquant (`+0x1C8`).
  - Mesure par le vrai code (`S/orc/thrown_damage.py`) : caisse (attribut 1, puissance 128) sur un Beannoïde (map 14, sprite 79) de 9 PV → 0 PV, retour « mort » 1.
  - La harnais appelle `ComputeNewHp` à la place du Beannoïde (son gestionnaire reste inerte dans l'oracle).
  - La caisse se brise sur la cible (E2 à T+22, état 4 à T+24).
- Pour le port (INFERENCE, DLL lue) : la passe de coups de la DLL est générique (`AlundraHitPass.cs`). Elle blessera donc toute cible de classe A dont le programme D est porté (Beannoïde 335 et Muruta 347 seulement aujourd'hui), dès que la caisse lancée est une entité normale avec ses boîtes d'attaque.

## 2. Modèles Python contre le code réel (FACT)

Outils : `S/g1a_models.py` (modèles), `S/vec_g1a.py`, `S/vec_families.py`, `S/orc/sweep_carry.py`, `S/orc/sweep_ladder.py`, `S/timelines.py`. Mutants dans `S/mut_g1a.py` et `S/orc/mut_carry.py`. Les fichiers copiés d'ailleurs sont `exelib.py`, `mipsdis.py`, `emu/` (interpréteur du dossier e14b1) et `orc/` (la machine d'oracle copiée).

| Cible | Adresse | Vecteurs | Écarts | Instructions couvertes |
|---|---|---|---|---|
| `StepTowards` | 0x8002F854 | 16 000 | 0 | 12/12 |
| `TryLift` | 0x8002EDBC | 8 000 | 0 | 60/60 |
| `PlayerTryAction` | 0x8002EEAC | 8 000 | 0 | 77/77 |
| `UpdatePlayerCarriedEntity` | 0x8002F884 | 20 000 | 0 | 164/164 |
| C23 | 0x8006B848 | 8 000 | 0 | 33/33 |
| E2 | 0x8007ED30 | 8 000 | 0 | 28/28 |
| `DestroyEntity` (1 arg) | 0x8003A774 | 4 000 | 0 | 15/15 |
| `MoveEntity`, branche porteur et récursion | 0x80037E34 | 4 000 | 0 | 61/61 |
| C4, moitié portée | 0x80066984 | 5 000 | 0 | 39/39 |
| C70, moitié portée | 0x8007B7B0 | 5 000 | 0 | 31/31 |
| C73, moitié portée | 0x8007BB30 | 5 000 | 0 | 20/20 |
| Groupe de portage du vrai `MovePlayer` (monde de l'oracle, caisse réelle de la 392) | 0x80031DF8, 0x800322B4-0x800324B8, 0x80031D68-0x80031D80 | 12 000 | 0 | 150/150 |
| Garde d'échelle | 0x80031D20-0x80031D64 | 4 000 (269 passages) | 0 | 5 instructions atteintes |

- Les vecteurs tirent des champs voisins au hasard (animation, direction, contact, indicateurs `+0x168`, positions, décalages périmés après un lancer). C'est ce qui a tué les premiers survivants.
- Mutants d'un mot sur les huit fonctions (opérateurs : sens des branches, `slt/sltu`, immédiats ±1, masques, décalage de champ +4) : **416, dont 339 tués**. Les 77 survivants sont tous équivalents :
  - 41 copies de registre `move → subu/negu $zero` ;
  - 26 mots de cadre de pile ;
  - 8 `slt ↔ sltu` sur des magnitudes positives ;
  - 1 `sltiu 4 → 5` sur un champ de 2 bits ;
  - 1 lecture du nombre de cases à l'index −1 (le héros ne peut être son propre appui).
- Mutants d'un mot sur le groupe de portage de `MovePlayer` : **131, dont 129 tués**. Les 2 survivants sont `negu $zero` et un décalage de pointeur que l'interpréteur ne refuse pas (accès non aligné).
- Les 19 scénarios de l'oracle précédent rejoués dans `S/orc/` redonnent les **55 mêmes fichiers** (SHA-1 identiques, `S/orc/out/manifest1.txt`).
- Vecteurs de référence prêts pour des tests C# :
  - `S/lst/goldens_g1a.json` : 850 vecteurs des huit fonctions, entrées et sorties lues dans le code réel (mots par offset, forme éparse) ;
  - `S/lst/goldens_carry_group.json` : 700 vecteurs du groupe d'états.
- Empreintes de tous les fichiers : `S/MANIFEST.txt`.

## 3. Écarts avec le plan, les annexes et le texte de la tâche (FACT)

1. E2 est à **0x8007ED30** ; 0x8007ED10 est E0/E1. L'adresse citée dans la tâche est celle de E0.
2. `h-g-deps.md:44` et O-E20-19 (« quand le héros le rattrape ») prêtent la règle des 5 ticks à un objet lancé. En réalité c'est le mode 1 (verrou ou case de sortie), qui n'agit que sur un objet porté, via `AttackCount`.
3. O-E20-19 est tranchée (§1.9).
4. La ligne « marche portée = comme sans caisse » du plan est vraie en régime établi seulement (2,4375 px par tick). Au départ, la marche libre fait une rampe (fiche 1, `Acceleration` 1 : le pas vaut `|cible − force| >> 1`). La marche portée est à pleine vitesse dès le premier tick (fiche 7, `Acceleration` 0, pas = écart entier). Le départ de la marche portée vaut donc `X + 2,4375` au premier tick (oracle g1 : 109 → 111,4375). Le DLL lit ces fiches (`AlundraScriptedMotion.cs:614-632`), donc aucun code ; mais les tests doivent comparer à l'oracle, pas à « la marche libre ».
5. Familles dont la fiche n'a pas d'attaque : l'herbe et les « Objet (transportable) » ont `AttackCount` 0, comme les haricots (le plan ne citait que les haricots). Ils sont gardés en verrou.
6. O-G-10 (« le spawner d'enregistrement refuse certains en-têtes ») s'explique par le test de zone de 0x8003A200-0x8003A25C : `[XMin..XMax] × [YMin..YMax]` de l'enregistrement doit contenir la case du héros (`+0x120/+0x124`). Avec une zone élargie par la harnais, les neuf familles de caisses/jarres/pierres/torches se créent. Ce n'est pas une propriété de `AttackCount`.
7. Numéros de ligne du DLL au HEAD : l'appel mode 0 est à `AlundraPlayerManager.cs:395` ; le verrou est `:317-335` ; la garde d'échelle `:386` ; le commentaire « NOT PORTED » des états d'échelle `:444`. Les destroy sont à `AlundraWorldProxy.cs:3267` (2 arguments) et `:3295` (1 argument, doc `:3290`).
8. `EntitySupport.BuildCollidables` (`EntitySupport.cs:44-59`, via `IsEligibleSubject :72-75`) exclut déjà `PlatformEntity != null`. Poser le lien sort donc l'objet des collidables sans code de plus (w1 du plan).
9. Comptes : 2 252 placements sur 242 maps dans `data-extracted` (le plan : 2 251). Écart d'une caisse ou jarre, non investigué. 18 des 30 maps de la chaîne ont des soulevables, pour 219 placements : conforme au plan.

## 4. Ce que la DLL a aujourd'hui (FACT, HEAD `2010ec3`)

- **Bouchons** :
  - `AlundraPlayerManager.cs:919` : `TryUseItem` (renvoie `TryUseItemResultForTests ?? 1`).
  - `:927` : `PlayerTryAction => 0`.
  - `:1271-1274` : `UpdatePlayerCarriedEntity` vide. Appelé avec 0 en `:395` et avec 2 en `:646` (nage).
- **Branche verrouillée** (`:317-335`) : n'appelle ni le mode 1 ni l'annulation d'objet.
- **Sites de `PlayerTryAction`** : présents en `:419` (Idle et Moving), `:438` (sauts), `:743` (0x29), `:787` (0x03), `:848` (0x2A). Absent pour l'échelle 0x0E et 0x35 (`:444`).
- **États du héros** : aucun cas pour 0x05, 0x06, 0x07, 0x0B, 0x0C, 0x2E, 0x2F, 0x30. 0x09 et 0x0A sont dans `IsGroundAttackGroup` et `IsAirAttackGroup` (`:1013`, `:1017`). Les chaînes d'animation exportées du héros existent (5→12, 6→47, 10→45, 11→48, 46→48, 9→0).
- **Champs** : `AlundraEntityScriptProxy.cs:88-92` (`PlatformEntity`, `CarriedEntity`, `RelativeWarpOffsetX/Y/Z`) ne sont jamais écrits ; ils sont clonés en `:2596-2600`. `CarriedEntity` n'est lu qu'en `AlundraPlayerManager.cs:386`.
- **Trou de nettoyage** : aucune des deux surcharges de `DestroyEntity` ne touche au porteur (l'étape (6) est un bouchon nommé). `RecycleDestroyedEntities` (`:3231`) libère la case et retire l'entité du monde sans effacer aucune référence.
  - Conséquence après g1a : un objet détruit par un autre chemin que C23 (opcode 0x2E, E1, pente 4, E2) laisserait `héros.CarriedEntity` pointer sur une entité retirée du monde. `PlayerTryAction` renverrait alors 1 chaque tick (0x0C/0x30) et l'échelle serait refusée : le héros serait coincé. W5 est obligatoire.
- **Registre** (`AlundraNativeSpriteRegistry.cs`) : aucune clé C23, C4, C70, C73, C62, E2, E12, E20, E21 n'est enregistrée. Les types de page valent `0x100 + indice`. Compte épinglé à 7 (`AlundraNativeSpriteRegistryTests.cs:51`).
- **Obstacle** : `AlundraMovementObstacleProbe.cs:48-49` passe `PickupKindMask` en drapeaux écartés (D-E19-28 et D-E19-46). `XCollisionEntity` (`AlundraEntityScriptProxy.cs:2411-2416`) n'est donc jamais un soulevable.
- **Motion** : `TickScriptedNpc` (`AlundraScriptedMotion.cs:165-170`) lit la fiche de `CurrentAnimationId`, pas celle de `TargetAnimationId`. La vitesse d'un objet lancé démarre donc un tick (une image) trop tard : c'est D-F5-5, f5 n'est pas faite.
- **Autres** :
  - `HasLogicalZ` existe (`:1370`), avec la loi d'e0k2 déjà confirmée sur la série de la caisse.
  - `SpawnDirections` (`:1269`), `GetNumberOfItem(state, id)` (`:2168`) et la passe de coups générique existent.
  - La clé de tri (`AlundraEntityDepthKey.cs`) documente l'absence de relèvement par un porteur.
  - La doc d'`EntityPickupKind` (`EntityFlags.cs:274-290`) est inversée pour les genres 2 et 3.
- **Hérité par la plateforme horizontale** (`AlundraScriptedMotion.cs:671-675`) : le héros ajoute `LastTickDeltaX/Y` de son porteur à son pas.

## 5. Données : où sont les soulevables (FACT, `S/census_*.py`, `S/lst/census_*`)

- **Prefabs** : 17 en-têtes soulevables dans `alundra-project/Data/sprite-records.json` (16 de genre 1, 1 de genre 3). Ce sont 17 prefabs dans `Entities/` :
  - Famille C23 / E2 (7) : Caisse en bois générique, Cruche générique, Cruche n°2, Herbe transportable, Bloc de glace transportable, Rocher tortue non ennemi, Pierre très lourde (genre 3).
  - Famille C4 (7) : Pierre générique, Tonneau générique, Tronc (transportable), Objet eau, lune, soleil, étoile. Les quatre « objets » ont E0. Les trois premiers ont E2.
  - Torche (C70 / E20).
  - Haricots de Jack (C73 / E21).
  - Bombe (C62 / D30 / E12).
- **Placements** : 2 252 sur 242 maps.

| Famille | Placements | Maps |
|---|---|---|
| Caisses et jarres génériques (C23) | 1 910 | 207 |
| Pierre et tonneau (C4/E2) | 103 | 50 |
| Torches | 57 | 10 |
| Troncs | 56 | 6 |
| Cruche n°2 | 42 | 20 |
| Objets (eau, lune, soleil, étoile) | 32 | 4 |
| Rochers tortue | 21 | 6 |
| Blocs de glace | 15 | 5 |
| Herbes | 8 | 3 |
| Pierre très lourde (sprite 234, genre 3) | 7 | 4 (maps 3, 4, 7, 11) |
| Bombe (sprite 2) | 1 | 1 (map 362) |

- **Chaîne d'histoire** : 18 des 30 cartes ont des soulevables (219 placements). Sans soulevable : 389, 476, 478, 391, 416, 162, 169, 10, 176, 135, 183, 185.
- **Types à enregistrer**, avec `type = 0x100 + indice de page` pour les cartes. Aucune autre en-tête (soulevable ou non) n'utilise ces codes, donc le registre par type suffit :
  - (C, 23) : 291, 292, 294, 297, 304, 438, plus le sprite commun 234.
  - (E, 2) : 290, 291, 292, 293, 294, 297, 304, 373, 438, plus 234.
  - (C, 4) : 290, 293, 363, 367, 368, 369, 373.
  - Torche : 393.
- **Plateformes** : les préfabs de plateformes ont `C = 0`. Leur mouvement vient des programmes d'événements, avec des fiches de vitesse (64 à 256).
  - « Plateforme flottante » grande et petite : 164 + 67 placements sur 68 + 23 maps.
  - « Chariot de chargement » : 354 placements sur 85 maps.
  - « Plateforme mobile toutes directions » et « Bloc à pousser_attaquer » : 0 placement (par signature d'en-tête).
  - 74 maps ont un soulevable et une plateforme. Candidats du plan avec leurs soulevables : 224 (23), 27 (15), 24 (10), 159 (10), 445 (9) ; 27 et 24 ont aussi des plateformes flottantes.
  - Quelles plateformes se déplacent **à l'horizontale d'elles-mêmes**, et à quel moment : UNKNOWN (non lu dans les événements).

## 6. Tests qui bougent (INFERENCE, aucune suite lancée)

- **`AlundraNativeSpriteRegistryTests.cs:51`** : compte épinglé à 7 → 8 à la phase C de g1a (clé (C, 23)) ; → 9 à g2b (clé (E, 2)). À réécrire par dérivation.
- **`AlundraLadderClimbTests.cs:660-669`** (`CaseSixGate_CarryingEntity_DoesNotEnterClimbing`) : met un `new Entity()` nu (sans proxy de jeu) dans `CarriedEntity` puis appelle `MovePlayer`. Après g1a, `UpdatePlayerCarriedEntity(0)` et le groupe d'états lisent ce porté. Risque de `NullReferenceException` si les nouveaux corps ne gardent pas ce cas. Le résultat attendu reste vrai : à l'état Idle, `PlayerTryAction` doit forcer 0x0C ou 0x30, jamais l'escalade.
- **Cas sans proxy de porteur** : `AlundraBeannoideOracleTests.cs:76,351-359` et `AlundraMurutaOracleTests.cs:73,429-437` (`PlatformEntity = new Entity { Name = "platform" }`) et `AlundraNpcCharacterControllerMoverTests.cs:2210` (le héros porte un « marin » en `PlatformEntity`). Les nouvelles branches (suivi, effacement du porteur) doivent tolérer un lien sans proxy.
- **Commentaires à corriger** quand g1a pose W5 : `AlundraBeannoideOracleTests.cs:18` et `AlundraDestroyEntityBreakEffectTests.cs:12` (« l'effacement du porteur est le bouchon d'E20.g »).
- **Liste des préréglages** : `AlundraTestSavesTests.cs:85-87` (`TheNinePresets_AreListed_InTheOrderOfTheTable`) bouge quand un préréglage `porter-m<N>` s'ajoute (avec g1b).
- **Pas de test de portage existant.** Aucun test ne nomme `PlayerTryAction`, `UpdatePlayerCarriedEntity`, `TryLift` ou `PutInHands`.
- **À g3 (D-E20-39)**, hors g1a et g2a : tout ce qui épingle le masque de D-E19-28 et D-E19-46 (`AlundraMovementObstacleProbeTests.cs:130`, `AlundraEntityContactArcTests.cs:480`, `AlundraDialogueOpcodesProductionTests.cs:382`), les arcs A5 et A5r de `AlundraShipCorridorArcTests`, ainsi que les 7 fichiers de test qui nomment « crate » (`AlundraHeroApexAndCeilingTests`, `AlundraLogicalZStepTests`, `AlundraAbsoluteZWritesTests`, `AlundraMovementObstacleProbeTests`, `AlundraLogicalZWorldTests`, `AlundraHeroObjectTopsTests`, `AlundraShipCorridorArcTests`). Les arcs de l'histoire sur les 18 cartes de la chaîne qui ont des soulevables sont à remesurer.
- **Rien d'attendu** dans les tests de chaînes d'animation du héros, de traces, ni dans `AlundraWarpDepartureTests` (hero frozen). Le test d'état « non porté » (`AlundraPlayerManagerTests.cs:96`) utilise 0x20, qui reste non porté.

## 7. Questions ouvertes

1. **(conséquence de D-E20-39)** Veut-on voir porter ou lancer quelque chose en jeu **avant** que caisses et jarres redeviennent solides ?
   - (A, défaut) Non : g1a et g2a restent invisibles jusqu'à g3 + g1b.
   - (B) Via h3 : bombe et haricots seraient les premiers objets portables. Demande h0, h1 et les gestionnaires C73/E21 et C62/D30/E12.
   - (C) Un geste de test (préréglage + touche qui met une caisse dans les mains), un écart de plus retiré à g3. L'auteur a refusé le détecteur de contact provisoire ; ceci s'en approche.
2. **Blessure, mort et case de sortie avec un objet** (sites 0x80031C64, 0x800327AC, 0x80032778, hors g1a) : en attendant c2b et E18, un héros touché garde la caisse dans les mains. Dans l'original, elle tombe de sa tête et se brise. Acceptable ?
3. **Dégâts d'un objet lancé** : confirmer que seules les cibles dont le programme D est porté (Beannoïde, Muruta) sont touchées avant E14.
4. **D-F5-5** : prendre la garde étroite dans g2a (« une entité marquée et lancée lit `TargetAnimationId` »), ou attendre f5 ? Défaut : garde étroite d'une ligne.
5. **Recette** : quelle carte ? Il faut un soulevable C23, une plateforme qui se déplace à l'horizontale **d'elle-même** et un dessus de 16 px, à l'arrivée sans verrou. Le T0 doit dépouiller les événements des cartes candidates.
6. **Changement de carte avec un objet** : l'original efface-t-il le porté au chargement ? La décompilation (`EntityManager.InitializeEntitySlots`) efface les 64 cases, le héros compris, et `ResetEntityState` (= 0x80031974) recrée le héros : INFERENCE, non lu dans le binaire. Côté DLL, il faut un test que le héros reconstruit à l'arrivée n'emporte pas `CarriedEntity`.
7. **Blocs poussables (O-E20-21)** : aucun placement du prefab « Bloc à pousser_attaquer » dans le corpus ; hors de g.

## 8. Tâches proposées, g1a puis g2a (cohérentes avec D-E20-39, D-E20-40, D-E20-41)

**Préambule — à reporter dans la révision du plan**
- Ordre interne : g1a → g2a → g2b → g3 (solidité) → g1b réduit à l'activation.
- g1b réduit à : `TryLift` et `PlayerTryAction`, l'ensemble câblé, les appels des états d'échelle (`:444`), le préréglage.
- Disparaissent : D-G-7, `LiftContactEntity` et les compteurs-témoins k2 à k4. Le contact `+0x130` est `XCollisionEntity`, qui revient avec le retrait du masque de g3.
- Pas de « poser » (D-E20-41) : la branche de Carré du groupe de portage est `ThrowCarried` seul.
- Recette (D-E20-40) : marche de 16 px, dessus d'un autre objet, plateforme horizontale. Aucun ascenseur.

**g1a — porter**
- **T0 (docs et annexe `docs/plan-e20-annexe/e20-g/`)**
  - Y mettre l'oracle copié, mes scripts, les vecteurs de référence et leurs empreintes.
  - Résultats déjà acquis : P-G-1 résolue (`+0x1D4 = 0` porté, ≠ 0 du lancer à la pose).
  - Restent à faire sur la DLL : P-G-2 à P-G-5, la liste prédite des tests recomptée, et le choix de la carte de recette.
- **T1 — phase A, fonctions pures** : `AlundraCarryRules` (pas, trois modes, tableau de transitions du groupe de portage), `PutInHands` (couture), classe C23 non enregistrée, compteur du verrou.
  - Tests rouges d'abord : (c1) à (c6) du plan.
  - Ajouts : (c7) les 850 vecteurs de `goldens_g1a.json` rejoués en C# (TryLift, PlayerTryAction, UpdatePlayerCarriedEntity, C23, DestroyEntity 1 argument, branche porteur) ; (c8) les 700 de `goldens_carry_group.json`.
- **T2 — tests de monde** (rouges avant la phase B), à 1, 2 et 3 ticks par image : (w1) à (w12) du plan. Ajouts :
  - (w13) départ de marche portée à pleine vitesse, accolé à la marche libre à rampe ;
  - (w14) un `CarriedEntity` sans proxy ne lève rien et laisse le héros en 0x0C ou 0x30 ;
  - (w15) un objet détruit hors C23 (opcode 0x2E, E1, pente 4) efface `CarriedEntity` au même tick ;
  - (w16) le héros reconstruit à l'arrivée n'emporte rien ;
  - (w17) une caisse portée ne marque aucun coup sur une cible de classe A qu'elle recouvre.
- **T3 — phase B** : W1 à W6 (corps du mode, appel du mode 1 dans le verrou, branche de suivi, groupe de portage et états 0x05 et 0x0B, effacement du porteur dans les deux `DestroyEntity`, clé de tri). Garde d'échelle déjà présente.
  - Mutants du plan, plus : décalage lu sur le triplet au lieu des positions, mode 2 qui efface `CarriedEntity`, effacement avec test d'identité.
- **T4 — phase C** : enregistrer (C, 23) pour les 7 types de §5. Épingle du registre 7 → 8 par dérivation.
- **T5** : ADR et deux verifiers frais.
- **Arrêts** : un test existant hors §6 bouge ; un diff du chemin du héros hors du groupe de portage ; une sonde contredit sa prédiction.

**g2a — lancer**
- **T0** : sondes P-G-2 (valeur de `BoxDirection` au lancer, attendue = direction du tick précédent), P-G-3, P-G-4, P-G-5. Question 4 de §7 tranchée.
- **T1 — phase A** : `ThrowCarried` pur.
  - Tests : (t1) position, quatre directions deux à deux, `t24` ∈ {1, 2} au sol et {2, 3} en l'air, animations 0x09 et 0x0A, garde `0x2000` ; (t2) regard du tick précédent contre regard de fin de tick.
  - Ajout : les vecteurs de lancer de `goldens_carry_group.json`.
- **T2 — tests de monde** : (v1) à (v9) du plan. Ajouts :
  - (v10) une caisse lancée touche une cible de classe A : `TouchingEntity`, `+0x1D0 = 0x19`, `HitCounter`, bascule en E ; avec un Beannoïde réel ;
  - (v11) elle ne touche jamais le héros ;
  - (v12) l'objet lancé de la mort dans l'eau n'est pas visé ici : f6a et O-E0K-2.
- **T3 — phase B** : la branche de Carré remplace le bouchon de g1a. Si la question 4 est « garde étroite », une ligne dans `TickScriptedNpc` pour les entités marquées (HasLogicalZ) et lancées ; sinon attendre f5.
- **T4** : ADR et verifiers. Mutants du plan, plus : table {0, 0x10, 8, 0x18} lue avec `+0x8C`, garde `0x2000` appliquée au saut.

## 9. Fichiers (dans `S/`)

- Listings : `lst/*.txt`.
- Modèles : `g1a_models.py`.
- Vecteurs : `vec_g1a.py`, `vec_families.py`, `orc/sweep_carry.py`, `orc/sweep_ladder.py`, `timelines.py`.
- Mutants : `mut_g1a.py`, `orc/mut_carry.py`. Sorties dans `lst/mut_g1a_final.txt`, `lst/mut_carry_final2.txt`.
- Recensements : `census_prefabs.py`, `census_maps.py`, `census_maps2.py`, `census_codes.py`, `census_platforms.py`. Sorties dans `lst/census_*`.
- Oracle : `orc/sheets_census.py`, `orc/thrown_damage.py`, `orc/carried_contact.py`, sorties dans `orc/out/`.
- Vecteurs de référence : `lst/goldens_g1a.json`, `lst/goldens_carry_group.json`.
- Empreintes : `MANIFEST.txt`.
- Pour rejouer, depuis une copie : `python -B vec_g1a.py 4000`, `python -B vec_families.py 5000`, `python -B -I sweep_carry.py 12000` et `python -B -I sweep_ladder.py 4000` depuis `orc/`, `python -B timelines.py`, `python -B mut_g1a.py 600` et `python -B mut_carry.py 2500` depuis `orc/`.