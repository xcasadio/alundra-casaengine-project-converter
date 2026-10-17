# Critique de disc-e20g3 : faits du binaire re-dérivés pour E20.g3 et E20.g4

Lecture seule. Rien n'a été écrit dans le dépôt, les sous-modules, `alundra-project/` ni `data-extracted/`. Le `git status` montre ` m CasaEngineMonogame` et ` m alundra-datas-analyser`, déjà signalés par la découverte ; je n'y ai pas touché. Le binaire a été relu et son SHA-1 vérifié (`a705f8da…`).

Mon dossier est `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20g3/critic/`. Il contient les scripts, `lst/`, `out/` et `MANIFEST.txt` (SHA-1 de chaque fichier). **`report.md` n'est pas écrit** : la règle de l'environnement interdit aux sous-agents d'écrire un fichier rapport. Ce message est le rapport complet.

**Outils.**
- `cdis.py` : mon désassembleur capstone.
- `my392.py` : un harnais indépendant de `lib392`, sur la machine d'oracle recopiée depuis `docs/plan-e20-annexe/e20-g1a/work` vers `critic/g1a`. `diff -rq` montre qu'elle est identique à la copie de la découverte.
- Le harnais lit les cellules de `data-extracted/data/map_392.json`. Elles sont identiques au tilemap exporté : 3120 cellules, 0 écart (`out/w0_smoke.txt`).
- Il fait naître **les 12 enregistrements dans l'ordre du fichier** (slots 1 à 12).

---

## Confirmé

**1. Liste des collidables** (`lst/list_pass.txt`).
- Avec `v1 = slot + 0x1CC`, les tests sont : `+0x6C & 0x80` (`0x8003858C`, saute si nul), `+0xB4 & 0x80` (`0x800385A0`, saute si non nul) et `+0x28` (`0x800385AC`, saute si non nul).
- Le statut n'est lu que pour la liste active (`0x80038548`-`0x8003856C` : `(statut-2) < 2`, puis `+0x20 == 0`) et pour celle des frappables (`0x800385D0`-`0x800385F4`).
- La liste couvre les slots 0 à `[0x80134360]` inclus, dans l'ordre des slots, de pas `0x294`.
- Aucun test de genre ni de statut pour les collidables.

**2. `FindEntityCollisionCandidate` `0x80036F34`** (`lst/feccand.txt`).
- Exception du héros pendant une transition : `0x80036F3C`-`0x80036F64`.
- Porte du sujet : le même triple (`0x80036F74`, `0x80036F88`, `0x80036F94`).
- Boucle sur `[0x80127D2C]` entrées de `0x80127C28`. Le seul saut est `beq a0, a2` en `0x80036FE8`.
- X, Y, Z sont semi-ouverts : un delta négatif se compare à la taille+1 du candidat, un delta positif à celle du sujet. Le premier candidat qui recouvre est renvoyé. Aucun drapeau du candidat n'est lu.
- `+0x1F0 = (taille << 16) - 1` : `0x14FFFF` pour 21 px (`out/w0_smoke.txt`).
- Seuls appelants : `0x800378C4` et `0x80037908`. Le seul appelant de `0x80037730` est `0x80037F00` (`scanimm.py`).

**3. Déplacement XY** (`lst/movexy_moveentity.txt`).
- Le test d'entité (`0x80037908`) passe avant le terrain. Le `jalr` du terrain (`0x80037928`) n'a lieu que si `s4 == 0`.
- Quand une entité bloque, il n'y a pas de glissement : `0x800379D0 bnez s4` mène à `0x80037D54`, qui écrit `+0x13C = 1`.
- Seule la boîte d'essai est testée : aucune règle de sortie.
- Dans `MoveEntity`, Z d'abord (`0x80037EF8`), puis XY (`0x80037F00`), puis `sw +0x130` en `0x80037F08`. La branche « porté » met `+0x130` à 0 (`0x80037E80`).

