# Critique fraîche de la découverte « carried-draw » : ordre de dessin de l'objet porté

Lecture seule. Je n'ai rien écrit dans le dépôt ni dans le sous-module, rien commité, et je n'ai lancé ni construction ni test dotnet. J'ai travaillé sur des copies des oracles : `diff -rq` avec les annexes d'origine est vide. Étiquettes : [FAIT] lu dans le code, le binaire ou les données ; [MESURÉ] sorti du vrai `ALUN_CD.EXE` dans l'émulateur ; [INFÉRENCE] déduit, non observé.

## 1. Verdict

Les faits porteurs du rapport tiennent. J'ai redésassemblé la chaîne de tri et de dessin, rejoué les trois oracles (sorties identiques au bit près) et relu le HEAD de la DLL et du moteur.

Les captures de l'auteur sont `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/images/15.png` (la bombe) et `16.png` (le sprint sans poussière). Sur la capture 15, Alundra regarde vers le bas et sa chevelure recouvre le bas de la bombe. Alundra face au bas a l'IDSV 4 contre 5 pour la bombe, donc la bombe n'est devant que 1 phase de Y sur 16. Le diagnostic colle.

Trois précisions de conception sont à porter dans la règle de la tranche A (§4). Deux cas ne sont pas couverts (§3).

## 2. Verdicts par fait porteur

| Fait | Verdict | Ce que j'ai vérifié |
|---|---|---|
| F1 clé `+0x1BC` | CONFIRMÉ | Désassemblage de `0x800397AC` et `0x800399B8`. Le calcul est bien `(PosY + IDSV<<16) & 0xFFFF0000` plus la partie entière de Z. La boucle 1 calcule aussi `+0x1C0 = +0x1E0 + +0x1F8` (le haut du corps), que le rapport omet. |
| F2 porteur seulement si collidable | CORRIGÉ (incomplet) | Le test du porteur est exact. Mais F a une seconde moitié (`0x80039860-0x80039978`) omise, voir A1. |
| F3 IDSV de l'image affichée | CONFIRMÉ | `0x80038CBC-0x80038CFC` : `+0x1A8 = octet 0 de l'ensemble d'images`. |
| F4 liste de dessin | CONFIRMÉ pour les entités | `0x8003B414-0x8003B4FC`, aucun cas pour un objet porté. « Un effet entre dans la même liste » : NON VÉRIFIABLE par moi, mais corroboré par `docs/plan-e19-g3c-annexe/discovery/notes.md` §3.1. |
| F5 tri et emplacement OT | CONFIRMÉ | Tri `0x8002DA04`, signé, décroissant, instable. Emplacement `16×min(clé>>20, 59)+6`, écran `((Y−Z)>>16)−camY`. Insertion en tête d'emplacement. Précision d'adresse : `0x8002E130` appelle le tri puis l'émetteur `0x8002DB48-0x8002DE28`. Le sens de lecture de l'OT n'est pas démontré ici (je me fie aux annexes). |
| F6 bombe 119 devant sur 120 | CONFIRMÉ | Rejeu identique. Réserve : monde vide (voir A1). |
| F7 un seul tick derrière | CONFIRMÉ | Rejeu identique : tick 162, saut vers le haut, hero IDSV 6 contre 5. |
| F8 caisse | CONFIRMÉ | Rejeu identique. Les 10 ticks « derrière » du scénario nord précèdent la prise, donc ne sont pas portés. Égalité au tick 11. |
| F9 données | CONFIRMÉ | Bombe IDSV 5, non collidable. Table du héros relue. 17 fiches dont 16 collidables. DrawOrder : héros 0 à 13 (376 fichiers), bombe 0 à 6. |
| F10 passe du HEAD | CONFIRMÉ | `FrameSyncPasses.cs:454-484`. 483 `.world` et 483 `.tileMap`, tous avec `AlundraWallPlacements`. |
| F11 IDSV image 0 | CONFIRMÉ | `EntitySpawnFactory.cs:168-182`, dans `Alundra/Entities/Spawning/`. |
| F12 écart écrit | CONFIRMÉ | Commentaire de `ApplyEntitySortKey` ; ADR-0049 point (3). |
| F13 préfabs | CONFIRMÉ | Mêmes réglages. Aucun `SortAnchorLocal` ni `LocalSortOffset` dans `Alundra/`. |
| F14 `PutInHands` | CONFIRMÉ | Seul écrivain non nul de `PlatformEntity` : `PlayerManager.cs:1724`. Deux appelants : `BombAi.cs:207` et `PlayerManager.cs:1324` (`TryLift`). |
| F15 moteur | CONFIRMÉ | Arrondi `floor(X)` et `ceil(Y)`, `SnapToPixel` vrai, comparaison `(…, Elevation, SortCoordinate, LocalSortOffset+DrawOrder, StableId)`, documentation moteur de « arme tenue devant ». Le test moteur confirme le sens de l'ancre : y = −8 donne +2800. Précision « Z entier » : voir A3. |
| §2 chiffres de l'exemple | CONFIRMÉ | Recalculés à la main : `0x00D20000`, `0x00D30020`, emplacement 214, coordonnées 20600 et 17400. |
| §3 modèle | CONFIRMÉ | Arithmétique exacte. Moyenne non pondérée par le temps : Alundra face à gauche ou à droite (IDSV 5 contre 5) donne 0 % devant. |
| §4 h3b sans effet | CONFIRMÉ | Diff de `161ac276` relu. `ceil(Z−Y_f) = Z − floor(Y_f)` pour Z entier. Écart théorique d'1 px à plus d'un 16 384ᵉ de cas. |
| §5 moteur sans défaut | CONFIRMÉ | `SortAnchorLocal` et `LocalSortOffset` sont publics et testés. Le défaut vient du choix de coordonnée de la DLL (ADR-0049 point 3). |
| §6 règle de la tranche A | CORRIGÉ | Quatre points au §4. |
| §8 tests | CORRIGÉ (incomplet) | Ajouter `BombHandsWorldTests` (banc ArcRun, commit `c51504fb`) comme hôte naturel. `HeroAuraWorldTests` B5 compare `heroKey.Elevation` à celle du quad, donc l'arène exécute bien la passe. `WorldProxyEntityManipulationTests:278-290` (entité `FlagToDestroy` ignorée) doit rester vert : les nouvelles écritures doivent être ignorées aussi. |

