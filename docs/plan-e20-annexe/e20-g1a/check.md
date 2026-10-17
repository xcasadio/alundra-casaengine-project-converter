# Critique du rapport g1a — verdict : fiable pour écrire les tâches, avec 6 corrections

Rien n'a été modifié dans le dépôt ni dans le binaire. Mes fichiers sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/check-g1a/`.

## 0. Ce que j'ai refait
- **Adresses :** j'ai redésassemblé moi-même (capstone) toutes les adresses citées, soit environ 25 fonctions ou sites. Je n'ai pas relu les listings du premier agent.
- **Modèles indépendants :** j'ai écrit mes propres modèles (`myvec.py`, `myvec2.py`) et mon propre générateur de vecteurs. Ils couvrent `StepTowards`, `TryLift`, `PlayerTryAction`, `UpdatePlayerCarriedEntity` (modes 0, 1, 2 et 255), C23, E2 et `DestroyEntity` à un argument. Résultat : **0 écart** sur 3 000 vecteurs par mode (6 000 pour C23, E2 et `Destroy1`), couverture 100 % des mots.
- **Une erreur de ma part, corrigée :** ma première version avait une assertion vacuante (`... or True`). Elle masquait les écarts d'`UpdatePlayerCarriedEntity` sur les modes 0 et 1. Après correction, j'ai tout relancé et le résultat ci-dessus est celui de la version corrigée.
- **Mutants :** j'ai écrit mon propre générateur (236 mutants). Les survivants sont des `move → subu`, un `andi 0x600 → 0x601` (le bit 0 est décalé hors du champ), le `sltiu 4` mort, le comptage `0x4360` de `PlayerTryAction` (équivalent), et des `slt/sltu` qui ne diffèrent qu'à |d| = 2³¹. Les mutants de seuil (`0x1FFFF`, 5) sont tués dès 4 000 vecteurs, mais pas à 150.
- **Égalité du mode 0 :** mes vecteurs aléatoires ne tuent pas le mutant `m <= e`. J'ai donc testé l'égalité à la main (`myeq.py`) : à m = e, X, Y et Z avancent tous les trois, comme le rapport l'affirme.
- **Goldens :** je les ai rejoués sur le vrai code dans ma propre machine (`replay_gold.py`) : 0 écart. Le golden `MoveEntityCarriedBranch` n'est valide que si 0x800375E0 et 0x80037730 sont stubés à 0, comme dans leur harnais. Sans ces stubs, 29 vecteurs sur 50 divergent.
- **Autres vérifications :**
  - J'ai recopié et relancé leur balayage `sweep_carry.py` : 4 000 vecteurs, 0 écart, 129/129.
  - Leur `machine.py` est identique à celui du dépôt.
  - Leurs 55 sorties d'oracle sont identiques (SHA-1) à celles du relevé précédent (`disc-e20g/bin/out`).
  - Les séries du mode 0 et du mode 1 rejouées sur le vrai code donnent les mêmes valeurs que le rapport.
  - J'ai recompté les données (carte, prefabs, `balance.json`) avec mon propre script.

## 1. Binaire — affirmation par affirmation

**Confirmées (désassemblage et exécution)**
- **`TryLift` 0x8002EDBC :**
  - Genre = `(+0x6C & 0x600) >> 9`. Genre 0 : retour 0. Genre 1 : soulève. Genres 2 et 3 : `GetNumberOfItem(0x3B)` à 0x8002EDF8, retour 0 si l'objet est absent. Le `sltiu 4` est mort.
  - Écritures aux adresses 0x8002EE0C et 0x8002EE1C. Le `sw` de 0x8002EE1C est dans le délai de `bne` : il est exécuté dans les deux cas. Valeurs 5, 0x0B, 0x0C, 0x30 correctes.
  - Aucun test de distance, de regard, de statut, de taille ni de Z.
  - Seuls appelants : 0x8002EF2C et 0x8002EF9C.
- **`PlayerTryAction` 0x8002EEAC :**
  - Si le héros porte déjà, il force 0x0C ou 0x30 et renvoie 1.
  - Avec un contact `+0x130` et un front de Carré, il renvoie directement `TryLift`, même si c'est 0.
  - Sinon la boucle va de 0 à `[0x80134360]` inclus (statut 2 ou 3, `+0x20 == 0`, `+0x12C == héros`, aucun bouton). Un compte négatif sort tout de suite avec 0.
  - Les 6 sites d'appel sont exacts, et il n'y a aucun appel indirect.
