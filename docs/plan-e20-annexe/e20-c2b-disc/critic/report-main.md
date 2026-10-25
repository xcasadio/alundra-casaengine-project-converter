# Critique fraîche de la découverte E20.c2b (lecture seule)

## 0. Cadre

- **Ce que j'ai fait.**
  - J'ai relu le binaire `ALUN_CD.EXE` (France) avec capstone, via une copie de `bx.py`, et j'ai balayé l'image entière.
  - J'ai relu la DLL et les tests uniquement par `git show HEAD:` et `git grep HEAD`.
  - J'ai relu les `docs/` et les JSON de `data-extracted/` directement.
  - Je n'ai rien écrit dans le dépôt, rien commité, et lancé aucune construction ni aucun test. Seuls mes scripts et sorties sont dans mon dossier.
- **Écart de HEAD.**
  - Les rapports citent `e9e8c58a`, le HEAD actuel est `ffc505b1`.
  - Entre les deux, le code de production est identique. Seuls changent un test (`Alundra.Tests/Ai/Objects/ChestAiObjectTests.cs`, +15 lignes), un ADR et des documents.
  - Les numéros de ligne des rapports (`PlayerManager.cs` 129, 144, 323, 337, 354, 358, 642 ; `WorldProxy.cs` 2914, 2969, 3026, 3040, 3099) sont exacts.