## 3. Manques et contradictions

**A1. Le héros peut être relevé par un voisin, et la bombe passe alors derrière lui dans l'original.**
- [FAIT] La seconde moitié de F relève la clé de toute entité collidable à celle des collidables plus bas qui chevauchent sa boîte en X et Y. La liste `0x80127C28` ne contient que les collidables : le héros et le sprite 25 y sont, le sprite 16 et la bombe n'y sont pas (`out_list_probe2.txt`).
- [MESURÉ, avec une écriture de harnais] Un voisin collidable de 16 px de haut, chevauchant la boîte du héros au sud, donne la clé du héros `0x00D00000` contre `0x00CD0020` pour la bombe. La bombe est alors derrière pendant 66 ticks sur 66 (`out_neighbour_probe5.txt`). Le même voisin à sa hauteur réelle de 32 px, ou à 16 px mais sans chevauchement, ne change rien.
- Conséquence : le « toujours devant » du résumé ne vaut que pour un héros isolé. Le port ne reproduit pas ce relèvement (écart déjà écrit dans la doc de `EntityDepthKey`). Il faut l'écrire dans l'ADR comme écart accepté.
- Les vecteurs d'oracle du rapport sont valides tels quels, car ils sont pris dans un monde vide.

**A2. Le relâchement : la tranche A seule laisse un saut devant puis derrière au lancer.**
- [MESURÉ] Bombe lancée avec le carré (`out_throw_probe.txt`) :
  - lancée à gauche, à droite ou vers le bas, la bombe reste devant le héros pendant toute la fenêtre observée, de l'image 50 à 55 ;
  - lancée vers le haut, elle passe derrière après 4 ticks, ce qui est normal puisque son Y diminue.
- [INFÉRENCE] Après la tranche A, un objet lâché reprend la coordonnée `100·(Y−Z)`, donc se retrouve derrière le héros dès qu'il quitte les mains. Le chevauchement visuel est faible (mains et tête) mais le basculement est visible. Le rapport range ce cas dans une « suite à part » ; la question 3 doit le dire explicitement.

**A3. Le Z du héros est fractionnaire en l'air, celui de l'objet porté est entier.**
- [FAIT] `FollowPosZOnRoot` fait suivre à la racine du héros `PosZ/65536f`. L'objet porté a une racine à `PosZ>>16` (`EntityScriptProxy.cs:2626-2640`).
- Le sprite du héros est donc à `100·floor(Y−Z_frac)`, et l'objet à `100·(floor(Y)−floor(Z))`. L'objet est égal ou plus haut de 100, jamais derrière.
- C'est inoffensif pour le cas « devant ». Mais la formule `aY` tout en entiers du rapport ne garantit pas l'égalité exacte en saut, donc le cas « derrière » (−K) n'est pas garanti.