**4. Ordre du tick `0x8003B388`.**
- Passes : `0x80038634`, `0x800386D0` (`MovePlayer` en `0x800386EC`), `0x80038998`, `0x800384F4`, `0x80038E18`, `0x80038364`, `0x80038E84`, `0x80039300`.
- Les compteurs remettent à 0 `+0x224`, `+0x12C` et `+0x220` (`0x800389D8`-`0x800389E0`), **pas `+0x130`** (`lst/physpass_counters.txt`).
- Le `+0x130` que lit `PlayerTryAction` est donc celui de la physique du tick précédent.

**5. `PlayerTryAction` `0x8002EEAC` et `TryLift` `0x8002EDBC`** (`lst/trylift_pta.txt`).
- Lecture de `+0x130` en `0x8002EF08`. Front de Carré : `lhu 0x80126F2A & 0x80` (`0x8002EF18`-`0x8002EF24`).
- `TryLift(contact)` est renvoyé même nul (`0x8002EF2C`).
- Genre : `(+0x6C & 0x600) >> 9` (`0x8002EDD4`). Genre 1 : soulève. Genre 0 : renvoie 0. Genres 2 et 3 : `jal 0x8004E428` avec `a0 = 0x3B` (`0x8002EDF8`).
- Aucun test de distance ni de regard.
- `TryLift` n'est appelé qu'en `0x8002EF2C` et `0x8002EF9C`. `PlayerTryAction` est appelé de 6 sites (`0x80031E50` … `0x800321E4`).
- Mesure (`out/w4_contact.txt`, 79 départs) : un Carré le tick où le héros devient affleurant ne soulève **jamais** (0/79). Un tick plus tard, il soulève 78 fois sur 79.

**6. Libération** (`ctab.py` donne les entrées de la table C ; `lst/release.txt`, `lst/c4.txt`, `lst/c23.txt`, `lst/c70.txt`).
- C23 = `0x8006B848`. Il écrit `(flags | 0x30) & ~0x80` (`0x8006B8A4`-`0x8006B8BC`) pour **tout** `+0x24 != 0`.
- C70 = `0x8007B7B0`. Il écrit `| 0x34`, puis `& ~0x80` (`0x8007B80C`-`0x8007B824`).
- C4 = `0x80066984`. Il n'efface `0x80` que si `+0x24 == -1` **et** `+0x80 == 2`. Le `-0x81` est dans le délai de saut en `0x800669D0` ; le store `0x800669E8` n'a lieu que dans ce cas.
- C73 = `0x8007BB30`. Il ne fait que `| 0x10` (`0x8007BB6C`).
- E2 `0x8007ED30` ne touche que `0x40` et `0x30` (`0x8007ED7C`-`0x8007ED8C`), puis appelle `DestroyEntity`.
- Balayage de toute l'image (`out/flagwrites.txt`, `owner.py`) : les seuls OU de `0x80` dans `+0x6C` sont dans C68 (`0x8007B670`) et C71 (`0x8007B974`), qui ne sont pas des familles de soulevables.

**7. Entité détruite.**
- `DestroyEntity` `0x8003A59C` met le statut à 4 (`0x8003A5BC`) sans toucher aux drapeaux.
- La passe `0x80038634` du tick suivant recopie le gabarit `0x80134368` sur les slots de statut 4.
- L'entité reste donc collidable pendant la physique du tick où elle est détruite.
- L'écart DLL `EntitySupport.cs:53-55` (filtre `FlagToDestroy`) est confirmé : une image de décalage.

**8. Les six animations du héros** (`heroanim_raw.py`, `out/heroanim_raw.txt`, lus dans `DATAS.BIN` brut).
- Le mot 0 de l'en-tête vaut `0x6FBC` (pointeur de la table d'animations) ; 94 fiches de 14 octets.
- L'octet `0xD` a le bit `0x80` pour les ids 32 (`0xD0`), 33 (`0xD0`), 34 (`0xD4`), 35 (`0xD1`), 36 (`0xD0`) et 39 (`0xD0`).
- Le champ JSON `Acceleration` égale l'octet `0xD` sur les 94 fiches.
- Index de la fiche : `+0x88 × 14` (`0x80038B18`-`0x80038B28`) ; `+0xB4` vient de l'octet `0xD` (`0x80038B68`, `0x80038B70`).
- L'état du héros est `+0x88`, dispatché par la table `0x80022D3C` (`0x80031D84`-`0x80031DAC`). Gestionnaires :
  - `0x20` et `0x24` : `0x8003251C` ;
  - `0x21` : `0x80032680` ;
  - `0x22` et `0x23` : `0x800324B8` ;
  - `0x27` : `0x80032758`, le gestionnaire par défaut (`out/jt.txt`).