- Dossier de travail, noté `<C>/` : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/c2b-disc/critic/`.
- Le texte du rapport DLL et du rapport plans m'est arrivé tronqué. Je ne juge que ce que j'en ai lu et ce que j'ai revérifié moi-même.

## 1. Verdicts sur les faits porteurs

### 1.1 Binaire

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| A1 | `0x80031A68` : 58 instructions (`…31B4C`), héros fixe `0x80127D30` (aucun paramètre), deux branches. Toucheur (`+0x224 ≠ 0`) : `ComputeNewHp(hero)` (retour ignoré), puis `GetDirection(hero.+0x114 − t.+0x114, hero.+0x118 − t.+0x118)`. Sinon branche case dangereuse. | **CONFIRMÉ** | Désassemblage `0x80031A68-0x80031B4C`. |
| A2 | Appelants : exactement deux `jal`, en `0x80031C14` et `0x80031C44`. Aucun `j`, aucun mot de données, aucune paire lui/addiu ou ori qui matérialise l'adresse. | **CONFIRMÉ** | Balayage des 0xAB000 octets de l'EXE : `<C>/out_callers_80031A68.txt`. Aucun overlay de code dans le dossier extrait (LOADER, CLOSING et END sont des programmes séparés). |
| A3 | Chaîne : `MovePlayer 0x80031B50` ← `0x800386EC` ← passe d'événements `0x800386D0` ← `0x8003B3A8` ← répartiteur `0x8003B388` ← `0x8002E108`. Un seul appelant à chaque niveau. | **CONFIRMÉ** | `<C>/out_callers_chain.txt`. |
| A4 | Un seul bloc de blessure `0x80031C58-0x80031C9C`, partagé par les deux branches. `0x80031C64` est le `jal 0x8002F884` avec `a0 = 2`. Le pilote d'objet `0x8003634C` est appelé avant d'écrire l'état. | **CONFIRMÉ** | Listing `0x80031C44-0x80031C9C`. `0x8002F884` a six sites d'appel : `…1BF4` (mode 1), `…1C64` (mode 2), `…1D78` (mode 0), `…261C` (nage), `…2778` (case de sortie), `…27AC` (mort). |
| A5 | Entrée supprimée : `[0x800DC4B8] & 0x48` (le répartiteur n'appelle pas `MovePlayer`) ; `hero.+0x20 ≠ 0` ; `ctl & 0x34` avec mot ≠ `0x20` exactement. Mot = `0x20` : entrée sans animation, `+0x1CC = 0x78`, pas de test de mort. | **CONFIRMÉ** | `0x8003B394`, `0x80031BA4`, `0x80031BD0`, `0x80031C0C` (`bne` sur le mot entier), `0x80031C14-0x80031C2C`. |
| A6 | Dégâts du toucheur : `0x8004464C(attaque = t.+0x1C8, fiche, PV)`. Fiche = `0x80127008` pour le héros, `+0x1C4` sinon. Réponse `r = fiche[(attr & 0xF)+2]` : `0x40` met les PV à 0, `0x80` et `0xC0` les laissent, sinon `max(1, (puissance × r) >> 4)` et plancher 0. Le bit `attr & 0x80` ajoute la puissance des 3 fiches d'équipement. Le retour « PV = 0 » est jeté. | **CONFIRMÉ** | `0x8003A374` et `0x8004464C`. Une réponse `0x80` donne donc une blessure complète sans perte de PV. |
| A7 | Case dangereuse : exige `+0x224 = 0`, `+0x1CC = 0`, niveau des bottes `[0x80127000] < 3`, coût `(+0x180 & 0x180) >> 7 ≠ 0`. Table `0x80028B8C = {0,1,2,3}`, plancher 0 signé, armure ignorée. Direction `(+0x8C + 0x10) & 0x1F`. | **CONFIRMÉ** | `0x80031ABC-0x80031B34`. Table lue : `[0, 1, 2, 3]`. |
| A8 | Le mot `+0x180` (OR des quatre coins) n'est calculé que si `Flags & 0x100`. | **CONFIRMÉ** | `0x800380FC-0x80038108` (sinon saut à `0x80038274`, qui met `+0x180` et `+0x184` à 0). `MovePlayer` pose le bit à chaque tick libre (`0x80031C30-0x80031C38`). |
| A9 | État posé : `0x3A` si la pente lue en `0x80031B98` (avant `CheckAndExecuteWarp`, donc celle du tick précédent) vaut 4, sinon `0x31`. Direction = valeur de retour. | **CONFIRMÉ** | `0x80031C74-0x80031C94`. Le seul écrivain de `+0x18C` est `0x80038348` (passe physique). |
| A10 | Enchaînement : `0x31` → `0x1C` → tenue puis direction `+0x10`. `0x3A` → `0x3B` → tenue. Le gestionnaire terrestre de `0x31` est `0x80032680` (`TryUseItem` puis fin). `0x1C` et `0x3E` partagent `0x80032558`. | **CONFIRMÉ / MESURÉ** | Frame terminateur de chaque animation : octet 1 = 28 (`0x1C`) pour `0x31`, 59 (`0x3B`) pour `0x3A`, 128 (tenue) pour `0x1C` et `0x3B`. La table de sauts `0x80022D3C` donne les gestionnaires. |
| A11 | `+0x1CC = 0x78` (120). Le compteur baisse de 1 par tick et par entité, héros compris, dans la passe `0x80038998`. Le tick de la blessure finit donc à 119, et une nouvelle blessure par case dangereuse revient à T+120. | **CONFIRMÉ** | `<C>/out_recoil_a1.txt` : `c1cc` = 119, 118, 117… Il y a 21 `sw` sur `+0x1CC` (`<C>/out_1cc_store_sites.txt`) : le héros écrit `0x78` (`…1C2C`, `…1C9C`, C48 `…4D98`), les monstres écrivent `0x5A` (90). |
| A12 | `+0x1D0 = 25` a un seul écrivain, la passe de coups (`0x80039670`). Les deux `sh` sur `+0x1CC` et `+0x1D0` en `0x800978DC` et `0x80097924` ne touchent pas une entité : ils écrivent dans une structure de demi-mots du code graphique, via `[0x800D9D88]`. | **CONFIRMÉ** | `<C>/out_1d0_accesses.txt`. |
| A13 | La passe de coups ignore une cible si `+0x1D0 ≠ 0`, `+0x1CC ≠ 0` ou `+0xB4 & 0x40`. L'immunité (`(r & 0xC0) == 0x80`) se lit sur `cible.+0x1C4`, **pas** sur `0x80127008`. Une cible immune garde `+0x1D0 = 25` et les étincelles, mais n'écrit pas `+0x224`. | **CONFIRMÉ** | `0x80039414-0x80039448` et `0x800395D4-0x80039670`. |
| A14 | 28 animations du héros portent `AnimFlags & 0x40`, dont `0x31`, `0x1C`, `0x3A`, `0x3B`, `0x3C`, `0x3E`. | **CONFIRMÉ** | `<C>/out_anim_0x40.txt`. `Acceleration` du JSON est bien l'octet `0xD` (`AnimationSet.cs:10`), copié dans `+0xB4` en `0x80038B68-0x80038B70`. |
| A15 | Clignotement : l'entité n'entre dans la liste visible `0x80134250` que si statut ∈ {2,3} **et** `(+0x1CC & 3) ≠ 3`. La règle est générique, pas propre au héros. 30 ticks cachés sur 120, le premier au tick de la blessure. | **CONFIRMÉ** | `0x800385D0-0x800385F4`. L'oracle A1 donne `visible` = 0,1,1,1,0,… |
| A16 | C89 (`0x8006295C`) force le héros à `+0x88 = 0x31`, `+0x8C = 0`, `Flags \|= 0x108`, sans toucher aux PV ni à `+0x1CC`. | **CONFIRMÉ** | Seconde écriture de la constante `0x31` dans une zone `+0x88`, avec `0x80031C84`. Rien d'autre n'écrit `0x31`, `0x3A` ni `0x1C` en constante. |
| A17 | HUD : les cœurs baissent sans son. Le son 8 joue à la remontée des PV, le son 9 à celle des PM. | **CONFIRMÉ** (forme) | `jal 0x800490FC(8)` en `0x8004C2F8`, dans la branche « affiché < réel ». Voir §4 pour le rythme. |

### 1.2 DLL (HEAD `ffc505b1`)

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| B1 | Aucune partie du chemin de blessure n'existe dans la DLL : ni entrée, ni bloc, ni chemin `== 0x20`, ni case `0x31` sur terre. `git log --grep=c2b` ne rend que des commits de plan. | **CONFIRMÉ** | `PlayerManager.cs` lignes 354 → 355 → 358 → 377 : `Gravity`, `Create(FreeMode)`, `UpdateItemUseDriver`, nage. Point d'insertion : entre 355 et 358. |
| B2 | Le héros est déjà cible de la passe de coups. Elle écrit `TouchingEntity`, `FrameCollisionTickCounter = 0x19`, trois étincelles, et ne touche jamais aux PV. | **CONFIRMÉ** | `Combat/HitPass.cs`, `WorldProxy.BuildHitList` (3099). |
| B3 | Aucun lecteur de production de `TouchingEntity` du héros. | **CONFIRMÉ** | `git grep` : lecteurs `HitOpcodes.cs:30`, `Damage.cs:81`, `EntityScriptProxy.cs:1968`, effacement `WorldProxy.cs:3060`. |
| B4 | `Damage.ComputeNewHp` est pure et a un seul appelant de production (`Ai/EnemyHelpers.cs:232`). Son commentaire « no production code calls them yet » est périmé. Le port reproduit le binaire ligne à ligne (équipement `Attacks[g+1<count ? g+1 : 0]`). | **CONFIRMÉ** | `Combat/Damage.cs`. |
| B5 | Aucun clignotement dans la DLL ni le moteur. Trois écrivains de `Entity.IsVisible` existent : `FrameSyncPasses.cs:109`, `PickupAi.cs:285`, `ChestAi.cs:88`. Le moteur honore le drapeau (`Entity.Draw` retourne tôt, ligne 546). | **CONFIRMÉ** | Pointeur moteur `92d8e847`. |
| B6 | « Aucun test n'asserte `Entity.IsVisible` » (`c2-dll.md:141`). | **CORRIGÉ** | Trois tests l'assertent : `ChestAiBlockWorldTests.cs:58`, `ChestAiWorldTests.cs:101`, `WorldProxyEntityManipulationTests.cs:269-273`. |
| B7 | Le chemin `controlFlags == 0x20` n'est atteignable que par un test : `ForcedSequence` n'est posé nulle part en production. | **CONFIRMÉ** | `git grep` ; `DialogueDirector.cs:292` ne pose que `MessageBox` ou `MenuOpen`. |
| B8 | Constantes présentes : `HurtBounceAnimationId = 0x1C`, `SwimHurtAnimationId = 0x3A`. Aucune constante `0x31`. | **CONFIRMÉ** | `PlayerManager.cs:130`, `:145`. |
| B9 | `InputBlockedMask = 0x34`, `GameplayBlockedMask = 0x48` : même masque que le binaire. | **CONFIRMÉ** | `Session/GameState.cs:103`. |
| B10 | Recensement des cases dangereuses : 36 cartes, 6738 / 559 / 25 cases de coût 1 / 2 / 3. | **CONFIRMÉ** | Recomptage indépendant sur les 483 `map_*.json` : `<C>/out_census_hazard.txt`. |
| B11 | 169 fiches de `BALANCE.BIN.json` sur 512 portent une attaque de nibble ≠ 0, dont la fiche 0 (le héros lui-même). | **CONFIRMÉ** | Recomptage. |

## 2. Faits corrigés ou ajoutés

**C1. Le total du recul est −2 168 832 brut, soit −33,09375 px.** Le chiffre de `c2-binary.md` §2.3 (−2 138 112) omet le dernier tick, T+15 (−30 720).
- Je l'ai recalculé sur l'oracle versé `docs/plan-e20-annexe/e20-c2/oracle/out/A1.json`, run 1 (contact sans pad). Le déplacement de position est exactement −2 168 832, de 15 204 352 à 13 035 520. Voir `<C>/out_recoil_a1.txt`.
- Chronologie : `0x31` sur 4 ticks, `0x1C` sur 11 ticks, Idle à T+15 avec la direction retournée de `0x10`. La force de T+15 (−30 720) vient de la décroissance, alors que l'état est déjà Idle.
- Le rapport binaire a donc raison. L'acceptation de c2b doit viser −2 168 832.

**C2. D-H-7 est inexact au niveau du binaire.**
- D-H-7 pose à `0x80` la réponse « type 4 » de la fiche `0x80127008` pour que « la passe de coups ne le touche alors pas, comme la DLL ».
- Or la passe lit l'immunité sur `+0x1C4` du héros (fiche 0, toutes réponses à 0), jamais sur `0x80127008` (A13).
- Le héros est donc toujours touché : `+0x224`, `+0x1D0 = 25` et les étincelles sont écrits.
- Une réponse `0x80` n'évite que la perte de PV. L'entrée donne quand même l'état `0x31`, `+0x1CC = 0x78` et le recul (A6). `c2-oracle.md:80` le dit déjà pour le Charme de Nava.
- Je n'ai pas lu le banc de l'oracle h3. Selon qu'il exécute le vrai `MovePlayer`, le réglage de D-H-7 peut donc produire un héros blessé sans perte de PV, ce que la DLL n'a pas.

**C3. L'ombre suit le clignotement.** C'est la question marquée INCONNU dans `c2-oracle.md:318`.
- La passe `0x80038E84` ne crée ou ne garde l'ombre (`+0x84`) que si statut ∈ {2,3} et `(+0x1CC & 3) ≠ 3`. Sinon elle défait l'ombre (`0x80038EE8-0x80038F18`).
- Pas lu : si l'ombre de la DLL est une composante de l'entité héros ou une entité séparée.

**C4. Les fiches d'attaque du héros.** L'attaque courante est choisie par état : `attacks[état+1]` (`0x80038B74-0x80038BAC`).
- Pour les états `0x31` (index 50) et `0x1C` (index 29), la fiche 0 est à 0 : le héros blessé n'est pas attaquant.
- Les états `0x1A` et `0x1B` portent une attaque de puissance 100.

**C5. Fin de partie à 0 PV.**
- Le bloc de blessure saute directement à la fin du tick, sans tester la mort.
- Au tick suivant, `+0x1CC` non nul fait renvoyer −1 à l'entrée, puis le test `hero.+0x14 == 0` (`0x80031CA8`) envoie le héros vers la mort `0x800327A4`.
- Le plan exclut la mort (D-E20-5, branches non portées). Un héros à 0 PV continuera donc de jouer avec 0 cœur dans la DLL.
- Le commentaire `PlayerManager.cs:64-67` (« no HP system wired… ») est périmé depuis c2a.

**C6. `HeroSheet` peut être null avant le premier `UpdateItemEffectState`** (`Items/EquipmentRecords.cs`).
- Dans ce cas la DLL ne change pas les PV. Le binaire, lui, lit une fiche à zéro et inflige 1 point.
- Cet écart ne se voit que dans un banc de test sans tick préalable.

## 3. Contradictions relevées

1. Recul : `c2-binary.md` §2.3 contre le rapport binaire (voir C1). Le rapport binaire a raison.
2. `IsVisible` assertée par aucun test (`c2-dll.md`) contre trois tests à HEAD (B6).
3. D-H-7, « la passe de coups ne le touche pas », contre le binaire (C2).
4. Le commentaire de site `PlayerManager.cs:1411` annonce des « named stubs » aux sites de blessure. Il n'en existe aucun, et le rapport DLL a raison.
5. Le rapport plans dit que c2b « remesure les arcs d'E14.a ». Je n'ai pas de contradiction à signaler, mais `plan-e14-ia-native.md:947` (Q-E14-b3) reste sans réponse.
6. Désaccord de HEAD entre rapports : `e9e8c58a` cité, `ffc505b1` actuel. Sans incidence sur le code de production.

## 4. Manques et points non vérifiables

**Tests qui contournent la blessure.**
- *Explicites, CONFIRMÉ par `git grep` des mentions c2b ou E20.c2.*
  - `Combat/HitPassTests.cs`. Les tests suivants cesseront de tenir (le héros sera blessé, avec `0x31` à la place de la marche ou du coup) :
    - H3 (lignes 276-291, `samples[17].Hero.AnimTarget == 1`) ;
    - H1both (299-305, coup `0x10`) ;
    - `WhileTheContactLasts_…DamagedCounterStaysAtZero` (320-323) ;
    - `TheReachBehindTheHero_…BecauseTheHeroIsNotHurtYet` (456-459) ;
    - le pin de contact du balayage de portée (449-480).
  - `Hero/HeroRunArenaTests.cs` : G7 comparé « jusqu'au tick avant la blessure », `Assert.Equal(64, last)`.
  - `Hero/HeroSwimTests.cs` : W7 avec point d'accès `HurtAccess` qui pose `0x3A`, `Allowances.Hurt`, `effects` ignoré, `FirstHurtEnd = hurt + 40`.
  - `Hero/HeroRunTests.cs:22` : colonnes d'après le coup, ignorées.
- *Non concernés*, malgré leurs mentions de c2 :
  - `Hero/HeroLifeTests.cs` et `HeroLifeArcTests.cs` (fonctions pures et un tick sans contact) ;
  - `HitPassRulesTests.cs:478` (le `0x31` n'y est qu'une valeur de verrou).
- *Implicites, NON VÉRIFIABLE sans exécution.*
  - Les tests via `HitArena` (24 fichiers). Son constructeur pose le héros, puis laisse passer 6 frames avant `ResetHitState` : un héros posé dans un Beannoïde sera blessé et déplacé pendant ce réglage. Le commentaire du fichier le dit.
  - Sur la carte 14, les Beannoïdes sont aux tuiles (10,52), (12,62), (26,60), (54,56), (44,68), (14,104), (56,92). Le départ du héros est en (8,6), à plus de 46 tuiles : les `ArcRun` de la 14 où le héros reste au départ ne sont pas exposés.
  - Deux `ArcRun` tournent sur des cartes qui ont des cases dangereuses : `g2c1-m8` (carte 444) et `E20e0a4-a4-36` (carte 36). Selon leurs positions, le héros peut y marcher sur une case dangereuse.
- Recommandation : prévoir en T0 un inventaire par exécution, suite complète avec l'entrée active, avant toute réécriture de test.

**Recul sur terre dans la DLL : NON VÉRIFIABLE.**
- La vitesse d'animation est appliquée de façon générique en `0x80036AB8-0x80036B04` (`+0xB8 = +0xF8 << 8`). Le port générique correspondant est dans `Physics/ScriptedMotion.cs`.
- Aucun test ne compare le recul de `0x31` et `0x1C` sur terre à l'oracle. Seule la nage (W7) l'est. Le premier contrôle réel sera A1, et `check.md` prévoit l'arrêt si la force diffère.

**Rythme du HUD : NON VÉRIFIÉ.**
- La routine `0x8004C1F0` fait baisser les cœurs de 1 tous les 4 appels (compteur `+0x1C` de 0 à 3).
- La DLL le cadence à 1 point pour 8 ticks actifs (`HudDirector`, d'après `c2-dll.md`).
- Les deux concordent si la routine est appelée une fois tous les 2 ticks. Je n'ai trouvé aucun `jal` direct vers `0x8004C1F0` et je n'ai pas résolu l'appelant.

**Appelants de `ComputeNewHp` : INFÉRENCE.**
- `0x8003A374` a 38 autres sites d'appel, dans `0x8007D9B0-0x8007ECA8` (natives d'ennemis).
- Je suppose qu'ils passent leur propre entité en `a0`. Je n'ai pas vérifié qu'aucun ne passe le héros.

**Interaction du clignotement avec les écrivains existants de `IsVisible`.**
- La règle du binaire inclut le filtre de statut 2 ou 3. Les écrivains existants (`ChestAi`, `PickupAi`) cachent un objet « Loaded » par `IsVisible = false`.
- Un clignotement écrit en `IsVisible = true` hors phase cachée les ferait réapparaître. Il faut ne restaurer que ce que le clignotement a caché lui-même.
- Le clignotement étant générique, les monstres blessés (écriture de 90) et les objets lâchés (`PickupAi` à 120) clignoteront aussi. Q-E0C-3 a été tranchée « avec c2b ».

## 5. Questions ouvertes

1. Le clignotement est-il générique, comme le binaire, ou limité au héros ? `check.md` §Q3 reste sans réponse écrite dans mes lectures.
2. À 0 PV avant E18, que doit faire le héros : continuer à 0 cœur, ou autre chose ?
3. Faut-il porter le chemin `== 0x20` alors que rien ne le déclenche en production ? C'est un code mort testable, et `check.md` §c le demande.
4. D-H-7 doit-il être réécrit ? Le héros restera blessé même avec une réponse `0x80`, et seuls les PV sont épargnés.

## 6. Chemins (scripts et sorties)

Racine : `<C>/` (`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/c2b-disc/critic/`).

- Outils : `bx.py` (copie), `scan_callers.py`, `scan_any.py`, `scan_load.py`, `scan_store.py`, `scan_addiu.py`, `scan_state.py`, `sites.py`, `jt.py`, `tbl.py`, `anims.py`, `anim2.py`, `census.py`, `recoil_a1.py`.
- Sorties : `out_callers_80031A68.txt`, `out_callers_chain.txt`, `out_1cc_accesses.txt`, `out_1cc_store_sites.txt`, `out_1d0_accesses.txt`, `out_224_accesses.txt`, `out_anim_0x40.txt`, `out_census_hazard.txt`, `out_recoil_a1.txt`.
- Copies de lecture, pour numéros de ligne : `pm_head.cs`, `wp_head.cs`.