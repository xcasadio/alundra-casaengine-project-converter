# E20.e0c revision 3: read-only reading of 2026-10-22, kept verbatim

The section « 2. Texte de plan proposé » is left out: it is in `docs/plan-e20-heros.md` §1.26. The scratch folder it names is
the session scratchpad `prep-e0c/`, whose scripts and outputs are copied next to this file (`README.md`).

**Préparation en lecture seule de la révision 3 d'E20.e0c (HEAD `1d0b6953`)**

Rien n'a été modifié, indexé ni commité dans le dépôt. Aucune construction ni aucun test de la DLL n'a été lancé (la suite n'a pas été rejouée). Mes scripts et leurs sorties sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/prep-e0c/`.

Deux faits changent la tranche.
- Seuls 59 des 152 objets posés exécutent C255 : les 93 autres ont un script de carte au créneau C.
- Un objet lâché ne passe jamais par l'état 2 de C255 et n'écrit jamais le registre `[0x80149CBC]`. Le texte de la révision 2 est faux sur ce point.

# 1. Faits vérifiés

## 1.1 Méthode

- `ALUN_CD.EXE` (France) a le SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Je l'ai désassemblé avec capstone.
- J'ai rejoué le vrai code de C255 dans l'interpréteur MIPS de l'annexe, copié dans mon dossier.
- Harnais `t_c255.py`, 1 500 vecteurs : 0 écart, couverture 381/381. Les modèles de l'annexe reproduisent donc encore le binaire, posé et clé compris.
- Huit scénarios dirigés sur le vrai code : `run_c255.py` et `run_c255_out.txt`.
- Recensement des objets posés fait deux fois : sur `data-extracted/` (`census_placed.py`) et sur le projet exporté, ce que lit la DLL (`census_exported.py`). Les deux donnent les mêmes chiffres.

## 1.2 Binaire : la branche « posé » (`+0x274 == 0`) et ses sites

**A255** (`0x80061BD4`)
- `+0x274 == 1` : retour immédiat, c'est le rôle de l'écriture du lâcher.
- Type 30 (le coffre) : `+0x88 = 1` si `+0x3C == 0`, `0x80061BE0-0x80061C04`.
- `+0x274 == 2` : `0x80061C20-0x80061C58`.
- Posé : `0x80061C60-0x80061D00`.
  - `+0x6C &= ~0x80`, écrit avant tout test.
  - Si `+0x40 != 0` : bit du drapeau bas, destruction légère ; bit haut, suite.
  - Puis `+0x27C = 0`, `+0x280 = -1`, `+0x284 = 0`, `+0x278 = +0x40`.
- Les 152 objets posés ont `_10 = 0` et `Contents = 0`. La branche à drapeau n'est pas atteinte par les données.
- Registre : A255 couvre 88 types (30 et 31 à 118 sauf 72). C255 couvre 87 types (31 à 118 sauf 72). F255 (`0x8007FC88`) ne couvre que le type 30. Lu dans `map_alundra.json`.

**C255** (`0x8007C174`), branche `+0x274 != 2` à `0x8007C39C`
- État 0 : si `+0xB8 > 0`, attendre ; sinon `+0x27C = 1`. Le même tick passe de 0 à 1 et le ramassage est au tick suivant.
- État 1, `0x8007C488-0x8007C6EC`, dans cet ordre :
  1. Rien si un dialogue est ouvert (`0x80045004` lit `[0x80152F08] & 4`).
  2. Recouvrement sur 3 axes, `ModdedPos` contre celle du héros (`0x80127D30`), tailles + 1.
  3. `GiveItem(héros, objet)` (`0x80033F00`).
  4. Tampon de texte `0x80191268` : nom (mot 0 de la table `0x800C5F7C`, ETC `0x200 + objet`) puis ETC `0x45`, soit « gagné! ». Message toujours construit pour un posé. Pour un lâché, seulement si `GiveItem` rend ≠ 0, ce qui n'arrive pour aucun objet du butin.
  5. `SetGameFlag(+0x280)`. Pour un posé, `+0x280 = -1`, donc écriture parasite hors banque : bit 31 du mot `0xFFC` de la banque des drapeaux temporaires (`0x801EBA40`).
  6. Octet 5 de la table ≠ 0 (sons 52, 53, 54 ; `0x8007C6BC`) : son, message ouvert en mode 0 si le tampon est non vide, puis `+0x27C += 2`. Le ramassage passe donc de l'état 1 à l'état 3.
  7. Octet 5 == 0, objet-clé (`0x8007C648-0x8007C694`) : `+0x27C += 1`, `Block(soi)` (`0x8003AD30`), `0x80049B7C(0)` (arrêt de la BGM, `InitializeBgm`), piste CD 11 (`0x8005A9E0(0xB)`), message en mode 1, puis `0x8004507C(4)`, masque de fermeture 4 (fermeture par script seule).
- État 2, `0x8007C6F4`, atteint uniquement par un objet-clé :
  - `CdBusy()` (`0x8005A7D4`) vrai : attendre.
  - Sinon `+0x27C = 3`, masque `[0x80149CBC] = 3`, `0x80049AF4()` (`StopAllSound`, qui relance la BGM).
- État 3, `0x8007C728` : attendre la fermeture du dialogue, puis `Unblock(soi)` (`0x8003ADAC`) et destruction légère (`0x8003A774`).
- Branche morte : avec `[0x80029BCC] = 1` dans l'image, la garde est toujours vraie. Le côté faux (`0x8007C69C`) ajoute ETC `0x46` (« Zugyadodoon (XA) ») et n'est jamais pris.

**Sites vérifiés**
- `0x800450F0` : ouvreur de message. Le mode 1 fait `[0x800DC4B8] |= 0x10`, les autres modes `|= 8`. Il remet le masque à 3 (`0x80045564`) et joue le son 6. Il ne refuse jamais : son garde `0x80047F94(0)` rend 1.
- `0x8005A9E0` : ne fait rien si `[0x800A82B0] == 0`. Cette variable vaut 1 après l'initialisation du CD (`0x8004E85C`). Sinon elle lance la piste (entrée 11 de la table `0x800C4818` : `0x800C489C`, départ 2560, canal 3, longueur 181) et pose `[0x800C480C] = 2`. `CdBusy` lit ce mot ; `0x8005A7C0` et les rappels CD l'effacent.
- Côté DLL : `0xB9` est ignoré et `0xBA` rend 0 (`Alundra/Events/EventProgramRunner.cs:1433-1436`). `CdBusy` faux en bouchon est cohérent avec cela.
- `Block(a0)` (`0x8003AD30`) pose `+0x20 = a0` sur toutes les entités d'état 2 ou 3, soi exclu, déjà bloquées exclues, héros compris. `Unblock(a0)` (`0x8003ADAC`) remet à 0 celles dont `+0x20 == a0`.
- Effet du `+0x20 != 0` :
  - Le choix du programme écrit `+0x228 = -1` (`0x8003871C-0x80038728`) : aucun programme ne tourne.
  - L'entité sort de la liste active (`0x80038560-0x80038570`), lue par la physique `0x80038364`, l'avance d'animation `0x80038E18` et la passe de coups `0x80039300`.
  - Elle reste dans la liste des visibles (`0x800385E8`) : l'entité bloquée reste dessinée.

**GiveItem** : table `0x800230D4`, corps `0x80033F40-0x800340CC`
- Retour : `octet 4 == 0`.
- « Inventaire d'abord » (`0x80033EC8`) pour `0x24`, `0x25`, `0x26`, `0x27`, `0x29`. Compte avant < compte après : l'objet entre, sinon il est utilisé sur place.
- Corps : `0x24` herbe `0x80032E50` ; `0x25` Potion L `0x80033274` (PV = PVmax, 8 anneaux, aucun tirage) ; `0x27` Breuvage `0x80033A2C` ; `0x29` Potion S `0x800330FC` (`PV += max(10, PVmax/2)`, 4 étincelles, 8 tirages) ; `0x26` et `0x51` `0x800335AC` (+1 PM, 4 étincelles) ; `0x52` `0x800336F0` (PM = PMmax, 8 anneaux d'animation 2, aucun tirage) ; `0x53` `0x800333AC` ; `0x45`-`0x48` argent ; `0x54`-`0x56` cœurs ; le reste ajouté à l'inventaire par `0x8004E530`.
- Ces corps sont aussi ceux des départs d'objets d'h1. L'annexe `docs/plan-e20-annexe/e20-h1/disc/oracle/` les a déjà modélisés, 0 écart.

**Source unique du coffre** : F255 (`0x8007FC88`) fait naître l'objet avec `sw 2, 0x274` et `+0x280 = le coffre`. Rien d'autre n'écrit 2 dans `+0x274` pour un objet de type 31 à 118.

**Textes** (`alundra-project/Dialogues/Etc.dialogue`, `line_texts`) : ETC `0x45` = « gagné! » (`Etc_0069`). Les noms sont à `Etc_(0x200 + objet)` (par exemple « Epée sacrée »). Les lignes compilées sont rognées en fin de ligne. L'espace éventuel du binaire entre le nom et « gagné! » reste à lire en T0.

## 1.3 Corrections au texte de la révision 2

1. **État 2 et registre.**
   - La révision 2 faisait passer un objet lâché par l'état 2 et lui faisait écrire `[0x80149CBC]` (Faits, T0 (f), (w8), Q-E0C-5).
   - Tous les objets du butin ont un octet 5 non nul : ils vont de l'état 1 à l'état 3 sans état 2, et n'écrivent jamais le registre.
   - Rejoué sur le vrai code : cœur lâché, tick 0 `GiveItem, SetGameFlag(0), sound(52)`, tick 1 `Unblock(soi), DestroyLight`.
   - Le registre n'est écrit que par un objet-clé posé : 4 au ramassage, 3 à l'état 2.
2. **« 151 objets posés servis par C255 »** : faux. Au créneau C, `RunPickedEvent` (`Alundra/Entities/EntityScriptProxy.cs:1762`) et `RunSpriteEvent` (`Alundra/Events/EventProgramRunner.cs:296-307`) donnent la main au script de carte si `ProgramIndexes[slot] & 0x7F != 0`.
   - 152 objets posés sur 53 types (la révision 2 disait 151).
   - 59 ont A et C natifs : C255 les sert.
   - 36 ont A natif et C script : seul A255 les touche.
   - 57 ont A et C scripts : ni A255 ni C255.
3. Le chiffre « 7 clés du registre de production » se dérive (`NativeSpriteRegistry.cs:36-43`) : 7 devient 9 avec (A, 255) et (C, 255). Inchangé.

## 1.4 Données : les 152 objets posés (global, types 31 à 118 sauf 72)

- 152 objets, 53 types, `_10 = 0` et `Contents = 0` pour tous.
- 52 sont à apparition automatique (bit `0x40` de `SpriteDirection`), 100 n'apparaissent que forcés par script.
- Octet 5 de la table : 101 objets-clés (octet 5 == 0), 40 au son 52, 9 au son 53, 2 au son 54.
- Parmi les 59 natifs :

| | Débug zone (carte 11) | 24 autres cartes |
|---|---|---|
| Total | 31, tous à apparition automatique | 28, tous « forcés » |
| Objets-clés | 22 | 5 : 2 vases de vie (cartes 17 et 439), 2 emblèmes de topaze (17 et 439), 1 Pierre de Zolist (432) |
| Son 52 | 7 : Breuvage, Herbe, Potion L, Potion S, Extrait, Armure brisée, Clé | 14 : 10 Cœurs moyens, 3 Herbes, 1 Potion L |
| Son 53 | 0 | 9 : 8 fois « 5 Gildas » et 1 fois « 10 Gildas » |
| Son 54 | 2 : Petit et Grand cristal | 0 |

- Objets à soin sur place (inventaire plein), tous les posés : 26 objets, dont 9 natifs.
- Vases de vie (`0x53`) : 16 posés, 2 natifs.
- Coffres : 518, dont 21 à `Contents` 0 et 396 à drapeau. Un A255 inscrit sur le type 30 changerait l'aspect des coffres vides ou ouverts : hors tranche.

## 1.5 DLL au HEAD `1d0b6953` (fichier:ligne)

**Lâcher et apparition**
- Bouchon du lâcher : `Alundra/Maps/WorldProxy.cs:3301-3303`, doc `:3293`. Destruction légère : `:3329` et `Alundra/Maps/IEntityWorldContext.cs:90`.
- Apparition par indice : `IEntityWorldContext.cs:81`, `WorldProxy.cs:3488` et `:3601`. Z + 1 à `:3549`. Refus d'un monde nu à `:3498-3500`. Marqueurs de naissance à `:3570-3575`.
- Tirage à la création : `Alundra/Items/LootRoll.cs:54`, `:80`, `:111` ; `WorldProxy.cs:1119`, `:3440`, `:3566`, `RollLootAtCreation` à `:3469`.

**Champs de l'entité** (`EntityScriptProxy.cs`)
- `BlockedByEntity` à `:99`.
- `ContentsItemId` et `ContentsGameFlag` à `:106-107`.
- `ForceZ` à `:338`, `IsOnGround` à `:417`, `DamagedTickCounter` à `:483`.
- `ModdedPosX/Y/Z` à `:486`, `Width/Height/Depth` à `:488`.
- `Bytes` (`+0x274`, `byte[4]`) à `:1226`, `DelayOrAngleOrEntityId` (`+0x278`) à `:1228`, `ItemState` (`+0x27C`) à `:1229`, `AIValues` (`short[10]`, `+0x280`) à `:1230`.
- `ModdedPos` n'est écrit que par `Alundra/Entities/Spawning/EntitySpawnFactory.cs:825-827` et la copie `:2684-2686`. Aucun lecteur, et rien ne le rafraîchit (héros compris).

**Blocage** : le champ existe, il est lu mais jamais écrit hors tests.
- Lecteurs : `EntityScriptProxy.cs:1680` (choix du programme), `WorldProxy.cs:3062` et `:3072` (liste des coups), `Alundra/Hero/PlayerManager.cs:321` et `:1424`, `EventProgramRunner.cs:2644`.
- Non lu par l'horloge d'animation ni la physique (`WorldProxy.cs:2973` et `:2979`, `RunEntityBlock` `:2905-2990`).
- Aucun `Block` ni `Unblock`.

**Dialogue**
- `Alundra/Dialogues/DialogueDirector.cs:48` (masque par défaut 3), `:107` (`IsOpen`), `:220` (`Open(asset, node, mode)`), `:236-253` (mode 1 : `MessageBox` 0x10, sinon `MenuOpen` 0x08), `:385` (`SetCloseMask`).
- `Alundra/Dialogues/EtcStringTable.cs:122`, `:132`, `:143`. Précédent complet : `Alundra/Ai/Objects/SaveBook.cs:248` et `:261`.
- Manque : ouvrir une boîte sur un texte composé (nom + « gagné! »). `Open` exige un nœud Yarn.

**Portail et tick**
- Portail du monde : `Alundra/Session/GameState.cs:82`, `:85`, `:99` (`GameplayBlockedMask = MenuOpen | 0x40`).
- La BGM expose `IBgmFadeDirector.LoadBgm` (`Alundra/Audio/IBgmFadeDirector.cs:45`) et `StopAllSound` (`:33`) ; aucune coupe dédiée à la fanfare.

**Registre et effets**
- Registre natif : `Alundra/Ai/NativeSpriteRegistry.cs:36-43` (production), `:54` (`Register`). Le créneau E, index 11, reste un no-op compté (`EventProgramRunner.cs:296-317`).
- Effets en file : `Alundra/Rendering/Effects/EffectStage.cs:192` (`Emit`). Précédent : `Alundra/Rendering/Effects/NativeEffects.cs:31` et `EventProgramRunner.cs:3146` (`UseLifeVessel`, ADR-0048), tirages à l'application.

**Aides existantes**
- Test d'axe de C255 : `Alundra/Combat/HitPass.cs:63` (`AxisOverlaps`).
- Garde de cible par `DamagedTickCounter` : `HitPass.cs:127`.
- Colonnes 0 et 1 de la table jetées : `Alundra/Items/ItemTables.cs:57-58`, `:81-90`, `:140`.
- Fonctions d'état : `PlayerManager.cs:1875`, `:1893`, `:1910`, `:1970`, `:2181`, `:2238`, `:2263`, `:2285`.

## 1.6 Tests d'aujourd'hui touchés (noms et chemins du HEAD)

**Phase B**
- `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:51-55` : `6 + 1`, à dériver.
- `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:169-190` : `3 + 3 + 1`, à dériver.

**Phase C**
- `Alundra.Tests/Rendering/Effects/DestroyEntityBreakEffectTests.cs:207-219` (`TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents`) : épingle le bouchon.
- `Alundra.Tests/Ai/Enemies/BeannoideOracleTests.cs:373` (`Assert.Empty(SpawnedEntities)`), `:407-418` (160 entrées exclues, `spawn_39F58` et son 84), doc `:30`.
- `Alundra.Tests/Ai/Enemies/MurutaOracleTests.cs:453`, `:486-497`.
- `RunE1Call` fabrique un `new WorldProxy()` sans `InitializeWithWorld` : l'apparition y est refusée, il faut un monde initialisé.
- À relancer, prévision non mesurée : `Alundra.Tests/Arcs/BeannoideGrateArcTests.cs` et `MurutaHallArcTests.cs`.

**Hors tranche**
- `Alundra.Tests/Items/ItemAndMoneyOpcodesTests.cs:149`, `:164`, `:300` (opcode 0x82 inchangé).
- `Alundra.Tests/Items/LootRollWorldTests.Rolls.cs:204` (appel par réflexion de `RollLootAtCreation`, nom et signature épinglés).

## 1.7 Noms d'aujourd'hui

- La DLL est en un dossier = un namespace, sans préfixe `Alundra`. Les classes de test sont en miroir (ADR-0075, ADR-0080).
- La garde est `python -B tools/dll-structure/check_names.py --rules`. Elle est verte : 0 constat, 1 avertissement (`TextBoxGpu.cs:266`).
- Les noms proposés (`LootDrop`, `ItemEffects`, `PickupAi`, `HeroHeals`) sont libres dans la DLL, les tests et le moteur. La garde complète les valide à l'exécution.
- Prochain ADR libre : 0082, à revérifier sur toutes les branches au moment du commit.


# 3. Points qui demanderaient l'auteur (en termes de jeu)

1. **Musique pendant un objet-clé, avant les pistes XA.**
   - Dans l'original, ramasser un objet-clé coupe la musique, joue une petite fanfare (piste 11 du CD), garde le message fermé jusqu'à la fin de la fanfare, puis relance la musique du début.
   - Tant que les pistes XA ne sont pas jouées, deux choix :
     - (A, proposé) la musique continue sans fanfare et le message se ferme normalement tout de suite ;
     - (B) la musique est coupée net et repart du début un instant après (coupure audible).
   - Exposition : 5 objets-clés dans les cartes du jeu (2 vases de vie, 2 emblèmes, 1 Pierre de Zolist) et 22 dans la Debug zone.
2. **Portée du ramassage natif.**
   - Il sert 59 des 152 objets posés : 31 en Debug zone et 28 sur 24 cartes, dont les 10 Cœurs moyens, les 9 sacs de gildas, 3 Herbes, 1 Potion L, 2 vases de vie, 2 emblèmes et la Pierre de Zolist.
   - Les 93 autres (emblèmes des cartes 5 et 479, bouquets des cartes 10 et 331, les vases de vie de la 134...) gardent leur script de carte.
   - Ils ne changent pas avec e0c. Je ne sais pas s'ils marchent aujourd'hui : c'est E19, pas e0c.
   - La recette de la phase D se fait en Debug zone (carte 11), seul endroit où tous les objets-clés apparaissent seuls.
3. **Marchepied** : si T0 mesure que le héros peut aujourd'hui se tenir sur un cœur ou une herbe posés, il les traversera après e0c, comme dans l'original.
4. **Texte** : « gagné! » sans espace avant le point d'exclamation, comme l'ETC du binaire. Vous aviez écrit « gagné ! ».
5. **Registre du masque** : c'est une correction de fait, pas un choix. Seuls les objets-clés écrivent le registre, jamais un objet lâché. Votre réponse à Q-E0C-5 est appliquée là où le binaire l'écrit.

# 4. Risques

1. **La promesse de D-E20-47 est plus étroite que prévu** (59 sur 152) : à dire à l'auteur (point 2). Sans la garde de données (p10), un renvoi futur à « 151 objets » ressortira.
2. **`Block` et `Unblock` touchent `RunEntityBlock`**.
   - Rien n'écrit `BlockedByEntity` aujourd'hui, donc aucun test existant ne devrait bouger. C'est à mesurer en T0 (i).
   - Le binaire choisit les programmes de toutes les entités avant de les exécuter (phase 1 puis 2 par passe). La DLL fait pick puis run entité par entité (`EntityScriptProxy.cs:1573-1585`). Une entité de rang supérieur au bloqueur peut donc ne plus tourner dans le tick du blocage, là où le binaire la laisse courir.
3. **Ouverture d'une boîte sur un texte composé** : le membre additif de `DialogueDirector` partage un système (E12, E15, E19.f) avec beaucoup de tests de dialogue. Il doit rester additif. L'espace entre le nom et « gagné! » est à lire en T0 (h).
4. **Corps de soin partagés avec h1** : e0c vient avant h1 dans l'ordre de l'enveloppe, donc c'est e0c qui écrit `HeroHeals`. h1 (déjà « à réviser », D-E20-51) doit absorber ce fait. La règle des PV (entité seule, D-H1-2) remplace le repli de Q-E0C-7, que l'auteur n'a pas tranché.
5. **Couture de la fanfare** : elle doit être la même primitive que `0xBA` (ADR-0076) pour qu'E19.sm7 n'ait qu'un seul endroit à brancher. Elle est inventée tant que sm7 n'est pas planifiée, donc à nommer en T0.
6. **Ordre des tirages** : après un ramassage, une entité qui tire dans le même tick reçoit d'autres nombres que dans l'original (accepté par ADR-0077). Les clés d'oracle doivent être faites par application de la file, pas par tick du ramassage, sinon (l2) est faux.
7. **`Collidable` effacé par A255** : les objets posés cessent d'être des supports. À mesurer (T0 (j)) avant de promettre « sans changement visible ».
8. **Fichiers sous-module** : `git status` montre `CasaEngineMonogame` et `alundra-datas-analyser` modifiés (contenu local, non indexé). Je n'y ai pas touché ; ADR-0079 prévoit déjà qu'ils ne se stagent pas.
9. **Remaster et copie `data-extracted/`** : e0c ne touche pas aux données. Le recensement lit `data-extracted/` (la copie) et a été recoupé avec le projet exporté. Aucun miroir n'est à faire ici ; le sens des écarts reste à vérifier avant tout futur miroir (texte non décodé du remaster ré-extrait le 2026-09-19).
10. **Invisible sans recette** : le bouchon de fanfare et le gel du monde en mode 0 ne se jugent qu'en jeu. La recette Debug zone est indispensable, pas seulement les tests.
11. **Taille** : quatre phases au lieu de trois, T0 plus lourd. La phase D est autonome et peut être détachée si la relecture la juge trop large, sans toucher à A à C.

# 5. Fichiers de travail (hors du dépôt)

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/prep-e0c/`

- `run_c255.py` et `run_c255_out.txt` : scénarios dirigés sur le vrai code.
- `census_placed.py`, `census_placed_out.txt`, `placed.json` : recensement sur `data-extracted/`.
- `census_exported.py`, `census_exported_out.txt` : recensement sur le projet exporté.
- `fdis.py` et `fulldis.py` : désassemblage ; `full.dis` (listing complet, 6 Mo, à ne pas verser).
- Copies de `emu/`, `exelib.py`, `lh.py`, `models.py`, `c255model.py`, `give_model.py`, `t_c255.py` : harnais de l'annexe.

Seuls les scripts, leurs sorties et `placed.json` sont à verser dans `posed-rev3/`.
