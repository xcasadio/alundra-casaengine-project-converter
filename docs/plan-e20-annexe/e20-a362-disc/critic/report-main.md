# Critique frais de la découverte `a362-h3a` (lecture seule, 2026-10-25)

## 0. Verdict global

L'essentiel du rapport tient :
- L'arc A362 n'exige aucun code de production.
- D-H-7 est faux.
- Les cinq chiffres de (w1) ne bougent pas.
- Le registre a quatre épingles.

Deux corrections touchent directement ce que l'arc devra asserter :
1. **La bombe d'Olen ne s'arrête pas à son atterrissage.** Elle glisse jusqu'au mur nord, au repos vers y ≈ 103. Elle touche le couvercle dès le premier tick de boîte, soit A+16. La prédiction du rapport (centre vers y 113, contact entre A+19 et A+25) est fausse.
2. **La chronologie de B[4] est décalée d'un tick, et un pc est faux.**
   - `0x8E @172` s'exécute à H+3, pas à H+2.
   - Le programme F d'Olen est `F[3] @748`, pas 740.

Cadre :
- Le HEAD a avancé de trois commits de tests (b2), de `8174c0cb` à `5e67722d`. Le code de production lu est identique.
- L'arbre contient les modifications d'un executor, que je n'ai pas touchées.
- Ni build, ni test dotnet. Le code DLL a été lu par `git show HEAD:`.
- Le binaire a été désassemblé par moi (`bx.py`). L'oracle a été rejoué depuis ma propre copie.

## 1. Verdicts par fait porteur

Étiquettes : [FAIT] lu, [MESURÉ] sorti de l'oracle, [INFÉRENCE] déduit.