- **`TryUseItem` 0x8002ED64 :** Rond = 0x20. Retour 1 sans front. Avec front : son 3 si `[0x800DC4B8] != 0`, sinon appel de 0x8003499C.
- **`StepTowards` 0x8002F854 :** comparaison non signée de |tgt − cur| avec le pas, `cur − pas` si l'écart est nul ou négatif.
- **`UpdatePlayerCarriedEntity` 0x8002F884 :**
  - Les trois modes et le passage du mode 1 au mode 0 quand `AttackCount == 0`.
  - Mode 0 : seuil `m < e` (égalité comprise), écriture brute de dx et dy dans le cas Z seul, compteur remis à 0.
  - Mode 1 : compteur signé `< 5`, seuil 0x1FFFF, compteur gardé, appel de 0x8003A774.
  - Mode 2 : `+0x24 = −1`, `+0x8C`, position + 0x200000, `+0x2C` non effacé.
  - L'objet à la tête (0, 0, 32 px) meurt au **22ᵉ appel** (`attack = 1`) et ne meurt jamais avec `attack = 0`. Un objet à 21 px de décalage au soulèvement meurt au 16ᵉ appel.
  - Série du mode 0 : X baisse au tick 12, (0, 0, 32) au tick 32, Y à 18 pour ±15 px.
- **Les 6 sites d'appel et leurs modes :**

| Site | Mode | Contexte |
|---|---|---|
| 0x80031BF4 | 1 | verrou |
| 0x80031C64 | 2 | blessure |
| 0x80031D78 | 0 | tick libre |
| 0x8003261C | 2 | nage |
| 0x80032778 | 1 | case de sortie |
| 0x800327AC | 2 | mort |