**A4. L'étincelle de la mèche n'est pas ancrée.**
- [FAIT] `BombAi.cs:321` la crée en mode 0 (`CreateNow`). Le point 5 de la tranche A (`IEffectAnchor.SortCoordinate`) ne la touche donc pas.
- Sa coordonnée reste `100·(Y−Z−8)`, avant comme après la correction : derrière la bombe et derrière le héros. Sur la capture 15 la flamme est visible parce qu'elle dépasse de la silhouette de la bombe. L'ordre étincelle contre bombe dans l'original n'est pas mesuré (l'oracle ne simule pas les effets).

**A5. Références.**
- Le HEAD courant est `4f7d7a42` ; seuls des commits de documentation séparent `539d4cf4` de lui, les faits de code tiennent.
- L'arbre de travail contient les modifications non commitées de `EarthTome1Ai`, sans recouvrement avec la tranche A.

## 4. Corrections à la règle de la tranche A

1. **Égalité des clés.** Le rapport écrit « −K sinon ». Une caisse collidable relevée est au moins à la clé du porteur, et sa partie Z ne la met jamais plus bas. À l'égalité (tick de la prise, mesuré dans `out_crate_keys_rerun.txt`), il faut +K. Réserver −K au seul cas `clé_objet < clé_porteur`, soit la bombe non collidable lors du saut vers le haut.
2. **Calcul de `aY`.** Le calculer à partir des deux translations rendues (`RenderProjection.WorldMatrixNoScale.Translation`), pas des entiers logiques. L'égalité est alors exacte par construction, y compris en saut (voir A3).
3. **Valeur de K.** Il faut `K ≥ 14` pour le +K (DrawOrder maximal du héros : 13). Pour le −K, il faut `K >` DrawOrder maximal de l'objet (6 pour la bombe, non mesuré pour les autres objets). Une constante unique large avec un test qui épingle ces deux maxima convient.
4. **Effets.** Le point 5 ne vaut que pour les effets ancrés (modes 1 et 3), comme l'effet de bris d'une caisse tenue. Il faut ajouter à la liste d'écarts que l'étincelle (mode 0) n'est pas touchée (A4).

## 5. Découpage

Je confirme la tranche A en DLL seule, dans les mêmes fichiers que le rapport, avec les quatre corrections ci-dessus. L'option « coordonnée explicite dans le moteur » (tranche B) n'est pas indispensable. Avant la tranche A, T0 doit poser A1 et A2 comme écarts connus dans le plan.

## 6. Questions à l'auteur, défaut « comme l'original »

1. **Saut vers le haut avec la bombe.** Pendant une seule image (le décollage), l'original dessine la bombe derrière Alundra. Je le reproduis (défaut), ou je la garde toujours devant ?
2. **Tous les objets portés.** Caisses, pots et torches corrigés d'un coup, avec la même règle (défaut), tu les vérifies en recette ?
3. **Au lancer.** Dans l'original, la bombe lancée reste devant Alundra tant qu'elle est en l'air près de lui (sauf lancer vers le haut). Si je ne corrige que la bombe tenue, elle passera derrière Alundra dès qu'elle quitte les mains. Je l'inclus dans cette tranche (défaut, comme l'original, un peu plus de travail) ou je la laisse repasser derrière au lancer ?

## 7. Chemins

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-feedback/carried-draw-critic/`

- Désassemblages : `dis_F.txt`, `dis_drawlist.txt`, `dis_sortemit.txt`, `dis_emit2.txt`, `dis_idsv_src.txt`.
- Rejeux identiques à ceux du rapport : `out_bomb_keys_rerun.txt`, `out_bomb_keys2_rerun.txt`, `out_crate_keys_rerun.txt`.
- Mes sondes et leurs sorties :
  - `list_probe.py`, `list_probe2.py` → `out_list_probe.txt`, `out_list_probe2.txt`
  - `neighbour_probe5.py` → `out_neighbour_probe5.txt` (voisin qui relève le héros)
  - `neighbour_probe2.py` → `out_neighbour_probe2.txt` (boîtes et hauteurs, voisin collidable)
  - `throw_probe.py` → `out_throw_probe.txt`
  - `sprite_scan.py` → `out_sprite_scan.txt`
  - `draworder_scan.py` → `out_draworder_scan.txt`
- Copies d'oracles (jamais modifiées) : `oracle_h3bin/`, `oracle_h3/`, `oracle_g1a/`.
- Aide de désassemblage : `bx.py`.