| Fait du rapport | Verdict | Preuve |
|---|---|---|
| A8 : les 48 opcodes de la 362 ont un `case` | **CONFIRMÉ** | Mon scan indépendant (`src/c_ops.py`) trouve 48 opcodes, Break et End compris, aucun manquant. Nuance : neuf cases ont un repli dégradé qui dépend d'un service du monde. Ce sont 0x27, 0x39, 0x3B, 0x42, 0x53, 0x55, 0x8E, 0x8F, 0xBD (`src/c_ops2.py`). L'effet dépend du câblage de l'ArcRun : NON VÉRIFIABLE sans lancer le banc. |
| A1, A2 : positions, `SpriteDirection&0x80` = carte, `&0x40` = présent au chargement | **CONFIRMÉ** | Règle de `EntityRecordMapper.ResolveSpriteType`. Positions recalculées : Olen (204,120), couvercle (216,104) z 32. |
| A3 : en-tête du sprite 243 | **CONFIRMÉ** | `map_alundra.json` : MoreFlags 0xA1, offset −23, taille 48×8×44, `BreakEffect` 11, E = 19. Fiche d'équilibre 243 : PV 1, réponses `[128,128,128,16,128×7]`, indexées par `nibble−1`. |
| A4 : bombe, sprite 2 | **CONFIRMÉ** | Flags 78/35/19 = `0x13234E` (ClassB, HitsClassA, HitsClassB, DeactivateOnAnimationEnd, Gravity, NoObstacleSlide, **pas** Collidable). C62, D30, E12. Fiche 2 : une attaque, attribut 4, puissance 5. Anim 1 : 17 images, son 46, boîtes aux images 4 à 10 (16×8×11 jusqu'à 48×32×31). Anim 2 : vitesse 128, impulsion 1024. |
| A4 : « AnimFlags = 0x40 » | **CONFIRMÉ, mot trompeur** | C'est l'octet `Acceleration` du jeu d'animations (64 sur les anims 1 et 2), lu par le DLL comme `HitAnimByte` (`EntityScriptProxy.HitPass.cs:108`). Le couvercle a 0 : cible valide. |
| A5 : script d'Olen, `Wait n` = n+1 ticks | **CONFIRMÉ** | `EventProgramRunner.Wait` : le tick d'armement, puis n ticks, soit n+1. T4 à pc 582 à S+31 : confirmé. |
| A5 : « F[3] pc 740 » (tableau du §1.4) | **CORRIGÉ** | F[3] est à **748** (tables F `[0,740,744,748,0]` : 740 est F[1], 744 est F[2]). |
| A6 : B[2], B[3], B[4], B[6], B[7] | **CONFIRMÉ** | Programmes décodés relus. |
| A7 : héros loin et sans contrôle | **CONFIRMÉ** | `0x10 @514`, B[3] promène le héros de 48 px vers le bas. D-C2B-13 (coup perdu en scène) est bien au plan. |
| B1 : `0x8B` | **CONFIRMÉ** | DLL (`EventProgramRunner.cs` `SpawnEntityNextToEntity`) et **binaire** `0x8004033C` : spawn de v2, recherche v1, X, Y, Z = position + `(v<<16)`. Aucune direction forcée. |
| B1 : direction du spawn | **CONFIRMÉ** | `0x8003A1B8` fait `SpriteDirection & 3` puis la table `0x80023644`, qui vaut `{0, 0x10, 8, 0x18}`. Record 5 : direction 0x10 = nord. |
| B2 : `0x87 [128,3]` = touché par l'attribut 4 | **CONFIRMÉ** | Binaire `0x80040048` (`src/../out/dis_87.txt`) : `+0x224`, `+0x1D4` (boîte), `+0x1C8` (attaque), nibble contre l'octet `0x80098FA4+v2`. Table lue : `[1,2,3,4,5,5,6,7,…]`. L'index 3 donne 4. Recherche 128 = entité elle-même : doc de `EntitySearchService`. |
| B3 : le couvercle est une cible valide | **CONFIRMÉ** | Masque de la bombe = 9 (`HitPass.ClassMaskOf`). Le couvercle porte le bit 0. Mesuré à l'oracle : `+0x224` posé sur le couvercle (§2). |
| B4 : D[1] s'exécute en entier | **CONFIRMÉ** (lecture) | Slot D à état neuf (`_slotAScratchState`), sans Break entre 728 et 737. Le DLL n'a pas été lancé. |
| B5, B6 : `(E,19)`, `(D,30)`, `(E,12)` absents, no-op | **CONFIRMÉ** | `CreateForProduction` n'a que 11 appels. `IsInlineDeactivateCode` vaut `<=1` ou 10. |
| B8 : aucun test ne charge la 362 | **CONFIRMÉ** | Seuls : l'arrêt de TA19, la garde des coffres, `ExemptCombatMaps`. Deux tests de corpus parcourent les programmes de toutes les cartes sans charger de monde (`MissingAnimationWaitCorpusTests`, `DialogueSpeakerCorpusTests`). Ils ne consultent pas le registre. |
| **B9 : atterrissage à ≈ 23,5 px, centre ≈ y 113, première boîte à 1 px du couvercle** | **CORRIGÉ** | Voir §2. |
| **B10 : H entre A+19 et A+25 ; `0x8E` à H+2** | **CORRIGÉ** | Voir §2. Les ticks A+51, A+52, A+53 sont confirmés. |
| C1 : D-H-7 faux | **CONFIRMÉ** | Rejoué : avec l'octet `0x8012700E` à 0x80, la PV de l'entité (`+0x14`) reste à 10 mais l'état passe à `0x31` et le recul est de 33 px, identique à la version sans réglage. `STATS+4` est la magie (5 dès le départ), pas la PV. |
| C2 : l'immunité vit sur `+0x1C4` | **CONFIRMÉ**, et complété | Je pose `rec+6 = 0x80` : `+0x224` n'est **jamais** écrit, PV 10, position inchangée (`src/c_prime_touch.py`). Le script du rapport n'imprimait pas le contact. |
| C3 : rejeux identiques | **CONFIRMÉ** | Par moi : `bomb_timeline`, `hurt_circle`, `bomb_limit3`, `throw_dirs`, `hero_hit`, tous identiques à l'annexe. |
| C4 : emplois de D-H-7 | **CONFIRMÉ** | `:10474`, `:10603`, `:10671`, `:10672`. |
| C5, C6, C7, C8, C10 | **CONFIRMÉ** | Les 5 sorties sont identiques octet pour octet. C5 : K+1 anim et son 46, boîtes de K+17 à K+31, fin K+52, statut 4 à K+53, libre à K+54. Aucun chiffre de (w1) ne change. |
| C9 : le héros est blessé par son lancer vers le bas | **CONFIRMÉ**, marges **CORRIGÉES** | Voir §3.7. |
| D1, D2 | **CONFIRMÉ** | Aucun test ne code D-H-7. |
| D4 : quatre épingles du registre | **CONFIRMÉ** | `NativeSpriteRegistryTests:58`, `LiftableAiRegistrationTests:76-77`, `ChargedArrowAiTests:238-239`, `WaveSwordLifeTests:185`. Aucune autre épingle de compte. Aucun test ne réserve `(D,30)`, `(E,12)` ou `(E,19)` comme clé libre : ils emploient `(D/C,25)`. |
| D5 : Z4 et Z7 | **CONFIRMÉ** | `Entities/LogicalZWorldTests.cs`. |
| D6 : 23 en-têtes à E = 19 | **CONFIRMÉ** | Mon scan indépendant : 23, dont les 3 couvercles globaux. |
| « G248 n'est posé que par la 363 » | **CONFIRMÉ, portée limitée** | Le scan cherche les opérandes `(248,0)` aux positions 1-2 de tout opcode non lecteur : 143, 258, 298, 363, 390. Seule la 363 est un `Flag on`. Un autre encodage d'écriture n'est pas exclu. |
| D'autres faits du §1.3 | **CONFIRMÉ** | `ArcSpec` (le héros est posé par tuile, d'où l'`Arrival` pour un pixel), `ActiveCollisionEntity`, les aides d'`ArcChecks`, les prefabs, le dossier de la 362 et ses 8 nœuds yarn. |

## 2. Correction majeure : où explose la bombe d'Olen

**[FAIT] Le script ne met jamais fin à l'animation 2.**
- C[5] pose `Set anim 2` à T4 et ne la remplace qu'à T5, environ 109 ticks plus tard.
- Aucun C62 ne la remet à 0 à l'atterrissage : l'enregistrement a C = 133, un programme de carte.
- L'animation 2 a la vitesse 128. `ScriptedMotion.PlanForce` applique la vitesse de l'animation à tout mobile.
- L'ancien rapport copiait la portée du lancer du héros, 23,5 px. Or c'est C62 qui la borne, en remettant l'animation à 0 au sol.

**[FAIT, binaire] La bombe n'est pas bloquée par le couvercle.**
- Règle documentée (`docs/plan-e19-opcodes.md` §d2b) : un mobile doit lui-même être Collidable pour être arrêté par une entité.
- La bombe n'a pas le bit 0x80.
- L'hypothèse du rapport, « le corps du couvercle peut arrêter le vol », est donc fausse.

**[MESURÉ] Dans l'oracle, avec les vraies cases de la 362.**
- Montage : le champ de l'oracle reçoit les 52×60 cases de `map_362.json` (mot `Flags` de chaque case). C62 est neutralisé (`+0x78 = 0`). Le couvercle est en (212,100) z 32. La bombe naît en (204, 136 ou 138) z 64, direction 0x10, avec ou sans lévitation jusqu'à T4. Elle reçoit anim 2 à T4 et anim 1 à T5.
- Résultat :
  - La bombe vole environ 23 ticks, atterrit à z = 32, puis **continue à 1 px par tick**.
  - Elle s'arrête à **y = 103,00, x = 204, z = 32**, contre la rangée 5 (le mur nord, y < 96). Cela vaut pour toutes les variantes essayées.
  - Elle est alors **dans l'empreinte du couvercle** (y de 100 à 108).
  - La boîte de 16×8 couvre y de 99 à 107 : elle recouvre le couvercle.
  - **Au premier tick de boîte, le couvercle est touché** (`+0x224`, trois étincelles). C'est T5+17, soit **A+16** avec A = T5+1.
- Contrôle : sur un champ plat sans mur, avec un couvercle sur le trajet, la bombe traverse le couvercle et continue sans fin.
- Sorties :
  - `out/crit_olen_real_flat_T5-120.txt`
  - `out/crit_olen_real_hover_T4-32_T5-140.txt`
  - `out/crit_olen_lid_flatfield_passthrough.txt`
  - `out/crit_olen_flight_slide_no_C62.txt`
  - `out/crit_map362_cells.txt`

**[INFÉRENCE]** Le DLL fait de même, puisque `PlanForce` et le test de mur sont portés et que Z4 pose déjà une bombe sur l'animation 2. **NON VÉRIFIABLE** sans lancer le banc. Si le DLL s'arrête ailleurs, c'est le « manque autre » et l'arrêt de h3a déjà prévu.

**Conséquences pour l'arc**
- Remplacer l'assertion « vol : atterrissage mesuré » par « glissade : y décroît de 1 px par tick après T4, repos vers (204, ≈103) à ±1, stable jusqu'à T5 ».
- H est le premier tick de boîte. Avec A = tick de `Set anim 1` (C pc 641), **H = A+16**.
- La marge est large : toute position de repos entre y 97 et 111 donne le contact au premier tick. Un contact plus tardif signale un écart du DLL.
- Le montage de l'oracle donne aussi la chronologie du retrait de la bombe, avec le natif E12 : statut 4 à A+52, case libre à A+53. Le natif y reçoit la bombe en statut 2, pas seulement en statut 3. Cela confirme les lignes « après h3a » du tableau.

### Chronologie rectifiée (A = tick de `Set anim 1` @641)

Formules déduites de `RunOneScriptCall`, à confirmer en T0 [INFÉRENCE] :

| tick | événement |
|---|---|
| S | `0x8B @569` |
| S+31 | T4 @582 ; la bombe passe en anim 2 au même tick ou au suivant |
| T4 + ≈24 | atterrissage, puis glissade jusqu'au mur |
| A | anim 1 et son 46 |
| A+16 = H | première boîte, contact sur le couvercle |
| H+1 | D[1] aux pc 728, 731, 734, 737 ; T6 ; couvercle en statut 3 |
| H+2 | B[4] voit T6 mais s'arrête au **Break @171** ; après h3a, E19 : son 0xE6, effet 11, statut 4 |
| H+3 | `0x8E @172` ; après h3a, case libre |
| H+64 | `0x8F @179` |
| H+125 | naissance @182 |
| H+156, H+187, H+218 | naissances @192, @202, @212 |
| A+51 / A+52 / A+53 | drapeau de fin / E12 détruit, statut 4 / case libre |

## 3. Manques et ajouts

1. **`0x8E` à H+3, pas H+2.** Le Break @171 termine l'appel (`EventProgramRunner.RunOneScriptCall`, cas `0x00`). Toute la suite de B[4] est décalée d'un tick par rapport au rapport.
2. **La bombe d'Olen ne coupe jamais l'herbe.**
   - E12 natif (`0x8007F27C`, relu) détruit la bombe dès son premier appel si `+0x88 == 1` et `+0xAC == 1`, sans appeler `CheckAndTriggerTileEffect`.
   - Olen la met en anim 1 lui-même, donc le premier appel de E12 arrive en fin d'animation.
   - L'en-tête de h3a (« la bombe d'Olen disparaît et coupe l'herbe », §Partage des phases, Phase B) est donc inexact.
   - Il n'y a pas d'herbe dans la scène, mais l'énoncé est faux.
3. **La recette de h3a « carte 362 par Change map » ne montre rien sans G248.** Sans lui, Olen reboucle sur le dialogue 131 (A5 et B12). Il faut le préréglage de Q-A362-2 ou le drapeau. Ce point manque à la liste des textes à corriger du §2.4.
4. **Les Murutas naissent dans le couvercle au HEAD.** Elles naissent en (204,104) et (216,104), dans le corps du couvercle encore présent. La règle d2b dit qu'un mobile qui chevauche un obstacle reste bloqué. Toute assertion sur leur marche après la naissance serait propre au HEAD et changerait après h3a. Le rapport affirmait seulement « la scène continue » ; l'arc doit s'arrêter aux naissances, comme prévu.
5. **Lids sur d'autres cartes.** Les sprites 241 à 243 ont des enregistrements sur 14 cartes : 6, 14, 61, 63, 64, 67, 80, 85, 90, 104, 151, 362, 382, 429 (`src/c_scan.py`). Aucune n'est dans la chaîne des 30 cartes, donc l'arrêt de h3a ne se déclenche pas. Mais la 14, carte des Beannoïdes de b2 et c2b, porte un couvercle 243. Au HEAD, la bombe n'est référencée que dans la 362.
6. **Textes du plan à relire en plus de ceux du §2.4.**
   - `docs/plan-e20-heros.md:10432` (« le héros du kit perd 5 PV sur 10 (sa réaction est E20.c2b) »).
   - `:10573` (Hors périmètre de h3a).
   - `:10733` (h3b).
   - Les questions Q-H3-1 et Q-H3-2.
7. **Marges de C9 corrigées.**
   - Seuils mesurés pour un héros immobile (`out/crit_hero_thresholds.txt`) :
     - est : touché à 34,75 ou moins, indemne à 35,0 ;
     - sud : touché à 23,5 ou moins, indemne à 24,0 ;
     - nord : touché à 22,0, indemne à 23,0 ;
     - ouest : indemne dès 34,0.
   - Marges du lancer : est environ 0,25 px, nord au moins 0,5 px, **ouest au moins 1,25 px** (le rapport disait ±0,25 pour la gauche et la droite), sud touché de 0 à 0,5 px.
8. **Bombe tenue et blessure.**
   - La clé `bomb_hurt_while_carrying_bomb` montre une bombe lâchée qui part sur 35 px, sans se briser.
   - D-C2B-13 dit « l'objet porté tombe et se brise ». Cela décrit les caisses ; pour la bombe, c'est C62 qui règle le lâcher.
   - À rapprocher en T0 de h3b et de la tranche des objets portés.
9. **Libellé.** Au §2.2, la ligne (w8) renvoie à « C7 » ; c'est C10.

## 4. Questions à l'auteur

Pas de nouvelle question. Deux ajustements :
- **Q-H3B-1** : rien n'est à reprendre pour le sud (touché de 0 à 0,5 px) ; le défaut « comme l'original » reste le seul cohérent avec D-C2B-13. Le coût de l'option B se mesure en écart au binaire.
- **Q-A362-2** : le préréglage `olen-m362` doit poser G248. Pour voir l'explosion en jeu, c'est indispensable, même pour la recette de h3a.

## 5. Fichiers

Base : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/a362-h3a-critic/` (notée `BASE`).

- **Lanceur et aide** : `BASE/run/runpy.py`, `BASE/run/bx.py`.
- **Oracle copié** : `BASE/src/` (copie des 52 fichiers de `docs/plan-e20-annexe/e20-h3/critic/run/`, plus les 9 `k_*.py` du rapport copiés).
- **Scripts nouveaux** (dans `BASE/src/`) :
  - `c_ops.py`, `c_ops2.py`, `c_ops3.py` (cases et replis dégradés) ;
  - `c_scan.py` (E19, couvercles, bombe) ;
  - `c_sprites.py`, `c_sprites2.py` (données des sprites 2 et 243) ;
  - `c_sheet.py`, `c_resp0.py`, `c_prime_touch.py` (feuille et immunité du héros) ;
  - `c_zone_hero.py` (seuils de blessure) ;
  - `c_cells.py` (cases de la 362) ;
  - `c_findprog.py`, `c_olen_flight.py`, `c_olen_flight2.py`, `c_olen_lid.py`, `c_olen_real.py`, `c_olen_real2.py` (vol de la bombe d'Olen).
- **Sorties** (dans `BASE/out/`) :
  - `crit_dh7_*.txt`, `crit_key_*.txt`, `crit_w*.txt`, `crit_throw_explode.txt`, `crit_limit3_hero.txt` (rejeux) ;
  - `crit_olen_real_flat_T5-120.txt`, `crit_olen_real_hover_T4-32_T5-140.txt`, `crit_olen_lid_flatfield_passthrough.txt`, `crit_olen_flight_slide_no_C62.txt`, `crit_hero_thresholds.txt`, `crit_map362_cells.txt` (nouvelles mesures) ;
  - `dis_8B.txt`, `dis_87.txt`, `dis_spawn.txt` (désassemblages).
- **Sources du DLL lues à HEAD** : `BASE/head/`.