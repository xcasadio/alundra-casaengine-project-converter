RAPPORT DU CRITIQUE walk-release-disc (W1, W2, W4 re-dérivés). Dépôt non modifié par moi. Je n'ai lancé ni build ni test. Mes scripts et sorties sont sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/walk-release-disc/critic2/`.
Le `git status` montre `m CasaEngineMonogame` et `m alundra-datas-analyser`. Ces deux lignes ne viennent pas de moi : mes scripts n'écrivent que dans le scratchpad, et la tâche E20.g1b T1 tourne dans le même checkout.

VERDICTS
- W1 (demi-pas = accélération générique de l'Idle) : CONFIRMÉ.
- W2 (la DLL de production ne zéro rien, c'est le montage de test) : CONFIRMÉ par lecture et par modèle, NON ÉTABLI par exécution.
- W4 (O-G3-3 n'a pas la cause du demi-pas) : CONFIRMÉ.
- Mécanisme et esquisse de correctif d'O-G3-3 dans le rapport (§4.3, §4.4, §5) : RÉFUTÉ, voir ci-dessous.
- Estimation « 0 à 2 tests bougent » : NON ÉTABLI.

W1 (rejoué sur ALUN_CD.EXE)
- Ordre des passes de `0x8003B388` relu au désassemblage : événements (MovePlayer), animation `0x80038E18`, physique `0x80038364`. Le set d'Idle est donc déjà actif au tick du lâcher.
- `0x80036614` : le pas ne se recalcule que si la vitesse, la direction ou l'octet `+0xD & 0xF` changent. Il vaut `|cible − force| >> acc`. `0x800367E4` est un incrément borné, que la DLL recopie à l'identique.
- Valeurs rejouées (`critic2/w1.py`, `w1b.py`) :

| Cas | Par tick |
|---|---|
| Marche est | t9 force 2,4375 ; t10 1,2188 avec x 120,7188 → 121,9375 ; t11 0 |
| Ouest | −1,2188 puis 0 |
| Nord / sud | 0,8125 puis 0 |
| Diagonale | 0,8617 / 0,5745 puis 0 |
| Mur | demi-pas refusé, `adj` = 1, x inchangé |
| Porté | marche anim 7 (acc 0) sans rampe ; au lâcher anim 12 (acc 1) : 2,4375 → 1,2188 → 0 |
| Saut | anim 45 : 1,1484 puis 0 |
| Course | glissade `0x2A` de 16 ticks ; Idle à t57 = 0,9844 (64512) puis 0 |

- L'octet d'accélération de l'Idle vaut 1 dans `data-extracted/data/map_alundra.json` et dans `sprite-records.json` (94 sets sur 94 égaux). C'est la donnée du disque, pas un réglage du harnais.
- La règle est générique : « F puis F/2 puis 0 » n'est que le cas Idle (acc 1) depuis une force stable. La règle exacte est le pas figé au tick de changement d'animation.

W2 (DLL)
- Les écritures `ForceX = 0` de la DLL sont uniquement : le reset d'entité (`EntityScriptProxy.cs:1842`), l'opcode `0x10` (`EventProgramRunner.cs:226`) et l'IA ennemie. Aucune au lâcher.
- `ScriptedMotion.PlanForce` et `IncrementForce` égalent le binaire.
- Mon modèle entier indépendant (`critic2/mymodel.py`) donne, avec Idle à 1, x 121,9375 au t10 comme l'oracle. Avec Idle à 0, il donne x 120,7188 et force 0, exactement la sonde `dll-release-probe.txt`.
- Cause : `HeroAttackClock.Table` (`JumpNpcRig.cs`) pose l'Idle à `(0, 0, 0)` et `JumpHeroRig.cs:133` pose `[0] = {Speed 0}`. J'ai comparé les 48 entrées de la table à la fiche réelle (vitesse, accélération, son) : un seul écart, l'Idle. Le correctif est donc une ligne dans `CarryRig.Build`. Rien dans le moteur ni dans les dossiers `Hero/` et `Physics/`, donc aucun manque moteur à consigner.
- Les arcs passent par `ArcRun.cs`, qui appelle `WorldProxy.ApplyHeroSpriteHeader` avec la vraie fiche : ils ont déjà le demi-pas.
- Limite : rien n'est exécuté. Le contrôle décisif reste de rejouer les sondes de g1b avec l'Idle à 1.

W4 : le mécanisme du rapport est faux dans sa forme
- Ce qui tient :
  - Dans `ComputeXYPosition` `0x80037730`, la branche « marche au sol » (`0x80037834-0x80037884`) n'écrit pas `s4` quand un pas est accepté.
  - L'oracle muté (j'ai remplacé le `beqz` de `0x80037840` par `j 0x80037908`) retarde d'un tick le contact au sol : nul à t07, caisse à t08.
  - Donc la branche pèse bel et bien sur le contact.
- Ce qui est faux :
  - Le retour de la fonction est `[sp+0x34]`, c'est-à-dire `s4` sauvé en tête de la DERNIÈRE tentative, donc le résultat de l'avant-dernière.
  - La boucle est une recherche binaire : un pas accepté poursuit avec la moitié du reste.
  - Hors branche sol, une tentative acceptée écrit `s4 = 0`. Le contact au tick raccourci vaut donc le résultat de l'avant-dernière tentative, qui dépend de la phase sous-pixel.
  - Trace en l'air (`w4trace.py air`, t5) : l'avant-dernière tentative échoue, donc contact caisse. Trace sur rec5 (`g3bin/og33_trace.py`, t38) : elle réussit, donc contact nul.
  - Balayage de 32 phases avec la branche sol forcée hors service : contact au tick raccourci 13/32, un tick plus tard 14/32, autre 5/32 (`w4sweep.py`).
- Conséquence : le correctif proposé (« hors branche sol, contact nul si le pas a progressé ») est RÉFUTÉ. Il casserait l'égalité DLL = oracle en l'air (g1b b1 : caisse en t5 et t6, que la DLL égale aujourd'hui).
- Un correctif fidèle doit émuler la queue de la recherche binaire (le mover du moteur fait « avance jusqu'au contact », ADR-0045). Cela relève probablement du moteur, donc d'un manque à consigner, et pas d'une écriture dans `EntityScriptProxy.cs`. Coût NON ÉTABLI.
- Indépendance des causes : le contact au sol arrive au même tick que le raccourcissement dans 18/32 phases et jamais plus tard (0/32 en retard ; les 14/32 « autre » sont des contacts en avance, en partie un artefact de ma détection) ; la force stable ou en rampe ne change rien (`w4_steady.py` du lecteur, que je n'ai pas rejoué). Cause de W1 et cause de W4 : indépendantes, CONFIRMÉ.
- Une affirmation du lecteur n'est pas mesurée : « forces de la DLL égales aux ticks 1 et 2 ». La sonde DLL (`dll-probe.txt` §b3) ne donne que y, contact et `adj`. Les forces en sont déduites.

CE QUI MANQUE AU RAPPORT
1. Les décisions D-E20-69 et D-E20-70 (`docs/plan-e20-heros.md:270-276`, lignes Q-G1B-1 et Q-G1B-2 du tableau des questions) :
   - Elles sont déjà tranchées en A le 2026-10-24 (« toutes les réponses par défaut »).
   - D-E20-69 repose sur une prémisse fausse (« le portage s'arrête net »). Il faut un erratum et informer l'auteur, pas seulement parler de caducité.
   - D-E20-70 dit « avec D-E20-69 si même cause, sinon dans la même tranche ou la suivante » : la seconde branche s'applique.
   - Le rapport dit « Q-G1B-2 reste ouverte » alors qu'elle est tranchée, et il ne cite pas ces décisions.
2. Après la correction de la prémisse, Q-G1B-2 mérite d'être reposée à l'auteur avec le vrai coût : un écart de 1/50 s, dans environ la moitié des phases hors sol, contre l'émulation d'une recherche binaire (probablement côté moteur).
3. Textes à corriger : `docs/plan-e20-heros.md:7287-7292` et `:11097` (Q-G1B-1), et `docs/plan-e20-annexe/e20-g1b/README.md:73-77` et `:104-105`. Ils affirment que la DLL s'arrête net.
4. Aucun test ne pose la fiche réelle sur un lâcher de marche. Un garde-fou serait utile : une base `ArcRun`, Idle = 1 et x = 121,9375. Cela empêche le retour de l'erreur de montage.
5. Des écarts d'`adj` existent entre l'oracle et la DLL (le lecteur les signale en §4.5). Le plan ne compare que le contact, donc ils ne bloquent pas B, D, D2.

W3 : « 0 à 2 tests bougent » reste NON ÉTABLI
- Les traces d'oracle de coups ne contiennent aucun Idle sous force (scan du lecteur, non rejoué).
- Mon sondage des tests Carry et Charge n'a trouvé aucun lâcher sous force, mais je n'ai pas lu les ~35 fichiers du lecteur.

Fichiers clés
- `Alundra/Physics/ScriptedMotion.cs` (lignes 635-775 et 1010)
- `Alundra.Tests/Support/JumpNpcRig.cs`, `JumpHeroRig.cs`, `RunMontage.cs`
- `Alundra.Tests/Hero/CarryRig.cs`