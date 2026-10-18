# Contre-vérification du relevé E20.f4 (§1.20), HEAD `3c923cfa`, branche `chantier/restructuration-dll-r3` (2026-10-18, lecture seule)

Le relevé dit travailler au HEAD `e1a68676`. Le HEAD actuel est `3c923cfa`, trois commits de docs plus loin. `git diff e1a68676 HEAD --stat` ne touche que `docs/plan-e20-heros.md`, et seulement à partir de la ligne 7318 : toutes les citations du plan sous 7318 restent valides. Je n'ai rien construit ni lancé côté .NET. Incident : une lecture de `tools/dll-structure/scan_types.py` par Python a créé `tools/dll-structure/__pycache__/` dans le dépôt (non suivi, horodaté 02:57:24 dans cette session). Je l'ai supprimé aussitôt, et `git status` est revenu à l'état de départ (seuls les deux sous-modules ` m`, déjà là avant).

## Erreurs

1. **§2 n°8, « trois clauses basculent ».** Il y en a quatre au HEAD, et le relevé le constate lui-même : `:257` (f4, phase A), `:243` (phase B de f4 puis de f5), `:247` et `:251` (f5), dans `Alundra.Tests/AlundraWeaponSpawnSeamTests.cs:238-259`. Le « quatre » du plan est donc juste ; à la base `9ecc7d5` la quatrième clause était `Assert.Empty(ProductionWiredWeaponTypes)` en `:238`. Ce qui est faux dans le plan : la liste nommée (trois clauses) et D-F4-8, qui ne couvre pas `:243`.
2. **§2 n°10, `CreateBreakEffect` présenté comme une exception au « Z du binaire ».** Ce n'en est pas une. Pour une entité, `IEffectAnchor.PosZ` vaut `PosZ + 1` (`Alundra/Entities/EntityScriptProxy.cs:461` ; `Alundra/Rendering/Effects/FrozenEffectAnchor.cs:30`, « The binary's Z of the entity (PosZ + 1) »). L'affirmation du plan est vraie pour tous les créateurs. Il existe aussi déjà un précédent de Z du binaire pour une entité qui n'est pas le héros (l'ancre de `CreateBreakEffect` et de `CreateHitAttachedEffect`, `NativeEffects.cs:175`, `:182`).
3. **§4.1, citation `:6392-6395` pour « f4, f5 et f6a prérequis fermes de g ».** Ces lignes sont le point 4 du bloc, qui parle du registre. Le bloc « Révision au HEAD » ne fait pas d'f4 un prérequis : au contraire, il joue (b3) **sans** D-F4-7 (`:6444-6446`). Les bonnes citations sont :
   - f5 pour g2a : `:6432-6433` et `:6560` ;
   - f4 et f6a pour g2b : `:6618` et `:6654` ;
   - g1b : `:6673`.
4. **§4.6, « g4a cite `:74` et `:235` » d'`AlundraHitArena.cs`.** C'est g4b (§1.29.2, `docs/plan-e20-heros.md:7469`). Cette citation n'existait pas à la base annoncée `e1a68676` (`git show e1a68676:docs/plan-e20-heros.md | grep -c 'AlundraHitArena.cs:74'` donne 0). Elle apparaît avec `5db6eae0`.
5. **§6 manque 4, « La somme exige de modifier trois fichiers hors périmètre ».** Ce n'est pas établi. Un test peut dériver chaque compte en inscrivant chaque classe dans un registre neuf (`new NativeSpriteRegistry()`, puis `BeannoideAi.Register(r)` etc., puis `r.Count`), sans toucher aux fichiers d'IA. Appuis : `Alundra/Ai/NativeSpriteRegistry.cs:31-46` (classe interne, constructeur implicite, `Register`, `Count`) et `CreateForProduction` `:36-43`, qui fait déjà ainsi. INFÉRENCE. Le vrai défaut est la coexistence de deux règles de dérivation (D-F4-5 contre `:6394-6395`) : je la classe C, pas B.
6. **§3.C, `AlundraWaveSwordSupport.cs` (`GrassCallTicks`) comptée parmi ce que touche le déplacement du bouchon.** `GrassCallTicks` lit le fichier d'oracle `wave_grass2.txt` (`Alundra.Tests/AlundraWaveSwordSupport.cs:102-107`), pas le bouchon : le déplacement ne le change pas. Les quatre références au journal (`AlundraWaveSwordWiringTests.cs:51`, `AlundraWaveSwordLifeTests.cs:53`, `:130`, `:149`) restent exactes.
7. **§1, ligne `ResolveSpriteType`.** La plage `:227-237` oublie le `return` avec `| 0x100`, qui est en `:238` (la méthode va de `:227` à `:239`). Surtout, c'est le chemin des enregistrements de carte. Pour la flèche, née par l'interface, le type vient de :
   - `Alundra/Entities/Spawning/EntitySpawnFactory.cs:541` (`isMapPage ? index | 0x100 : index`) ;
   - puis `:772` (`proxy.SpriteType = spriteType`) ;
   - doc en `:498-499`.

   Le verdict VRAI (§2 n°2) tient littéralement, mais ces citations ne prouvent pas le cas de la flèche.
8. **§6 manque 13, « le tir en l'air de la flèche chargée est sans objet ».** Ce n'est pas établi. Le Z de naissance est le Z du héros plus l'octet z de la fiche (`Alundra/Hero/PlayerManager.cs:1203`). Un relâchement en l'air, s'il existe, change donc la hauteur de naissance, pas la fiche. Le hors-périmètre peut avoir un objet : INCONNU (voir Inconnus).
9. **Citations imprécises, verdicts justes :**
   - doc du registre dans `EventProgramRunner.cs` : `:48-50`, pas `:47-50` (`:47` est vide) ;
   - anciennes lignes du bouchon de la vague données dans un autre ordre que celles du HEAD : à `5434cf6`, journal `:24`, `Tick` `:31`, bouchon `:37` ;
   - §2 n°22 : `WeaponTables.cs:53` est le commentaire. La correspondance est en `:84` (`table[6] = 9`) et la fiche 9 en `:66` ;
   - §2 n°18 : le test est en `AlundraMapEdgeWorldTests.cs:120`, `:116` est un commentaire ;
   - §2 n°13 : seul le chemin autonome est cité (`EntityScriptProxy.cs:1521-1524`). Dans un monde piloté, le marquage de rang 1 est en `Alundra/Maps/WorldProxy.cs:2920`.

## Omissions

1. **Le terrain de l'oracle est synthétique, pas la vraie carte 14.**
   - Source : `docs/plan-e20-annexe/e20-f3/oracle/tools/world.py:105-122` (« flat synthetic field », constantes d'en-tête de la carte 10).
   - Scénarios : `disc-e20f4f6/bin/run_charged.py` utilise `cells=fw.wall_col(15)`, une colonne 8 de hauteur 1 ou 2, une bande d'herbe en colonnes 5 à 12, rangées 5 et 6, et le héros en (100, 100) ou en (1150, 100). Le relevé ne le dit pas (voir manque B-a).
2. **Prérequis de programme absent de §1.20 : la fin du chantier de restructuration.** `docs/plan-restructuration-dll.md:160` dit : « Aucune autre tranche (E20, E14, E19) pendant le chantier ». R5 est ⚠️ (Q-R-3, `:861`). Les autres tranches le nomment : e0c `:6968`, E20.e `:7123-7124`, g `:6386`, g3 `:7264-7266`. Le relevé ne cite que la garde Q-R-7.
3. **(p3) contient une partie verte par construction.** `Damage.ResolveBalanceTarget` est une fonction pure. f3c l'avait marqué ainsi (« (p7a) [V] green by construction », `Alundra.Tests/AlundraWaveSwordDamageTests.cs:9`). « (p1) à (p3) rouges avant T3 » (`:5877`) et « retirer la ligne 7 rend rouges exactement (p1) à (p3) » (`:5880`) ne tiennent que si (p3) fait une seule assertion avec la partie `TouchingEntity`.
4. **Les natifs sont éteints dans `HitArena`** (`Alundra.Tests/AlundraHitArena.cs:70`) : sans l'interrupteur, C94 ne tourne jamais en (c4)-(c8) ni en (p1). Rallumer tous les natifs réveille les Beannoïdes de la carte, et ils restent présents même sans cible :
   - `HitArena.cs:77-84` ne collecte que les cibles demandées ;
   - `WaveRun` passe une liste vide (`AlundraWaveSwordSupport.cs:153`) ;
   - preuve que le réveil a un effet : la seconde relecture d'E20.e, `docs/plan-e20-heros.md:7047-7049`.

   Le plan ne lie pas ces tests à D-F4-7. Cela complète le manque 7 du relevé.
5. **Le T0 ne peut pas être une copie.**
   - La capture des effets (un enregistrement distinct par appel, vitesses `+0x54..0x5C`, comptage des tirages) vient du harnais de découverte `disc-e20f4f6/bin/fw.py:1-8`. Son en-tête dit que la source n'avait « one shared dummy slot ». `pworld.py:8` dit « effects ... stay stubs ».
   - Les 40 fichiers de `bin/src/` sont identiques octet pour octet à `e20-f3/oracle/tools/` (`cmp`). Les deux absents (`dis_update_animation.txt`, `exe_fp.py`) ne sont cités qu'en commentaire.
   - Format de sortie actuel : `.effects` est une liste de chaînes (`"fx(id=25,dir=3,x=124.000,…,v=(-3.0000,0.0000,0.0000))"`), les sons sont dans `journal` (`"snd(33)"`) et il n'y a pas de `.snd[]`. Il faut donc réécrire au format d'objets que demande `:5841-5842`.
   - La clé des 12 départs (D-F4-9, (c7)) est un texte, `bin/out/sweep_wall_charged.txt`, sans JSON.

   Ce point lève en partie l'INCONNU du manque 9 du relevé.
6. **O-E0A5-2, la cause côté DLL.** La passe des compteurs de la DLL ne parcourt que `_spawnedEntities` (`WorldProxy.cs:3000-3009`) : une case réutilisée repart de `FrameCounter` 0. Dans l'oracle `charged7_east_wall`, la case 1 est vide aux ticks 103-104 et `cnt1c` vaut 1 à S = 105 : la phase y est fraîche. En revanche (p1) naît « à la plus basse case libre » d'une vraie carte occupée, donc la phase de la traînée peut y différer. INFÉRENCE.
7. **§1.20 n'a pas l'e0a5 dans ses prérequis** (`:5794`), alors que la ligne 6 de l'enveloppe l'a (`docs/plan-e20-heros.md:714`, révision 10 `:655-660`). e0a5 est ✅ : la mention est périmée mais ne bloque rien.
8. **Commentaires de la DLL que la phase B rend faux, non listés :** `PlayerManager.cs:1198` (« empty until E20.f3c »), `:1208` (« The set is empty in this slice »), `:1224` (« E20.f4 to E20.f6 wire the others »), `:1228` (« the production set stays empty »).
9. **Noms des drapeaux du plan, non traduits :** `DeactivateOnAnimEnd` s'appelle `EntityFlags.DeactivateOnAnimationEnd` (`Alundra/Entities/EntityFlags.cs:83`). Les autres sont `HitsClassA` `:53`, `DeactivateOnImpact` `:69`, `DeactivateOnHit` `:75`, `NoObstacleSlide` `:130`. La règle de choix de E (`EntityScriptProxy.cs:1718-1720`) inclut aussi `DeactivateOnHit` et `DeactivateOnAnimationEnd`.
10. **D-F4-8 dit encore « par la première des tranches »** (`:5786`), formule d'avant l'ordre ferme (le diff `f8f9f09e`..HEAD de §1.20 ne change que le découpage, `:5729-5731`, les dépendants et le T0).

## Manques bloquants confirmés ou ajoutés

Gravité : B bloque l'exécution telle qu'écrite ; C est à corriger dans le plan avant.

**Confirmés du relevé :**
- n°1 (B) : chemins, noms et garde Q-R-7, absence de `check_names.py`. `check_names.py:96-98` émet bien `[hors table]` pour tout fichier hors table ; il n'existe ni mode `--rules` ni R7.
- n°2 (B) : l'ordre f4 / E20.e est contradictoire :
  - §1.25 `:6837-6838` place E20.e avant f4 ;
  - §1.27 `:7059` et `:7136` ;
  - O-F4-1 `:6249` ;
  - l'enveloppe, ligne 6 (`:714`), ne lie pas f4 à E20.e.
- n°3 (B) : le bouchon est dans le fichier de la vague (`WaveSwordAi.cs:25-40`), et le périmètre et les faits d'E20.e (READY, `:7088`, `:7133`) supposent qu'il y reste.
- n°5 (B) : `AlundraWeaponSpawnSeamTests.cs:243` bouge à la phase B.
- n°6, 7, 8, 9, 10, 11, 12 (C).
- n°14, 15, 16 (m).

**Requalifiés :**
- n°4 : B devient C (erreur 5).
- n°13 : non établi (erreur 8).

**Ajoutés :**
- **B-a.** Le banc des tests de monde n'est pas nommé. Les valeurs attendues (mur colonne 15, x = 349, fenêtre des 12 départs x0 = 96 à 107, marches de la colonne 8, bord à partir de x = 1150) viennent d'un terrain synthétique (omission 1). « L'arène de la charge » est la vraie carte 14 (héros en (212, 96), R = 91). Il faut nommer la façon de poser le terrain : le précédent est `AlundraMapEdgeWorldTests.cs:43-50` (`FlatCells.Create` posé par réflexion). E20.e a reçu un REVISE pour le même écart (`:7044-7047`).
- **B-b.** Prérequis « fin du chantier de restructuration » absent (omission 2).
- **C-c.** Les tests de vie et (p1) dépendent de l'interrupteur, ou à défaut d'un banc vidé, et le plan ne le dit pas (omission 4). Cela s'ajoute à la forme non fixée de D-F4-7 que g2b consomme (`:6618`, (b3) en `:6645` : « caisses vivantes, Beannoïde inerte, D-F4-7 »), alors que le bloc `:6444-6446` joue (b3) sans lui.
- **C-d.** (p3) est vert par construction pour sa partie pure (omission 3) : l'acceptation et la preuve de retrait sont à réécrire.
- **C-e.** Le T0 est un portage du harnais `fw.py` (avec `run_arrows.py`, que `run_charged.py:8` importe) et un changement de format, pas une copie d'outils (omission 5) ; la clé des 12 départs n'existe qu'en texte.

## Confirmé (comptes)

- **§1, citations : 32 lignes, 32 retrouvées au même code.** 29 exactes ; 3 imprécises sur un numéro de ligne (erreurs 7 et 9).
  - Ancien et HEAD relus :
    - `EventProgramRunner.cs:286-292` ;
    - `IsInlineDeactivateCode` `:328` (ancien `:378`, aux bases `be9a067` et `9ecc7d5`) ;
    - registre `:36-43`, `:54-67`, `:60-63`, `:70`, `:84-99` ;
    - `ProductionWiredWeaponTypes` `:1226` (ancien `:1213`, vide) ;
    - `UnhandledWeaponSpriteCodes` `:1240-1266` ;
    - le crochet `:1271` (ancien `:1258`) ;
    - S5 `:238`, `:243`, `:257` (ancien `:233`, `:252`) ; S1 `:83`, `:85-95` ; (p5) `AlundraWaveSwordWiringTests.cs:244`, `:251` ;
    - `EffectStage.cs:178`, `:192` (ancien `:190`) ; `NativeEffects.cs:96`, `:161`, `:175`, `:182`, `:200` (ancien `:190`) ; `Damage.cs:24` (ancien `:21`) ;
    - `HitArena.cs:27`, `:70` (ancien `:68`) ; `ArcRun` `:94` et `:127` (ancien `:84`) ;
    - `WorldProxy.cs` : `:3009` (ancien `:2944`), `:3301` (ancien `:3226`), `:3488` (ancien `:3376`), `:3566`, `:3573` ;
    - `EntityScriptProxy` `:98`, `:154`, `:159`, `:162`, `:499`, `:1383` ; `HitPass.cs:39` ; `EntitySpawnFactory.cs:736`, `:746` (ancien `:723`) ;
    - les deux épingles `AlundraNativeSpriteRegistryTests.cs:55` et `AlundraWaveSwordLifeTests.cs:181-183` ;
    - `NativeSpriteTypeFilterForTests` : 0 occurrence dans `Alundra`, `Alundra.Tests` et `tools` ;
    - `e20-f3/oracle/tools` : 56 fichiers ; `checksums.txt` : 66 lignes ; `e20-f4f6/` absente ; ADR-0026 présent ; dernier ADR : 0075.
- **§2, affirmations : 23 verdicts.** 21 justes (dont 3 avec citation imprécise : n°13, 18, 22), 2 partiellement faux (n°8, n°10).
  - Vérifié dans les données : `alundra_7` a `ProgramLoad` 0, `ProgramTick` 94 (seul en-tête avec 94), `ProgramDeactivate` 1, drapeaux 84 \| 32<<8 \| 3<<16 = `0x32054`, corps (−10, −7, 0) de 21×15×8, `BreakEffect` 8, `Contents` 0, `Sfx` 33.
  - L'effet global 25 a 4 animations, sans son.
  - Oracle `charged7_east_wall` relu : S = 105 ; x = 346 à S+40, 349 à S+41 et S+42 ; `adj_13C` à S+42 ; statut 4 et `attach(id=8)` à S+43 ; absent à S+44 ; 10 traînées id 25, dir 3, de x = 124 à 340, v = (−3, 0, 0) ; 0 tirage.
  - Les copies `critic/bin/out/json/charged7_*.json` sont identiques octet pour octet.
  - La fenêtre de D-F4-9 (la clé précède pour x0 = 98, 101, 104, 107) est cohérente avec `sweep_wall_charged.txt` : je l'ai recalculée à la main (INFÉRENCE arithmétique).
  - Q-K1 et Q-K2 sont confirmées : D-E20-37 et D-E20-38 (`:171-174`), `:4947`.
- **§3, tests.** Groupes A, B, D, E justes ; C partiellement faux (erreur 6).
  - Aucun test n'inscrit (C, 94) ; les clés libres utilisées sont (C, 25), (D, 25), (E, 0 et 1).
  - Seul S5 lit `ProductionWiredWeaponTypes` (`:243`, `:244`).
  - `Give(6)` : 0 occurrence ; l'hôte des tests de charge est `ContactHost` (`AlundraJumpTestSupport.cs:290-344`).
- **§4, conséquences : 9 points.** 7 justes ; 4.1 (citation) et 4.6 (g4a au lieu de g4b) erronés.
  - e0c « de 7 à 9 » `:7014` ; f5 périmètre `:5980` ; garde Q-R-7 `:6386`, `:6674`, `:6969`, `:7124`, `:7266` ; total de 15 clés (INFÉRENCE cohérente).
- **§5, noms : justes.** `scan_types.external_types()` relancé (lecture seule) : aucune collision pour `chargedarrowai`, `chargedarrow`, `arrowai`, `projectileai`, `tileeffect`.
- **§6, manques : 16.** 14 confirmés, 1 requalifié (n°4), 1 non établi (n°13).
- **§0, provenance : juste.** Commits relus : `1b01353e` à `146e1d81`. Aucun commit sous `Alundra/` ni `Alundra.Tests/` après `15804ae3`. Le diff de §1.20 entre `f8f9f09e` et HEAD ne porte que sur l'ordre ferme.

## Inconnus

- **Relâchement en l'air.** Une charge relâchée en l'air existe-t-elle dans le binaire ? Si oui, la flèche naît plus haut avec la même fiche (`PlayerManager.cs:1203`).
- **Banque d'effets.** La scène d'effets du monde de la carte 14 contient-elle l'effet 25 ? `effects-global.json` a 29 effets, dont le 25 ; je n'ai pas relu le chemin de chargement de la banque.
- **Compte de la suite au HEAD.** Non mesuré. Dernier relevé : 5 097 / 0 (`docs/plan-restructuration-dll.md:803`, `:826`, `:851`).
- **D-F4-5.** L'auteur veut-il une somme par classe (D-F4-5) ou le « compte d'avant plus ses clés » (`:6394-6395`) ?
- **Vie de la flèche dans la DLL.** Le type 7 reste-t-il vivant à S+1 et atteint-il x = 1 237 à S+14 ? La phase de `+0x1C` dans une case réutilisée (O-E0A5-2) est-elle bonne ? Non mesuré.
- **Ordre E20.e / f4.** Rien ne le tranche (§1.25 propose E20.e d'abord ; g2b, g1b et la consigne d'enveloppe exigent f4 avant g).
