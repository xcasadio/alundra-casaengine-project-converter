# Contre-vérification du rapport E20.f5 (`f-state/f5/state.md`)

Fait en lecture seule au HEAD `3c923cfa` (branche `chantier/restructuration-dll-r3`). Je n'ai rien écrit dans le dépôt, les sous-modules ni `alundra-project/`, et je n'ai lancé aucun projet .NET. Les marques ` m CasaEngineMonogame` et ` m alundra-datas-analyser` étaient déjà là à ma première commande `git status`.

Mes outils de lecture sont dans `scratchpad/f-state/f5check/` :
- `show.py` lit les lignes d'un fichier à une révision donnée ;
- `oracle_rows.py` et `oracle_summary.py` relisent les JSON de `scratchpad/disc-e20f4f6/bin/out/json/`.

## Erreurs

### E1. En-tête : la révision annoncée n'est pas celle des numéros cités

- Le rapport se dit « au HEAD `e1a68676` ». Or ses numéros de ligne du plan sont ceux de `3c923cfa` :
  - O-E20-4 est à l.7498 et Q-G1A-4 à l.7521 au HEAD ;
  - à `e1a68676`, ils étaient à 7475 et 7498 (`git show <rev>:docs/plan-e20-heros.md | grep -n`).
- `git diff --stat e1a68676 HEAD` ne touche que `docs/plan-e20-heros.md`, et seulement après la l.7318. Le code est donc identique et les sections §1.20 à §1.28 aussi.
- Gravité : forme seulement.

### E2. A20 (ii) : « aucun bord nord ou sud » dans le T0 est faux pour le nord

Les scénarios « libres » du T0 frappent en réalité le bord de la carte et rebondissent (`run_arrows.py:78-81`, « free field », sans mur) :
- `arrow3_free_north` : drapeau à S+23 en y = 7, puis rebond (statut 3, direction 0, +1,25 px par tick) ;
- `arrow3_free_west` : drapeau à S+15 en x = 10, puis rebond (`oracle_summary.py`).

Le bord nord que l'épingle P3 d'e0k3 attend (l.5268-5270) est donc déjà dans la liste du T0. Le plan ne le nomme pas comme tel ; seul le **sud** manque vraiment. Le §6.13 du rapport est à corriger dans le même sens. Il faut aussi signaler que les attendus des scénarios `arrow3_free_*` (nord et ouest) contiennent un rebond E17.

### E3. §3.A, ligne « S1, premier bloc, phase B » : présenté comme certain, il dépend de f4

D-F4-8 (l.5786-5787) dit : « le premier bloc de S1 et (p5) de f3c prennent un ensemble de test **vide** », réécrits « par la première des tranches », donc par f4.
- Si f4 applique D-F4-8, le premier bloc de S1 ne bascule plus à la phase B de f5.
- Le §3.B du rapport ne retient de D-F4-8 que S5, et son §6.9 compte S1 parmi ce qui rougit au retrait de {3, 4}.

Même réserve pour l'égalité `S5 :243` : elle bascule déjà à la phase B de **f4**, quand 7 entre dans l'ensemble. La ligne n'existait pas quand f4 a été planifiée, et la liste de f4 annonce « phase B : aucun test existant » (l.5848). Ce que f5 trouvera dépend de la façon dont f4 l'aura réécrite. Ces lignes sont « à relire à l'exécution », pas des épingles certaines de f5.

### E4. §3.C, AlundraHeroAttackTests : la justification est fausse

- **Ce que dit le rapport** : « hôtes qui ne sont pas des `IEntityWorldContext` (aucune référence dans ces fichiers) ».
- **Ce que montre le code** : `AlundraHeroAttackTests.cs:444` et `:501` construisent un `ArcRun`, dont le `WorldProxy` est un `IEntityWorldContext`.
- **Pourquoi la conclusion tient quand même** : ces deux cas ne frappent qu'à l'épée. Le tir à l'arc (`:128`, `:402`) passe par `JumpHeroRig`.

### E5. §3.C, AlundraLogicalZStepTests « à lire » : se tranche à la lecture

Ces tests appellent `ScriptedMotion.RunLogicalZPreXyStep` directement, par l'aide `Tick` de `AlundraLogicalZStepTests.cs:55-60`. Il n'y a ni passe des programmes, ni E17, ni clé de vitesse. Ils ne sont pas touchés.

### E6. §4.1 : citation mal attribuée

