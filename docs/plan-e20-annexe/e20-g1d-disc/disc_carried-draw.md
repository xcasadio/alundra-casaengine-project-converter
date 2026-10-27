# Ordre de dessin de l'objet porté : rapport de lecture seule

Rien n'a été écrit dans le dépôt ni dans le sous-module. Aucune construction, aucun test. Tous les fichiers produits sont dans le dossier de travail (§11).

**Résumé.**
- L'original trie les sprites sur la clé `(Y_px + IDSV)` en poids fort puis `Z` entier en poids faible. Un objet porté, plus haut de 32 px, a donc toujours une clé supérieure à celle d'Alundra et passe devant (sauf une image d'un saut, voir F7).
- Le portage trie à l'intérieur d'un cran de 16 px sur `100 × (Y − Z)`. Plus haut veut dire plus petit, donc dessiné avant, donc derrière.
- C'est vrai de tout objet porté, pas seulement de la bombe.
- Le changement d'E20.h3b n'y est pour rien.
- Aucun défaut moteur : la correction se fait côté DLL, avec les réglages que le moteur documente (point de tri et décalage local).
- Je n'ai pas pu faire tourner le portage. Le diagnostic côté portage est une lecture de code, complétée par un modèle ; il colle à ta capture 1.

Légende : [FAIT] lu dans le code ou le binaire, [MESURÉ] sorti de l'émulateur du vrai `ALUN_CD.EXE` ou d'un script, [INFÉRENCE] déduit, non observé.

## 1. Faits numérotés

### Original (`ALUN_CD.EXE`, France)

**F1 [FAIT]** La clé de profondeur d'une entité est son champ `+0x1BC`. Le pilote `0x800399B8` la calcule en trois boucles. Désassemblage dans `dis_sort_driver.txt`.
- Boucle 1 (`0x800399E8-0x80039A0C`) : remet `+0x1BC` à 0.
- Boucle 2 (`0x80039A34-0x80039A60`) : appelle `F` (`0x800397AC`) pour chaque entité visible.
- Boucle 3 (`0x80039A8C-0x80039AB0`) : `+0x1BC = (+0x1BC & 0xFFFF0000) + (short)(+0x11E)`, la partie entière de Z.
- `F` part de `s2 = PosY(+0x118, 16.16) + (IDSV(+0x1A8) << 16)` (`0x800397EC-0x80039804`). Le Y d'écran n'intervient pas, Z n'entre qu'en poids faible.

**F2 [FAIT]** Le cas du porteur existe, mais seulement pour un objet collidable.
- `F` lit `+0x28` (`0x8003981C`) uniquement si `+0x6C & 0x80` (collidable) est posé et `+0xB4 & 0x80` clair (`0x800397F4-0x80039818`).
- Dans ce cas, si `s2` est inférieure à la clé du porteur, la clé du porteur la remplace (`0x8003982C-0x80039858`).
- `+0x2C` (objet porté) n'est lu ni par le tri ni par le dessin.

**F3 [FAIT]** L'IDSV est celui de l'image affichée, pas de l'animation.
- `0x80038CBC-0x80038CF8` : `+0x1A8` reçoit l'octet 0 de l'ensemble d'images courant.

**F4 [FAIT]** La liste de dessin est construite en `0x8003B414-0x8003B4FC`, sans cas particulier pour un objet porté.
- Elle copie la clé vers `SpriteRef+0x10` (`0x8003B4AC-0x8003B4B4`) et X/Y/Z vers `+4/+8/+0xC`.
- Un effet entre dans la même liste.

**F5 [FAIT]** Le tri et l'émission se font en `0x8002E130`.
- Le tri est `0x8002DA04`, signé, décroissant (`0x8002DA68-0x8002DA8C`, `dis_quicksort.txt`). Il est instable, donc les clés égales n'ont pas d'ordre garanti.
- L'emplacement OT est `16 × min(clé >> 20, 59) + 6` (`0x8002DBD8-0x8002DBEC`).
- La position d'écran est `((Y − Z) >> 16) − camY` (`0x8002DB80-0x8002DB9C`).
- Le plus petit clé est dessiné d'abord, par insertion en tête d'emplacement (`docs/plan-e19-g3c-annexe/discovery/notes.md` §3.1, `docs/plan-e20-annexe/e20-f1/check.md`).
- Z ne change donc jamais l'emplacement OT, il départage à l'intérieur.

**F6 [MESURÉ]** Bombe portée (`bomb_keys.py`, tri `0x800399B8` rejoué après chaque tick) :
- Le héros a la clé `0x00690000`, la bombe `0x00690020` dès qu'elle est à 32 px, emplacement OT 102 pour les deux.
- La bombe est donc dessinée après le héros, devant, sur les 119 ticks où elle figure dans la liste de dessin.
- Drapeaux : héros `0x0003118C` (collidable), bombe `0x0013234E` (non collidable).
- `+0x28` de la bombe vaut `0x80127D30`, l'adresse du héros. Pourtant le cas du porteur de F2 est sauté.
- La bombe est devant uniquement parce que son IDSV (5) est au moins égal à celui d'Alundra et que Z est plus haut.

**F7 [MESURÉ]** Bombe portée en marchant et en sautant dans les 4 directions (`bomb_keys2.py`) :
- Sur 241 ticks portés, un seul tick est derrière : le tick 162, l'élan du saut avec objet face au haut de l'écran. Le héros y a l'IDSV 6, la bombe 5, clés `0x00C90005` contre `0x00C80025`.
- Les ticks 255 à 262, derrière aussi, sont ceux de la bombe lancée puis explosant, plus portée.

**F8 [MESURÉ]** Caisse (collidable, IDSV 7), trois levées de la carte 392 (`crate_keys.py`) :
- Une caisse soulevée du côté nord est relevée à la clé du porteur au tick de la prise (égalité, `0x00750000`), puis dépasse de +Z. C'est le chemin du relèvement de F2.
- Devant à tous les ticks portés dans les trois scénarios.

**F9 [FAIT]** Données (`alundra-project/Data/sprite-records.json`), sorties dans `out_bomb_idsv.txt`, `out_hero_idsv_carry.txt`, `out_liftables.txt` :
- Bombe : `BankKey alundra_2`, Flags `0x13234E`, non collidable, IDSV de l'animation 0 égal à 5 dans les 4 directions.
- Héros : `alundra_0`, IDSV de portage de 0 à 6.
  - Animation 12 (debout avec objet) : bas 4, haut 2, gauche et droite 5.
  - Animation 7 (marche) : 1 à 3.
  - Animation 6 (saut) : bas 0, haut 6.
- 16 des 17 fiches soulevables sont collidables. La bombe est la seule qui ne l'est pas. La torche est la fiche `137` (IDSV 7).

### Portage au HEAD `539d4cf4` (DLL)

**F10 [FAIT]** `Alundra/Maps/Loop/FrameSyncPasses.cs:454-484` (`RunWallInterleaveSortKeyPass`) n'écrit que `DepthSortable2DComponent.Elevation`.
- Formule : `16 × min((PosY + (IDSV << 16)) >> 20, 59) + 6` (`WallPlacementOverlay.cs:345-354`).
- Pour un objet collidable porté, la valeur est relevée à la clé du porteur (`:473-480`, `EntityDepthKey.cs:33-41`, W6).
- La passe n'a lieu que si `_wallPlacementOverlayApplied` (`WorldProxy.cs:2690`). Les 483 cartes portent la propriété.

**F11 [FAIT]** L'IDSV du portage est celui de l'image 0 de la paire (animation, direction), pas de l'image affichée (`EntitySpawnFactory.cs:168-182`, `FrameSyncPasses.cs:490-500`).

**F12 [FAIT]** L'ordre fin est laissé au moteur. Le commentaire `WallPlacementOverlay.cs:363-368` écrit lui-même cet écart : l'ordre fin vient de `-worldY`, qui plie Z, au lieu de `PosZ >> 16`. Voir aussi ADR-0049 point (3).

**F13 [FAIT]** Le préfab de la bombe (`Bombe.entity:178-191`) et celui du héros (`Alundra.entity:542-555`) ont les mêmes réglages :
- `sort_mode TopDownYUp`, `sort_anchor (0,0)`, `local_sort_offset 0`, `sorting_layer 300`, `elevation 0`.
- Le DLL n'utilise ni `SortAnchorLocal` ni `LocalSortOffset` (recherche sur tout `Alundra/`).

**F14 [FAIT]** Portée et placement :
- `PutInHands` (`PlayerManager.cs:1720-1731`) est le seul endroit qui pose `PlatformEntity` non nul.
- `CarryRules` (`HeadHeight = 0x200000`, `FreeOffset`, mode 0) tire l'objet à X et Y du héros et à Z + 32 px.
- `BombAi.Start` (`:198-210`) fait naître la bombe à la position du héros.
- `FollowCarrier` : `EntityScriptProxy.cs:1867-1890`.

### Portage (moteur)

**F15 [FAIT]**
- Le sprite est placé à `(X, −(Y − Z))`, avec arrondi au pixel : `floor(X)` et `ceil(renderY)` (`SimulationSpacePolicy.cs:159-176`, `RenderProjectionComponent.UpdateProjection`). `SnapToPixel` est vrai pour toute entité créée (`EntitySpawnFactory.cs:766`).
- La coordonnée de tri est `round(−(worldY + ancre.Y) × 100)` (`DepthSortable2DComponent.cs:66-76` et `:101-114`), donc `100 × floor(Y_px − Z_px)`.
- La clé est comparée dans l'ordre `(passe, couche, ordre, Elevation, SortCoordinate, LocalSortOffset + DrawOrder de pièce, StableId)`, croissant (`RenderSortKey2D.cs:66-78`, `SpriteRendererComponent.cs:527` et `:553-558`). Le plus petit est dessiné d'abord.
- Le moteur documente `LocalSortOffset` et `SortAnchor` pour un objet tenu : « arme tenue devant : même point de tri que le personnage, décalage +5 » (`CasaEngineMonogame/docs/engine/tilemaps-gestion-profondeur.md`, sections SortCoordinate et LocalSortOffset).
- `SortAnchorLocal` et `LocalSortOffset` sont publics et modifiables.

## 2. Pourquoi l'objet porté passe derrière

Exemple chiffré : Alundra à Y = 206,09375 px, debout face au bas (animation 12, IDSV 4), Z = 0. La bombe est à Z = 32 px, IDSV 5.

| | Alundra | Bombe |
|---|---|---|
| Y d'écran (original) | 206 | 174 |
| Clé du binaire | `0x00D20000` | `0x00D30020` |
| Emplacement OT | 214 | 214 |
| **Original** | dessiné d'abord | **après, devant** |
| `Elevation` du portage | 214 | 214 |
| `SortCoordinate` du portage | 20600 | 17400 |
| **Portage** | dessiné après | **avant, derrière** |

- **[FAIT, F12 et F15]** Les `Elevation` sont égales dans environ 15 cas sur 16, car le cran vaut 16 px. C'est la coordonnée qui décide, et Z pèse dans le mauvais sens (−3200 pour 32 px).
- **[INFÉRENCE]** La bombe repasse devant seulement quand `Y_px mod 16` tombe dans une bande de 1 à 5 px, car son IDSV lui donne alors un cran de plus. Pendant une marche verticale, elle peut donc clignoter entre devant et derrière.
- **[INFÉRENCE]** Même problème pour la bombe lancée en vol (Z en l'air, plus de porteur), et probablement pour la silhouette d'Alundra qui saute près d'un PNJ. Je ne les ai pas mesurés.

## 3. Tous les objets portés, ou seulement la bombe ?

**[INFÉRENCE, modèle `order_model.py` et `order_summary.py`, sorties `out_order_model.txt` et `out_order_summary.txt`]** : tous les objets portés sont concernés. Le modèle donne la part des positions où l'objet est devant, pour chaque fiche soulevable et chaque état de portage.

| Fiche | Collidable | IDSV | Original : devant | Portage : devant (moyenne, maximum) |
|---|---|---|---|---|
| bombe | non | 5 | 97 %, jamais derrière sauf le saut vers le haut | 12 %, 31 % |
| `alundra_1`, `107` à `113`, `41` | oui | 2 à 4 | 100 % | 1 % à 8 %, maximum 25 % |
| `182`, `37`, `48` | oui | 5 à 6 | 100 % | 12 % à 18 % |
| `34` à `38`, `117`, `137` (torche) | oui | 7 | 100 % | 24 % |
| `alundra_234` (lourd) | oui | 11 | 100 % | 49 % |

- Ça rend le défaut d'autant plus visible que l'IDSV de l'objet est bas.
- Le relèvement W6 ne change rien à la coordonnée.
- Le test `CarriedSortKeyTests.cs:62-70` n'épingle que l'`Elevation`, pas l'ordre fin.
- La torche n'a pas été mesurée à l'émulateur. Elle a les mêmes drapeaux que la caisse et le jumeau C70 de C23, donc le même chemin.

## 4. L'effet d'E20.h3b (commit `161ac276`)

**[FAIT et MESURÉ par script]** Aucun effet sur l'ordre :
- Avant : `FollowCarrier` appelait `PushLogicalPositionToRoot()` avec une racine à `(PosX >> 16, PosY >> 16, PosZ >> 16)`. Après : `keepSubPixel: true`, racine à `(PosX / 65536f, PosY / 65536f, PosZ >> 16)` (`EntityScriptProxy.cs:2626-2640`).
- Avec `SnapToPixel`, `ceil(−(Y_f − Z))` vaut `Z − floor(Y_f)` pour un Z entier. La position rendue et la coordonnée sont donc identiques.
- Vérifié dans `h3b_snap_check.py` (flottants 32 bits émulés) : 2 écarts sur 200 000 couples, d'un pixel, à moins d'un 65536ᵉ de pixel d'un entier. Pour Y = 206,09375 et Z = 32, la coordonnée est 17400 avant comme après.
- L'`Elevation` vient de `PosY` logique, inchangée.
- Ce qui date de h3b : la bombe tenue n'existe en production que depuis son enregistrement (`PlayerManager.cs`, `BombItem`). Les caisses sont portées depuis g1b, avec le même défaut.

## 5. Ce qui manque

**Côté DLL**
- Une règle d'ordre fin pour l'objet porté : même point de tri que le porteur, décalage local au-dessus de ses pièces. C'est la suite logique de W6, qui n'a fait que le cran.
- `IEffectAnchor.SortCoordinate` (`EntityScriptProxy.cs:482`) doit suivre la coordonnée du sprite pour une entité portée, sinon l'effet de bris d'une caisse tenue (ancre gelée) se trie à l'ancienne coordonnée.

**Côté moteur**
- Rien d'indispensable : `SortAnchorLocal` et `LocalSortOffset` existent, sont documentés et testés (`CasaEngine.Tests/Scene/DepthSortable2DComponentTests.cs`, sens du décalage d'ancre : ancre y = −8 augmente la coordonnée).
- Le moteur trie correctement ce qu'on lui donne. Ce n'est donc pas un défaut du moteur. Option plus lourde, non nécessaire ici : un mode moteur « coordonnée explicite » (voir 6.B).

## 6. Découpage proposé

**Tranche A (celle que je recommande), DLL seule.** Elle ne dépend de rien d'ouvert. h3b, g1a et g1b sont faites.

Règle proposée, pour une entité dont `PlatformEntity` est un proxy porteur (même test que W6) :
1. `Elevation` : inchangée.
2. `SortAnchorLocal = (0, aY)` avec `aY = ((PosY_objet >> 16) − (PosZ_objet >> 16)) − ((PosY_porteur >> 16) − (PosZ_porteur >> 16))`, en pixels. Sa coordonnée devient celle du porteur. Pour la bombe à 32 px, `aY = −32`.
3. `LocalSortOffset = ±K` : plus K si la clé du binaire de l'objet est supérieure à celle du porteur, moins K sinon. Le cas « moins » est le saut vers le haut avec la bombe (F7). K doit dépasser le plus grand `DrawOrder` des pièces du héros ; je trouve 13 dans les `bankalundra_0_anim*.anim2d`.
4. Les deux valeurs sont réécrites à chaque image pour toute entité, donc remises à (0,0) et 0 quand l'objet est lâché.
5. `IEffectAnchor.SortCoordinate` d'une entité portée prend la coordonnée du porteur.

Fichiers touchés :
- `Alundra/Maps/Loop/FrameSyncPasses.cs`
- `Alundra/Entities/EntityDepthKey.cs` (fonction pure de la règle)
- `Alundra/Entities/EntityScriptProxy.cs` (ligne 482)
- `Alundra/Cells/WallPlacementOverlay.cs` (le commentaire de l'écart, lignes 357-369)
- `Alundra.Tests/Maps/Loop/CarriedSortKeyTests.cs` (étendu)
- Un test de monde dans le style de `HeroAuraWorldTests` (B5) : `BuildSortKey` sur `RenderProjection.WorldMatrixNoScale.Translation` du héros et de la bombe.
- Un ADR (amendement d'ADR-0089, qui contient déjà W6, ou ADR neuf).

Ordre des tâches :
- T0 : copier les oracles ci-dessous et poser la question 2.
- T1 : tests d'abord, rouges sur le HEAD. Test de monde de la bombe sur les 16 phases de Y, test de la caisse, test du saut vers le haut, test du lâcher.
- T2 : la règle et `SortCoordinate` de l'ancre.

Le poste est étroit, sans conflit avec l'exécuteur en cours : il n'écrit que `EarthTome1Ai`.

**Tranche B (suite, à décider séparément).** Reproduire la clé du binaire pour toutes les entités et les effets. Ça corrigerait aussi le saut, la bombe en vol et l'étincelle.
- **Sans engine** (réglages existants uniquement) : coordonnée `100 × (Y_px + IDSV)` par `SortAnchorLocal.Y = −Z − IDSV`, plus `LocalSortOffset = Z × 256`. Il faut alors toucher tous les effets ancrés.
- **Moteur** : une coordonnée de tri explicite posée par le jeu.
- Le rayon d'action est large : `HeroAuraWorldTests` B5, `EffectQuadPushTests`, `FrozenAnchorTests` et ADR-0049 et ADR-0057 sont concernés. Je ne le recommande pas pour ce retour.

## 7. Oracles et clés possibles

- Le tri du binaire se rejoue sans modifier l'oracle existant : copie de `docs/plan-e20-annexe/e20-h3/critic/run` (`oracle_h3`), de `e20-h3/bin/run` (`oracle_h3bin`) et de `e20-g1a/work/orc` (`oracle_g1a`), avec `W.run_pass(m, 0x800399B8)` après chaque tick, comme `e20-f1/oracle/keyprobe.py`.
- Clés prêtes (sorties du dossier) :
  - `out_bomb_keys.txt` (120 ticks, bombe)
  - `out_bomb_keys2.txt` (294 ticks : marche, 4 sauts, lancer)
  - `out_crate_keys.txt` (3 levées de caisse)
- Utilisation côté DLL : un test qui, pour chaque ligne (animation du héros, direction, IDSV, Y, Z, drapeaux), compare le signe `clé objet − clé héros` à celui de `RenderSortKey2D.CompareTo` des deux sprites. Signe attendu : devant partout, derrière au tick 162 de `bomb_keys2`.
- Hors périmètre, et non établi : l'étincelle de la mèche (effet 9) face à la bombe. L'oracle ne simule pas les effets.

## 8. Tests existants qui bougeraient (tranche A)

- `CarriedSortKeyTests.cs` : ses assertions d'`Elevation` restent vraies, il est étendu.
- `WorldProxyEntityManipulationTests.cs:278-317` : inchangés, la passe écrit en plus des valeurs par défaut.
- `FrozenAnchorTests.cs` : à étendre (cas d'une entité portée).
- Pas de test de l'ordre fin d'une entité dans `Alundra.Tests` aujourd'hui. Seuls `HeroAuraWorldTests` B5, `EffectQuadPushTests.cs:253-255` et `FrozenAnchorTests.cs:38` touchent à des coordonnées de tri.
- Côté moteur, rien (tranche A).

## 9. Risques

- Le diagnostic côté portage n'est pas observé en exécution. Le test rouge du T1 le confirmera ou l'infirmera avant toute correction.
- Un PNJ pris entre le porteur et l'objet pendant les 15 premiers pixels de la levée peut être mal intercalé : écart accepté et à écrire.
- L'IDSV du héros est celui de l'image 0 (F11) alors que le binaire lit l'image affichée. Ça peut décaler le cran sur les frontières seulement.
- ADR-0026 : PosZ du DLL vaut celui du binaire moins 1. Le calcul de la coordonnée doit rester en repère DLL, celui de la clé en repère binaire, comme `EntityDepthKey.Compute`.
- Si la tranche B est faite plus tard, la règle de A doit rester isolée dans une fonction pure remplaçable.

## 10. Questions à l'auteur (défaut « comme l'original »)

1. **Quand Alundra saute en marchant vers le haut de l'écran en portant une bombe, pendant une seule image (le décollage), l'original dessine la bombe derrière lui.** Je reproduis ça (défaut), ou je la garde toujours devant ?
2. **Ce que tu as vu sur la bombe vaut pour tout ce qui se porte (caisses, pots, torches).** Je corrige tous les objets portés d'un coup (défaut), et tu les vérifies en recette ?
3. **La même règle fausse touche aussi tout ce qui est en l'air : Alundra qui saute près d'un PNJ, la bombe en vol après le lancer, l'étincelle de la mèche.** Je corrige seulement l'objet porté maintenant (défaut, ce que tu as signalé) et je note le reste comme suite à part ?

## 11. Chemins (dossier de travail)

Base : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-feedback/carried-draw/`

- Scripts : `bx.py` (copie), `idsv_scan.py`, `idsv_dump.py`, `liftables_survey.py`, `order_model.py`, `order_summary.py`, `h3b_snap_check.py`, `bomb_keys.py`, `bomb_keys2.py`, `crate_keys.py`.
- Sorties : `out_bomb_idsv.txt`, `out_hero_idsv_carry.txt`, `out_liftables.txt`, `out_order_model.txt`, `out_order_summary.txt`, `out_h3b_snap_check.txt`, `out_bomb_keys.txt`, `out_bomb_keys2.txt`, `out_crate_keys.txt`.
- Désassemblages : `dis_sort_driver.txt`, `dis_drawlist.txt`, `dis_render_entities.txt`, `dis_emitter_head.txt`, `dis_quicksort.txt`, `dis_idsv_source.txt`.
- Copies de lecture du HEAD : `*.head.cs` et le dossier `engine/`.
- Copies de l'oracle, jamais modifiées : `oracle_h3`, `oracle_h3bin`, `oracle_g1a`.