- **Groupe d'états de portage :**
  - La table des états (0x80022D3C) est exacte : 5 → 0x800322F0, 0xB → 0x800322B4, groupe → 0x8003232C, 9 → 0x80031E18, 0xA → 0x80031DF8.
  - `hero.+0x8C = s1` dans le délai de `jal TryUseItem`.
  - Ordre des branches : Carré (garde `+0x180 & 0x2000`, table `{0, 0x10, 8, 0x18}` indexée par `+0x98`, `t24` 1/2 au sol et 2/3 en l'air), puis genre 3 (0x0C ou 0x30), puis Croix (garde 0x4000) avec 6/0x2E ou 7/0x0C, puis 0x2F/0x30 en l'air.
  - Pas de « poser ».
  - Garde d'échelle en 0x80031D54.
- **Écritures de `+0x28` et `+0x2C` :**
  - Trois écritures non nulles seulement : 0x8002EE0C/1C, 0x80034298/9C (haricots), 0x800345F8/FC (bombe).
  - Les effacements listés sont exacts, y compris les 3 queues de `DestroyEntity` : 0x8003A62C, 0x8003A74C, 0x8003A7A4.
  - Les autres sites des offsets 0x28 et 0x2C sont des structures hors entités : tween en 0x80059E…, structure de warp 0x800DC4C0, fenêtres/UI, `jmp_buf` 0x8008B…
- **C23 / E2 / `DestroyEntity` :**
  - C23 : tables `{0, 3, 1, 4}` aux trois adresses, drapeaux de C23/C70/C73/C4 exacts.
  - E2 est bien en **0x8007ED30** ; 0x8007ED10 est E0/E1 (la table E de l'EXE le confirme).
  - Les trois `DestroyEntity` : 0x8003A59C (loot d'abord), 0x8003A648 (récursive), 0x8003A774.
- **Suivi de l'objet porté :** `MoveEntity` 0x80037E34 (branche porteur), remise à zéro des forces en 0x80036A84, exclusion en 0x80036C60 et 0x80037F54, clé de tri en 0x8003981C, décalage à l'arrivée en 0x80031498, `UpdateRidingEntity` 0x80037364, ordre de la passe physique en 0x80038364.
- **Choix du programme E :** conditions `0x10`/`0x20`/`0x40`, eau (6) et `0x8004` (−1) exactes.
- **Haricots et bombe :** refus si `+0x2C != 0`, `+0x18C == 4` ou `[0x80127164] != 0`. Bombe : au plus 3, `+0x274 = 0xF0`. Retours 0/1 exacts.
- **Passe de coups 0x80039300 :**
  - Masque de classes, `+0x1D4`, `+0x1C8`, `+0x224`, `+0x1D0 = 0x19`, `+0x220 += 1` exacts.
  - Le héros (flags `0x8C`) n'est pas de classe A. Le Beannoïde (flags 3) l'est.
  - Dégât mesuré avec le vrai code (`mydmg.py`) : caisse (1, 128) sur un Beannoïde de 9 PV donne 0, et 200 PV donne 72.
- **Test de zone du spawner (O-G-10) :** en 0x8003A200-0x8003A25C, confirmé.
- **Données :**
  - `AnimSets` du héros : fiches 5/6/7/46/47/48 à `Acceleration` 0, fiche 1 à 1, `Sfx` 44/10, impulsion 1280.
  - `AttackCount` de chaque famille (297, 363, 367-369 et haricots à 0).
  - 17 prefabs (16 + 1) ; 18 cartes de la chaîne avec 219 placements.
  - Fiches de la caisse : vitesses 128/256/384, impulsion 1024, boîte 24×16×16.
- **Comparaison avec la décompilation :** celle-ci inverse le test de l'objet 0x3B et traite l'égalité du mode 0 autrement que le binaire (`PlayerManager.cs:1105`, `UpdatePlayerCarriedEntity`). Le rapport suit correctement le binaire, mais il ne dresse pas lui-même cette liste d'écarts.

**Réfutées, ou inexactes**
1. **« 850 vecteurs » (§2) :** c'est **950** dans `goldens_g1a.json` (200+100+100+250+100+100+50+50). Les 700 du groupe sont corrects.
2. **§3.9 « écart d'une caisse ou jarre, non investigué » :** l'écart est la **bombe** (sprite commun 2, carte 362, un placement). Preuve : 2 244 (en-têtes des cartes) + 7 (pierre 234) + 1 (bombe) = 2 252. Le plan compte 2 244 + 7 = 2 251 sans la bombe. Le tableau du rapport contient lui-même la ligne « Bombe 1 ».
3. **§0.2 / §3.2 :** `h-g-deps.md:44` et `:170` disent déjà « carried » (objet **porté**) pour la règle des 5 ticks. Seule la formulation de O-E20-19 (« objet lancé… quand le héros le rattrape ») l'attribue à tort à un objet lancé. La correction du rapport est juste sur le fond mais mal attribuée.
4. **§1.4 :** 0x80035214 et 0x800352D4 ne « refusent pas quand on porte ». Ils lisent `héros.+0x2C` puis testent si le `+0x68` de l'objet porté vaut 1 (haricots) ou 2 (bombe) : ce sont les pilotes de l'objet tenu. Seul 0x800348F8 (départ d'une magie) refuse (`bnez → 0x80034978`).
5. **§5, types à enregistrer :** le type **35** manque pour (C, 23) et (E, 2). Il s'agit d'un placement de la carte 231, de `SpriteDirection` 64 (bit 0x80 absent), donc type brut 35, avec un en-tête de caisse dans la table de la carte. Il est compté dans les 1 910 mais pas dans la liste. C'est une INFERENCE à confirmer ; elle ne concerne qu'un placement.
6. **§1.9, « un objet porté ne blesse personne » :** vrai pour les caisses, jarres, herbe et pierre (le prefab n'a de boîtes de collision qu'aux animations 1, 3 et 4). Le prefab de la **torche** a des keyframes de collision à l'animation **0**, sa fiche porte une attaque (6/6/1), et le rapport ne l'examine pas. Pour la torche : UNKNOWN. Cela ne touche pas g1a/g2a, car la torche est g2c.

**Non vérifiable par moi**
- Les cibles de chaîne d'animation (5→12, 6→47, 10→45, 11→48, 46→48, 9→0). Les types d'animation Once/Loop du prefab sont cohérents, mais les cibles viennent de l'oracle.
- Les tableaux de l'oracle de plateforme au-delà du fait que le harnais met le héros sur la plateforme et que le vrai code porte les deux (`g5_platform.py`).
- Le contenu du gabarit d'entité 0x80134368, hors de l'image de l'EXE. La boucle 0x8003B24C-0x8003B2C8 copie bien un gabarit de 0x294 octets sur les 64 cases, puis recrée le héros (0x80031974). Le Q6 du rapport reste une INFERENCE.

## 2. Faits sur la DLL (HEAD 2010ec3)
**Tous exacts :**
- Bouchons `:919`, `:927`, `:1272`. Appels `:395` (mode 0) et `:646` (mode 2). Branche verrouillée `:322-335` sans mode 1.
- Sites de `PlayerTryAction` `:419`, `:438`, `:743`, `:787`, `:848`, absent en `:444`.
- Garde d'échelle `:386`. Doc d'`EntityPickupKind` inversée (`EntityFlags.cs:274-290`).
- `PlatformEntity`, `CarriedEntity` et `RelativeWarpOffset` jamais écrits en production (12 écritures dans les tests).
- Les deux `DestroyEntity` (`AlundraWorldProxy.cs:3267`, `:3295`) sans effacement du porteur. Le recyclage (`:3231`) n'efface aucune référence.
- `EntitySupport.cs:44-59` et `:72-75`, registre à 7 clés (test `:51`), `PickupKindMask` écarté (`AlundraMovementObstacleProbe.cs:48-49`), `TickScriptedNpc` lit `CurrentAnimationId` (`:165-170`), `LastTickDelta` hérité (`:671-675`).
- Ordre du tick libre : l'appel du mode 0 vient bien après les deux fonctions d'armes, comme en 0x80031D68-0x80031D78.
- Les tests déplacés cités (`CaseSixGate_CarryingEntity…`, les deux fichiers « oracle » avec `platform_28`, `AlundraNpcCharacterControllerMoverTests.cs:2210`, `TheNinePresets…`) existent à ces lignes.
- Le test d'échelle `:660-669` donne en effet 0x0C/0x30 et non l'escalade. Le risque de `NullReferenceException` avec un `Entity` nu est réel.
- Le test `:2210` n'appelle que des recherches, sans tick : W3 ne le touche pas.

## 3. Ce qui manque au rapport
- **États 0x05 et 0x0B :** ils appellent `TryUseItem` en premier (0x800322B4, 0x800322F0) et n'écrivent pas `+0x8C`, contrairement au groupe 0x8003232C. Le tableau §1.5 ne le dit pas, et la phase B (W4) doit reproduire cette différence.
- **Balance nulle :** le binaire déréférence `carried.+0x1C4` sans test (`lbu 0xE(…)`). En C#, `Balance` peut être nul (« cas dégradés » de `AlundraEntityScriptProxy.HitPass.cs:21`) : il faut choisir et tester le comportement (traiter comme `AttackCount == 0`).
- **|d| = −2³¹ :** le binaire l'accepte (le `negu` laisse INT_MIN, traité comme petit par `slt`). `Math.Abs(int.MinValue)` lève en C#. C'est inatteignable (±2048 px) mais à garder en tête pour les vecteurs.
- **Branche verrouillée :** l'ordre exact du binaire est `0x8002FB14(1)`, remise à 0 de 0x80126FFC et 0x80127110, mode 1, puis item cancel 0x800350C0. Le rapport cite l'item cancel pour h0 mais ne donne pas l'ordre pour W2.
- **Dégâts (cf. 1.6 ci-dessus) :** dire que « porté ne blesse personne » est prouvé pour les caisses seulement.
- **Passe de coups :** une cible immune (`réponse & 0xC0 == 0x80`) saute l'écriture de `+0x224` mais pas `+0x1D0` ni `+0x220`. La DLL le porte déjà ; le rapport l'omet.
- **Registre :** les types (E, 0) des quatre « objets » (363, 367, 368, 369) seront nécessaires en g2b/g2c.

## 4. Conclusion
Les faits binaires de g1a et g2a sont solides. Je les ai retrouvés par désassemblage, par modèles écrits indépendamment, par mutants et par rejeu de l'oracle. Je n'ai trouvé aucune erreur qui change une décision du plan : timeline, seuils, ordre des branches, tables, drapeaux, suppression du porteur par les trois queues de `DestroyEntity` et O-E20-19 sont tous corrects.

On peut écrire les tâches du plan à partir de ce rapport en appliquant les six corrections de la §1 :
- 950 goldens, et non 850 ;
- l'écart de comptage est la bombe ;
- la règle des 5 ticks n'est mal attribuée que par O-E20-19 ;
- le rôle exact de 0x80035214 et 0x800352D4 ;
- le type 35 (carte 231) à ajouter ou à écarter ;
- « porté ne blesse personne » limité aux caisses, jarres, herbe et pierre.

Il faut y ajouter les omissions de la §3, surtout `TryUseItem` en tête des états 5/0xB, la balance nulle, et le stub de récursion à documenter pour le golden `MoveEntity`.