- **Ce que dit le rapport** : « e0c et E20.e … leur ordre avec f4, f5, f6 n'est pas fixé (l.6393-6394) ».
- **Ce que disent ces lignes** : elles portent sur l'ordre des clés du registre entre e0c, g1a et g2b.
- **L'ordre d'E20.e** : le §1.25 **propose** « E20.e … avant f4 ; f4, f5, f6a, f6b » (l.6837-6838, « sans changer les dépendances écrites »).
- **Conséquence** : le bouchon que D-F4-3 veut extraire aura peut-être déjà été remplacé par E20.e. Sans effet sur f5 (E17 n'appelle pas la coupe).

### E7. A5 : valeur intermédiaire inexacte

- Le corps de 16 px vaut `Depth = (16 << 16) − 1 = 1 048 575` (`EntitySpawnFactory.cs:864`), et non 1 048 576.
- Le test est `d < Depth + 1 = 1 048 576`, avec `d = 1 048 577` : faux pour 16 px, vrai pour 17 px (1 114 112).
- La conclusion 16 rate / 17 touche tient.

### E8. §6.2 et §6.5 classés « bloquants » à tort

Je les reclasse en « à préciser ».
- **(2) Phase de rebond et retour au statut 2.** La DLL commente les champs :
  - `TargetAnimationId //88` (`EntityScriptProxy.cs:158`) ;
  - `TargetDirection` au champ suivant ;
  - `ForceResetAnimationFlag` (`:167`, `+0xAC` dans `FrameSyncPasses.cs:163-164`).

  D-F5-4 dit « cible d'animation 1 ». Le retour au statut 2 est écrit par E17 lui-même, dans le relevé du binaire (l.5918, « Non trouvé : statut 2 »), et un mutant le vise (« statut 3 au repli », l.6032). Il manque seulement de l'écrire dans une décision D-F5.
- **(5) API de destruction.** « `DestroyEntity(e, −1)` » désigne sans ambiguïté la forme à deux arguments. L'accès par `entity.ScriptHost` a déjà son précédent dans le code en ligne (`EventProgramRunner.cs:290`). C'est à nommer, pas un blocage.

## Omissions

### O1. Prédicat de D-F5-5 : trois marqueurs, pas deux, et une conséquence en production

| Marqueur | Déclaration | Qui le pose |
|---|---|---|
| `HasLogicalZ` | `EntityScriptProxy.cs:1383` | l'interface seule (`WorldProxy.cs:3573`) ; doc `:1381` : « this one stays for the interface » |
| `JoinsBirthTick` | `:1375` | l'interface, et depuis e0a4 (le 2026-10-16, après la rédaction du plan) les opcodes 0x2D, 0x8A, 0x8B (`EventProgramRunner.cs:2367`, `:2378`, `:2406`, `:2445`) |
| `SwitchedAtCreation` | `:1392` | `EntitySpawnFactory.cs:548` et `WorldProxy.cs:3425` (naissance par script d'un enregistrement) |

- À la rédaction (HEAD `be9a067`), « née par l'interface » équivalait à `JoinsBirthTick`. Ce n'est plus vrai.
- **INFÉRENCE** : un prédicat qui lit `JoinsBirthTick` changerait dès la phase A le mouvement des entités nées par script en production, à chaque animation demandée au milieu d'une image. Cela contredit « phase A sans changement de comportement » (l.5986) et risque les « cinq traces » de l'acceptation.
- Seul `HasLogicalZ` est compatible avec D-F5-1 au HEAD.

### O2. Si f5 lit `HasLogicalZ`, g1a est aussi touchée

`PutInHands` de g1a pose `HasLogicalZ` (D-G-13, l.6567-6568). La règle de f5 couvrirait alors les objets portés et relâchés **dès g1a**. Or le texte READY de g1a (§1.24.1) ne cite ni f5 ni la règle de vitesse ; par exemple (w8) relâche l'objet en animation 3. Le choix « autre voie » de Q-G1A-4 (l.7521) se décide donc dans f5 et doit être reporté dans g1a et g2a. **INFÉRENCE**, rien n'a été mesuré.

### O3. La phase A est inerte pour la vague, et les données le prouvent

Le rapport (A3) laisse ce point « à prouver ». Les données suffisent :
- `sprite-records.json` `alundra_27` à `alundra_29` : animations 0 et 1 de vitesse 0, fins `Hold`, aucune `Chain` ;
- le plan dit que la vague « ne bouge jamais (forces 0, `ForceAdjusted` 0) » (l.5354).

Avec `HasLogicalZ`, la règle ne change donc rien en production à la phase A. Il reste à le réécrire comme justification.

### O4. La fenêtre du mur des flèches n'est pas celle de f4

- `sweep_wall.txt` (type 3) donne S+41 à **S+44** (x0 = 98 → S+44).
- Le texte de f4 dit S+41 à S+43 pour le type 7 (l.5748, l.5825).
- D-F5-7 renvoie à D-F4-9 : la **règle** (« arrivée + 1 », mêmes exceptions x0 = 98, 101, 104, 107) se transpose, je l'ai vérifié par calcul pour les types 3 et 7. Les **bornes**, elles, ne se transposent pas, et le tableau de f5 (l.5999) ne les donne pas.

### O5. Commentaires périmés oubliés dans `PlayerManager.cs`, fichier que f5 modifie

- `:1198` : « empty until E20.f3c » ;
- `:1207-1208` : « The set is empty in this slice, so nothing is born in production » ;
- `:1228` : « the production set stays empty ».

Ils sont périmés depuis f3c, et f5 est dans ce fichier. Petite correction : la doc du registre du runner est à `EventProgramRunner.cs:48-50` (la l.47 est vide).

### O6. Contradiction interne du §1.24 que le rapport ne relève pas

- Le bloc « Révision au HEAD » fait jouer (b3) de g2b sur un banc qui rallume les natifs, parce que D-F4-7 manque (l.6444-6446).
- §1.24.3 exige pourtant « **E20.f4 ✅** (D-F4-7) … sans l'une d'elles, arrêt » (l.6618).
- Pour f5, la même alternative existe (banc vidé d'E20.e ou interrupteur de f4) : elle décide si la dépendance à f4 est vraiment ferme.

### O7. Risque de B1 (`AlundraMapEdgeWorldTests.cs:120-160`) sous-estimé

- Avec E17, la flèche proche rebondit au tick 8 vers l'ouest (−1,875 px par tick depuis 1 237). La flèche lointaine, sur le même Y (`:53`), vole vers l'est : 1 103 + 6(t−1). Elles se croisent vers les ticks 19 et 20 (calcul).
- Le test exige `ForceAdjusted` = 0 sur 20 ticks pour la flèche lointaine.
- B3 (`:241-268`) montre qu'aujourd'hui une flèche et un marcheur au même X ne se bloquent pas. Le risque est donc faible, mais pas « écrit pour cela ». **INFÉRENCE**.

### O8. K1 (`AlundraMapStateWorldTests.cs:108-123`) : risque de la flèche libre confirmé

`free.X.Max()` attend `100 + 3 + 6·59` px. Avec E17, une touche (cible de classe A de 17 px ou plus sur y = 100) détruirait la flèche et casserait l'égalité. Avant f5, une touche ne fait que passer au statut 3, avec un E17 qui ne fait rien. **INFÉRENCE**, à mesurer au T0.

### O9. Points mineurs

- **Prérequis** : §1.21 omet E20.e0a5, que l'enveloppe cite (l.717).
- **Tir en l'air de William** : la fiche w9, sous-fiche 2 (`WeaponTables.cs:66` : `0x17, 0x04, 0x08, 0x10`, délai 8), ne figure pas dans les scénarios ; seul le tir en l'air du chasseur y est. **INFÉRENCE** : S = P+9 en l'air pour William.
- **Compte du registre d'e0c** : e0c écrit « le compte passe de 7 à 9 », avec réécriture par dérivation (l.7014). Si f5 passe avant, ce sera 8 → 10 : sans blocage, mais le texte d'e0c suppose qu'il passe avant f5.
- **Sorties durables qui lisent les oracles des flèches** : il manque `AlundraLogicalZStepTests.cs:168` et `AlundraMapEdgeWorldTests.cs:215`, `:278`.
- **Registre** : `AlundraNativeSpriteRegistryTests.cs:211` inscrit aussi (F, `SaveBook.InteractCode`), sans conflit avec (E, 17).
- **`checksums.txt`** est dans `e20-f3/oracle/`, pas dans `tools/`.
- **Clause d'origine de S5** : la « quatrième clause » du §1.20 était `Assert.Empty(ProductionWiredWeaponTypes)` (`9ecc7d5`, ligne 238 du test), remplacée par l'égalité `:243` de f3c.

## Manques bloquants confirmés ou ajoutés

**Confirmés** (numérotation du §6 du rapport) :

1. **(§6.1) Prédicat de D-F5-5 non nommé.** À compléter par O1 et O2 : trois candidats ; la phase A n'est inerte qu'avec `HasLogicalZ` ; la décision touche g1a et g2a.
2. **(§6.3) Fausse justification « aucune entité marquée en production ».** Elle est à réécrire, mais les données rendent la phase A inerte (O3).
3. **(§6.4) Le héros est déjà dans `SpawnedEntities`.** Preuves : `WorldProxy.cs:2302-2305`, `IEntityWorldContext.cs:29`.
4. **(§6.6) Dépendance à f4 non satisfaite.** D-F4-7 est absent (`rg` vide), comme D-F4-8 et l'annexe `e20-f4f6`. À trancher avec O6.
5. **(§6.7) Garde des fichiers neufs Q-R-7 encore à l'état de proposition.** Preuves : `docs/plan-restructuration-dll.md:1234`, `check_names.py:96-98`.
6. **(§6.8) Preuves seulement dans le scratchpad.** Elles sont là (`disc-e20f4f6/bin/*.py`, `out/json`), mais aucune consigne ne les copie dans le dépôt.

**Ajoutés :**

7. **Liste prédite conditionnée par l'exécution réelle de f4** (E3). Le premier bloc de S1, l'égalité de S5 et ses clauses négatives sont à relire au HEAD de départ. La preuve de retrait (l.6040-6041) et l'arrêt « un test hors de la liste » ne peuvent pas être fixés avant.
8. **Prédictions de la sonde de vitesse manquantes à 2 et 3 ticks par image.** C'est l'A2 du rapport, qui en fait un point non bloquant (§6.10). Je le rends bloquant : l'arrêt « le décalage excède la prédiction » (l.6047-6048) se déclencherait à tort. Valeurs calculées, que j'ai refaites :
   - rebond au 1ᵉʳ tick d'une image de 2 ticks : −2,25 px ;
   - au 2ᵉ tick : +1,875 px ;
   - au 1ᵉʳ tick d'une image de 3 ticks : −6,375 px.

   Il faut aussi ajouter la borne S+41 à S+44 de la fenêtre du mur (O4).

**Reclassés « à préciser »** (E8) : §6.2 et §6.5.

## Confirmé (comptes)

| Partie | Juste | Détail |
|---|---|---|
| §0 | tout | Annexe `e20-f4f6` absente ; scratchpad présent. Tableau l.5990-6004 identique à `arrow3_east_wall.json` (statut, px, pz, fx, fz, adj, dir, anim, flags, box, `reset_AC`, son). Les 9 scénarios résumés sont exacts. `sweep_wall.txt` : 43, 43, 44, 43, 42, 43, 42, 42, 43, 42, 41, 42, et le modèle « arrivée + 1 » vérifié |
| 1.A | 10 / 10 | `HitPass.cs:119-141` (Width/Height/Depth à `:142-144`), `ScriptedMotion.cs:171` et `:175`, `PlayerManager.cs:1226`, S5 `:238`, S1 `:83` et `:92`, P5 `:241-251`, P3 `:150`. Les lignes anciennes sont confirmées à `be9a067` et `9ecc7d5` |
| 1.B | 25 / 25 | Symboles et lignes |
| 1.C | 6 / 6 | |
| 1.D | 9 / 9 | Groupes de citations. Les commits relevés sont exacts pour les 9 fichiers que j'ai contrôlés |
| §2 | 18 / 20 entièrement justes | A5 juste avec une valeur fausse (E7) ; A20 à moitié faux (E2). A2 recalculé : 393 216 → 0 au lieu de −122 880, et valeurs à 2 et 3 ticks exactes |
| §3.A | 5 / 7 | S1 conditionnel (E3) ; liste des commentaires incomplète (O5) |
| §3.B | 3 / 3 | |
| §3.C | 9 / 12 | E4, E5, O7 |
| §4 | 9 / 10 | Citation de §4.1 (E6) |
| §5 | 6 / 6 | `ArrowAi` sans homonyme dans les `.cs` du checkout (`rg`) ; `Alundra/Ai/Projectiles/` existe |
| §6 | 6 bloquants confirmés sur 8 | 2 reclassés (E8) ; 13 en partie faux (E2) ; 9 à 12 et 14 à 16 justes |

## Inconnus

- **Comportement réel de la DLL** : rebond, mort à S+60, tick de `reset_AC` à 1, 2 et 3 ticks par image. Rien n'a été exécuté.
- **Forme exacte du recouvrement d'E17.** Le plan dit « inclusif » ; je ne l'ai pas redésassemblé. Rappel : la DLL a déjà deux formes, celle de `HitPass.cs:63-70` et celle de `HitOpcodes.cs:76-78`.
- **Blocage physique entre deux flèches** (O7) et **cible de classe A sur le trajet libre de K1** (O8).
- **Absence d'enregistrement de type 3 à 6 dans les 483 `.tileMap`** : affirmée par le rapport, non refaite.
- **Effet d'e0c sur la mort d'une flèche.** Le §1.26 dit « si item == 0, rien » (l.6892) et l'en-tête a `Contents` 0. Je n'ai pas vérifié que `LootRoll.Roll(hasRecord: false, …, HeaderContents 0)` donne bien l'objet 0.
- **Boîtes d'animation de la flèche.** L'oracle les confirme (`bw` 1 572 863, `bd` 1 048 575, `bh` 720 895 → 24×16×11), mais je n'ai pas relu l'export des cadres de la DLL.
- **`check_names.py`** : je ne l'ai pas relancé ; le résultat « 0 finding, 1 avertissement » vient du rapport.