- Le héros n'écrit jamais son bit `0x80`. Dans `MovePlayer`, seul `0x100` est modifié (`0x80031C38`, `0x800322A4`). Aucun store absolu sur `0x80127D9C` dans l'image.
- Les mêmes portes existent dans `0x80036BFC`, `0x80036D94` et `0x80037F28` (`lst/zdown_up.txt`, `lst/ground.txt`).

**9. Carte 392.**
- Géométrie re-dérivée : boîte du héros Y = `[PosY-7, PosY+8)`. Il faut `PosY ≥ 295` (contre rec4/rec8, `[273, 288)`) et `PosY ≤ 297` (contre rec6/rec10, `[305, 320)`).
- Mesure (`out/w1_window.txt`), avec 12 caisses :
  - **saut depuis l'est** : 297,0 réussit 15 fois sur 19 ; 297,0000153 échoue 19 fois sur 19 ;
  - **soulever puis passer depuis l'est** : passe de 295,0 à 297,0 ; à 297,0000153, le héros est bloqué par rec6 à x = 657.
- **Depuis l'ouest, dans la vraie carte** (`out/w6_westedge.txt`, départ (600, y), hors de rec9) : 294,9999847 et 297,0000153 font 0 sur 12 ; 295,0, 296,0 et 297,0 font 12 sur 12.
- Contact au tick 27 depuis (720, 296), Carré au tick 28 : rec5 portée. C'est reproduit.
- `a3_fullroute.py` relancé depuis une copie : sortie identique au bit près (SHA-1 `de221c8f…`, `out/a3_fullroute.txt`).
- La porte (colonnes 27-28, rangées 18 à 20) est le seul passage au niveau des cellules (`out/m392_cells.txt`).

---

## Réfuté ou corrigé

**R1. Sens du retour de `0x80037730` (A2.2, « candidat de l'avant-dernière tentative ») : réfuté pour un mover au sol.**
- `0x34(sp)` n'est écrit qu'en `0x800377CC` (avec `s4`) et mis à 0 en `0x80037DC8` (force nulle). `s4` n'est écrit qu'en `0x80037910`.
- La branche de marche au sol (`0x8003783C`-`0x800378CC`) est prise quand `+0x6C & 0x100`, `+0xB8 == 0`, et que le terrain sous la boîte est à moins de `0x30003`. Le terrain vient de `0x800370C4`, qui ne lit que le terrain (`lst/h_370c4.txt`).
- Quand cette branche ne trouve pas de candidat, elle saute en `0x8003791C` **sans écrire `s4`** : la valeur reste collée d'une tentative à l'autre.
- Mesure `out/w7_return.txt` : le modèle « collant » donne 160 sur 160 ; le modèle « avant-dernière tentative » donne 127 sur 160.
- **Règle exacte au sol :** le contact est l'entité qui a refusé le pas complet du tick (le dernier candidat trouvé). Il vaut 0 si le premier pas complet est accepté (retour direct en `0x80037D68`), même quand ce pas finit exactement affleurant, et 0 sans force.
- En l'air, ou debout sur une caisse, `s4` est réécrit à chaque tentative (INFERENCE tirée de la condition de branche).

**R2. Règle du Carré.**
- « Un tick après un tick bloqué » n'est exact que si « bloqué » veut dire « pas complet refusé ».
- Cas limite (`out/w4_contact.txt`, x0 = 702,09375) : le héros devient affleurant au tick 19 avec le pas complet accepté, donc contact 0. Carré au tick 20 : épée (anim 16). Le soulèvement n'est possible qu'au tick 21.

**R3. Les mesures du côté ouest et deux affirmations de route reposent sur un monde sans rec9.**
- Fichiers concernés :
  - `lib392.py:51` : `rec_ids` par défaut `(4, 5, 6, 7, 8, 10, 11)` ;
  - `a3_edges.py:13`, `a3_sweep.py:19`, `a3_jumpx.py:11`, `a3_r3b.py:8` : appels sans `rec_ids`, donc avec ce défaut ;
  - `a3_routes.py:41` : `rec_ids` explicite, sans rec9.
- Or le départ ouest (560, y) est **dans** l'empreinte de rec9 (X `[566, 587)`, Y `[281, 296)`, `out/w0_smoke.txt`). Dans la vraie carte, le héros y démarre perché sur rec9 (`out/w2_trace_west_294_5.txt`).
- Conséquences :
  - « R2 … sol à l'ouest (x = 562) » (rapport :153) et « x ≤ 562 au tick 124 » (:160) sont faux dans la vraie carte. En marchant vers l'ouest à y ≈ 296, le héros s'arrête à **x = 597,0 contre rec9** (`out/w1_window.txt`, LIFT : contact final rec9 ; route complète : 597,77 au tick 130).
  - La plage d'appel ouest « 582,1 à 615,0 » tombe en partie dans rec9.
  - Les bords mesurés côté ouest restent justes : je les ai remesurés dans la vraie carte (`out/w6_westedge.txt`).

**R4. « Le héros passe par-dessus rec5 si et seulement si PosY ∈ [295 ; 297] » n'est vrai que pour la barricade de départ, depuis le sol.**
- (a) **Soulever en l'air.** `PlayerTryAction` est appelé depuis les états de saut (`0x80031E98`). À z ≥ 80, le contact est la caisse **du haut**, et Carré la soulève en plein vol : état `0x2C` vers `0x0B`, rec10 portée, à y = 298, 300 et 312 (`out/w5_airlift.txt`). Plus bas, c'est la caisse du bas : rec5 ou rec6.
- (b) **D'ouest en est, depuis le dessus de rec9**, le héros saute sur une pile de 2 et traverse : supports rec9, puis rec8, puis rec5, x final 744,4 (`out/w2_trace_west_294_5.txt`). À y = 298, c'est par rec10 (`out/w2_trace_west_298.txt`).
- INFERENCE, non jouée : si rec10 est retirée, la fenêtre de saut au-dessus de rec5 et rec6 devient [295 ; 313].

**R5. « Le mur ramène y < 295 à 295 » et « le mur nord cale à 295,000 » sont imprécis** (`out/w3_door.txt`).
- En entrant dans la porte par l'est avec seulement GAUCHE, depuis y0 ∈ [286 ; 295] : 145 départs sur 145 finissent avec `PosY = 295 + ((y0 − 295) mod 0,5)`, soit dans [295 ; 295,4375]. C'est toujours dans la fenêtre, mais pas toujours 295,0.
- En tenant HAUT dans la porte : le héros se cale à 295,000, puis glisse vers l'est et ressort de la porte. Avec y0 = 300, après 30 ticks : x = 706,5 et y = 288,5.

**R6. A2.3 point 3 omet que la boucle des supports ne lit aucun bouton.**
- Aucun accès à `0x80126F2A` entre `0x8002EF3C` et `0x8002EFA4`.
- Le plan le dit déjà (`docs/plan-e20-heros.md:6304`).

---

## Manques

1. **Décalage d'un tick sur `+0xB4`.** La passe des listes (`0x800384F4`) passe avant celle des animations (`0x80038E18`). Le seul écrivain de `+0xB4` à chaque tick est `0x80038B70` ; les autres sont `0x80039EA8` (`InitializeEntity`) et `0x800622BC` (C89). L'appartenance à la liste suit donc un changement d'animation avec un tick de retard. La porte du sujet, lue pendant la physique, le suit le jour même.
2. **Collidable non actif** (statut hors {2, 3} ou `+0x20 != 0`) : il est dans la liste, mais sa boîte n'est recalculée que pour la liste active (`0x80038394`-`0x800383E0`) et son `+0x130` n'est pas réécrit. La DLL recalcule `Pos + Mod` (`EntityCollision.cs:62-64`). INFERENCE : aucun cas de caisse connu.
3. **Soulèvement sans bouton** d'une entité dont `+0x12C == héros`. `TryLift` passe alors directement en `0x0C` ou `0x30` (`0x8002EE18`-`0x8002EE34`). INFERENCE : c'est ainsi qu'on rattrape un C4 lancé, puisqu'il garde `0x80`. Un C23 lancé est hors liste, donc il n'a jamais de support.
4. **Branche de marche.** Le test d'entité se fait à la hauteur relevée, et le test à la hauteur d'origine est sauté. Sur sol plat, le résultat est identique.
5. **Exception de transition.** La DLL `IsEligibleSubject` (`EntitySupport.cs:73-76`) n'a pas l'exception de transition du héros (`0x80036F3C`). Mineur.

## Inconnus

- Les noms des états `0x20`-`0x24` et `0x27`. Écrivains trouvés : `0x800324F4`, `0x80032504`, `0x80032510`, `0x8003435C` (`0x20`), `0x80034510` (`0x21`). Celui de `0x27` n'a pas été trouvé.
- Qui écrit `+0x24 = -1`, le seul cas où C4 efface `0x80`.
- La route complète par soulèvement en l'air : non jouée de bout en bout.
- Le contact en l'air : tiré du code seulement.
- Si l'on atteint le dessus de rec9 en sautant depuis le sol ouest : INFERENCE oui, comme pour rec5. Mes traces partent sur rec9 artificiellement.
- Si l'ordre de la liste DLL suit l'ordre des slots : `WorldProxy.cs:3687-3699` non relu.

---

## Conséquences pour les tranches g3 et g4

**g3**
1. Le binaire n'a aucune règle propre aux soulevables. Retirer le masque de `MovementObstacleProbe.cs:50-51` suffit côté obstacle.
2. **Contact** : il doit valoir l'entité qui a raccourci ou annulé le pas du tick, et null si le pas complet est accepté, même affleurant, ou sans force. Le commentaire DLL `EntityScriptProxy.cs:2420-2423` dit exactement cela. À épingler par un test sur le cas affleurant (R1, R2).
3. **Soulèvement** : il faut le contact du tick précédent et un front de Carré. Il est aussi possible **depuis les états de saut** (vers l'état `0x0B`). S'y ajoute la boucle des supports sans bouton. Tests possibles : Carré le tick de l'affleurement donne l'épée ; un tick plus tard, le soulèvement.
4. **Identité du contact** : le premier de la liste dans l'ordre des slots, à la hauteur testée. En l'air à z ≥ 80, c'est la caisse du haut. L'ordre de la liste DLL doit être l'ordre des enregistrements.
5. **Après la libération** : les caisses et cruches sont des C23, elles ne bloquent plus en vol. C4 et C73 restent solides. E2 ne restaure jamais `0x80`.
6. Deux écarts d'un tick à documenter : l'entité détruite (`EntitySupport.cs:55`) et `+0xB4`.

**g4**
1. La fenêtre de 2,0 px [295,0 ; 297,0] vaut pour le saut et pour le soulèvement, confirmée des deux côtés dans la vraie carte.
2. Les recettes et tests doivent charger **les 12 enregistrements**. Rec9 arrête le héros à x = 597 et impose de descendre au-delà de y 303 avant d'aller vers l'ouest.
3. **Critère d'acceptation utile** : une entrée dans la porte depuis le nord avec GAUCHE seulement donne `PosY ∈ [295 ; 295,5)`. C'est un test du glissement de mur de la DLL. Une entrée avec y0 > 297 ne passe pas.
4. **Ne pas faire du « si et seulement si » un critère.** Le critère (b) de la découverte (« à y = 298 … le passage reste fermé ») n'est vrai que si l'on interdit le soulèvement en l'air : rec10 se soulève en plein saut à y = 298 (`out/w5_airlift.txt`).
5. Valeurs de recette reproduites avec 12 caisses : contact au tick 27 et soulèvement au tick 28 depuis (720, 296), arrêt à x = 657,0, route complète identique.