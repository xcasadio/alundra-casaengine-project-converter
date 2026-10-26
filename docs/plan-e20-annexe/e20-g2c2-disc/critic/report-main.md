# Critique fraîche de la découverte « g2c2 » (les torches), en lecture seule

**Méthode.** Je n'ai rien écrit dans le dépôt, lancé aucune construction ni aucun test. Le code de la DLL est lu par `git show HEAD:` et `git grep HEAD`. Le HEAD a bougé pendant ma lecture (`8174c0cb` → `5e67722d` → `6df3f265`, trois commits de l'executor b2). `Alundra/Events/EventProgramRunner.cs` est modifié dans l'arbre de travail, non committé. Aucun commit n'a touché `Alundra/Ai`, `Alundra/Entities` ni `Alundra/Combat`.

Le binaire est `ALUN_CD.EXE` (France), lu avec `bx.py` et capstone. Les rejeux tournent sur ma propre copie de l'oracle (`e20-g1a/work` plus `c4_scen.py`).

---

## 1. Verdict par fait porteur

| Fait | Verdict | Ce que j'ai refait |
|---|---|---|
| F1 données de la torche (type 393) | **CONFIRMÉ** | `sprite-records.json`, BankKey 137 : MoreFlags 0xE2, CanPickup 0x23, FlagsPortraitShadowType 0x33, C70 en tick, E20 en désactivation, corps 21×15×15, BreakEffect 10, Contents 0. `balance.json` entrée 393 : MaxHp 10, réponses 16 à tout, une seule attaque (6, puissance 1). Genre 1 : (0x23E2 >> 9) & 3 = 1. |
| « Aucun autre type ne porte C70 ou E20 » | **CONFIRMÉ** | Recensement indépendant sur les 483 cartes, tous index confondus : C70 et E20 sont seulement à l'index 137, dans 10 cartes. Le roster E14 confirme pour la page globale. |
| F2 57 placements actifs, 10 cartes, comptes par carte | **CONFIRMÉ** | Mêmes comptes. Aucun placement désactivé. |
| F3 48 au chargement, 9 par script | **CONFIRMÉ** | Directions 0xC0 (48) et 0x80 (9). Les spawners sont bien 87 C[63], 90 C[4], 94 B[1] et 92 C[15]. |
| F3 « programme scripté identique à C[46] de la 27 » | **CONFIRMÉ, avec une nuance** | Les trois programmes sont 87 C[55], 90 C[2] et 94 C[17], identiques octet pour octet à C[46] de la 27. Ils contiennent un opcode `0x00` que le rapport omet dans sa liste. Les 0x70, 0x03, 0xBD, 0x25, 0x40, 0x62, 0x8A, 0x2D, 0x36 existent à HEAD. |
| F4 seul le héros soulève ; aucun spawn de la torche par le binaire | **CONFIRMÉ, avec réserve** | Il y a trois écritures de `+0x28` côté entités : `0x8002EE1C`, `0x8003429C`, `0x800345FC`. Parmi les 102 appels à `SpawnWarpEntity`, aucune constante d'index n'est 137. Deux sites dynamiques (`0x80062280`, `0x80077250`) copient le type d'une entité existante (`type − 0x100`), donc ils ne créent une torche que si l'appelant en est une. |
| F5 arrivées des sauvegardes | **CONFIRMÉ** | `initialMapId` : 160 → 86, 161 → 91, 162 → 93, 163 → 95. Aucune sauvegarde ni aucun préréglage n'arrive en 92. |
| F6 C70 (`0x8007B7B0`) | **CONFIRMÉ** | Désassemblé. C70 est structurellement identique à C23 (`0x8006B848`), avec la table `0x800281DC` au lieu de `0x80027400` et `| 0x34` au lieu de `| 0x30`. Les mots 0 à 3 de la table valent 0, 3, 1, 4, comme `0x80027400` et `0x800270C0`. |
| F7 E20 (`0x8007F6C8`) | **CONFIRMÉ** | Pseudo-code exact. La boucle efface six mots, de `+0x84` à `+0x70` en descendant. Le type `0x187` (391) est bien celui de la goutte. |
| F8 chronologie de la torche lancée | **CONFIRMÉ** | `s8_torch` rejoué : t84 E20 (son 142), t87 flamme née, t88 torche libérée (son 41), flamme à x = 178,0 en marche et 143,5 à l'arrêt, flags `0x132146`. |
| F9 flamme sans effet, 39 ticks, et variante « nozero » | **CONFIRMÉ** | `f1_flame` rejoué. Normal : t87 à t125, codes `[0,0,0,0,0,0]`. Variante qui neutralise la boucle d'effacement : effet 17 à t91, 95, …, 123, explosion à t125, vie de 76 ticks. |
| **La prémisse de D-E20-64 est fausse** | **CONFIRMÉ** | Preuve de code (E20 efface les six codes), preuve de rejeu, et preuve DLL : le code E 0 est traité en ligne par `IsInlineDeactivateCode`. Au HEAD la flamme fonctionne entièrement sans f6b. |
| F10 flamme | **CONFIRMÉ** | `BalanceRecord.AttackForAnimation(a)` renvoie `Attacks[a+1]` si l'index est dans la table, sinon `Attacks[0]`. Animation 3 → `Attacks[4]` = (6, 1) ✓. Flags `0x132146` : HitsClassA, HitsClassB, fin d'animation, gravité, DestroyOnSlidingSlope (bit 20), pas de DeactivateOnImpact ✓. |
| F11 D34 (`0x8007E8AC`) | **CONFIRMÉ** | Désassemblé. Les fiches 238 et 239 : réponse 16 seulement au nibble 6 (index 5), 128 aux autres. Voir aussi le point 3.5 : la propagation passe de [INFÉRENCE] à « portée par les données ». |
| F12 A19 et C71 | **CONFIRMÉ avec correction** | Machine à 5 états, mais le rapport décale les états 2 et 3 (voir §2). La liste de 56 gouttes est confirmée. |
| F13 les 16 usages de `0x87 [128, 6]` en carte 92 | **CONFIRMÉ** | Mon propre balayage donne D5, D8, D12, D19, D34 à D37, D40 à D43, D53 à D56. Les autres cartes à torches n'ont aucun test du feu : la 90 teste le nibble 3 (5 fois), la 442 le nibble 1 (8 fois). L'interrupteur `sprite 51` répond 16 au nibble 6. |
| F14 adresses des tables natives | **CONFIRMÉ** | Les cinq entrées pointent exactement sur les adresses du rapport, sans alias. |
| F15, F16, F17 DLL à HEAD | **CONFIRMÉ** | Aucune occurrence de torch, bramble, goutte, 393, 238, 239 ou 391 dans `Alundra/`. 11 appels d'enregistrement dans `CreateForProduction`. 25 types câblés (`PlayerManager.cs:972`, liste écrite à la main dans `LiftPureTests:140`). `ReleaseFlagsPolicy` à `LiftableAi.cs:118`, `TickCarried` à `:182`, `Break` à `:71`, spawn en `ChestAi.cs:78`. |
| F18 / F20 f6b non commencée | **CONFIRMÉ** | Pas de `BallAi.cs` dans `Alundra/Ai/Projectiles`. |
| Risque 2 « le héros n'est pas blessé par sa torche, INCONNU » | **MESURÉ** (je ferme l'inconnue) | Voir le point 3.2. |
| « 161 ronces » / « écart de 4 avec le roster, cartes de test » | **NON VÉRIFIABLE** | Je retrouve bien 63 + 98 = 161 sur 24 cartes. La cause de l'écart avec le roster (61 et 96) reste une inférence. |
| « Meia Dream » pour les cartes 441 et 442 | **NON VÉRIFIABLE** | Non relevé, dit par le plan. |

---

## 2. Corrections

1. **F6, ordre des bits.** Le rapport écrit « garde les bits d'arrêt 0x10, 0x20 et 0x40 ». La torche posée (`0xE2`) n'a que 0x20 et 0x40 ; elle **gagne** 0x10 et 0x04 au lâcher. Résultat : `0x3323E2 → 0x332376`, la valeur du rapport est juste.
2. **F12, états de C71.** La table de sauts est `0x800281EC`. Les états réels sont :
   - 0 : armer 40 ticks.
   - 1 : décompte, puis animation 1 et gravité.
   - 2 : au contact `+0x140` ou `+0x144`, animation 2 (son 145), passer à l'état 3.
   - 3 : mettre `+0x274 = 4`, retirer `Collidable` (`&~0x80`), puis regarder le support `+0x12C`. Si le support est le héros, lire son objet porté `+0x2C`. Si l'entité est de type `0x189`, forcer son statut à 3 et son `+0x274` à 1.
   - 4 : à la fin de l'animation, remise à zéro, `+0x54` restauré, `+0x11C` augmenté de 160 px.

   Le rapport met « Collidable retiré » dans l'état 2 et le test de la torche dans « 3 » sans dire qu'il n'a lieu qu'**un seul tick**, celui de l'entrée dans l'état 3.
3. **F17, `LiftableAi.Break` « modèle exact d'E20 ».** Faux sur un point : E2 détruit aussi quand `(CombinedVramFlagsAND & 4) != 0`. E20 n'a pas cette clause, vérifié au désassemblage. E20 a en plus le spawn et la règle de contact `+0x130`.
4. **Risque 5.** « Si f6b se fait ensuite » contredit l'ordre par défaut (D-E20-64 : g2c2 après f6b). Avec l'ordre par défaut, le test « la flamme n'émet rien même si `(C,60)` est enregistrée » est actif dès g2c2.
5. **F18.** Le plan ordonne e0d, b2, **c2b**, e0k5, f6b, puis g2c2. À l'arrivée de g2c2 la blessure du héros **existera** (D-E20-83). Les recettes et les tests doivent donc compter qu'Alundra perd des PV dans une flamme, et non plus seulement des étincelles.

---

## 3. Manques et cas oubliés

**3.1 La torche porte `DeactivateOnHit` (0x20) : tout coup de sa propre attaque l'éteint.** [FAIT code, MESURÉ oracle]
- La passe de coups fait `attacker.HitCounter++` pour toute cible touchée, même **immune** (`HitPass.cs:167`). Le tick suivant, le choix d'état fait passer la torche en statut 3 (`EntityScriptProxy.cs:1954`).
- Le rapport ne rejoue que l'arrivée au sol (flag 0x10). Le chemin « contact » est un deuxième scénario de naissance de la flamme.
- Mesure (`p_hitswitch.txt`), torche lancée contre une cible ClassA, passe de coups réelle :
  - t79 : coup. La cible reçoit `+0x224 = torche` (cooldown de la cible : 25).
  - t80 : statut 3 et animation 2 (premier appel d'E20).
  - t83 : flamme née (statut 1, animation 3), position du contact.
  - Cadence : flamme = coup + 4 ticks ; atterrissage = atterrissage + 4.
  - La flamme retouche la même cible à t104, quand le cooldown de 25 ticks est fini.
- Conséquence pour les interrupteurs de la 92 : c'est la **torche elle-même** (attaque (6, 1), nibble 6) qui allume l'interrupteur, avant toute flamme. La sonde P-T-4 doit être jouée avec la torche, pas seulement avec la flamme.
- Cas voisin [INFÉRENCE géométrique] : la plaque de la torche posée est à z + 24, 18×12×1, entièrement dans l'emprise 21×15 du corps. Elle ne touche le héros que s'il est **debout sur** la torche. Alors la torche posée s'allumerait d'elle-même.

**3.2 Auto-blessure : MESURÉ, non.** (`p_selfhit.txt`, passe de coups réelle, vérifiée par `g7_hit`)
- Lancer en marche, lancer à l'arrêt, et torche portée 100 ticks : `HitCounter` de la torche = 0, `+0x224` du héros = 0 pendant tout le vol, la flamme naît à 109 + 69 px du héros.
- La sonde P-T-1 de la DLL devient une **comparaison** avec ces valeurs. L'origine du « pas de coup » est géométrique (boîtes), pas une exclusion du lanceur.

**3.3 Carte 92 : le levier lâche quatre torches qui s'enflamment à l'atterrissage.** [FAIT binaire + données, MESURÉ]
- C[15] est le programme du « levier » (rec 14, C143/D143).
- Il attend le drapeau 150, met le drapeau 151, puis fait naître 4 torches (rec 68) par `0x8A`, aux points (216 ; 424), (288 ; 424), (216 ; 456), (288 ; 456) en pixels, Z = 176.
- Il enchaîne `0x62 [98, 68, 16, 0]`, qui fait `Flags |= 0x0010` (**DeactivateOnImpact**, recherche par identifiant d'enregistrement 68) sur ces quatre torches, puis le son 55.
- Le rapport cite la pluie de torches mais pas le `0x62`.
- Mesure (`p_rain.txt`) : une torche tombant de 55 px avec le bit 0x10 posé en l'air atterrit à t15, E20 au t16, flamme née à t19 (atterrissage + 4), vie de 39 ticks. Sans le bit, la même torche reste inerte.
- Le chemin « atterrissage sans lancer » est donc **un chemin de production** : il faut une sonde, un test et un point de recette (le levier de la 92), avec des flammes à l'arrivée sur Alundra.

**3.4 A19 lit `+0x54` : c'est `ProgramIndexes[2]` (l'index du programme C scripté).**
- La DLL déclare `ProgramIndexes` en `int[6]` à `+0x4C`, donc `+0x54` = index 2.
- A19 le range en `+0x275` (`Bytes[1]`). L'état 4 de C71 le **restaure** : le programme scripté (« attendre N ticks », puis `0x40 [2, 0]`) est réarmé, et le cycle recommence.
- Sans cette restauration, une goutte ne tombe qu'une fois. Le rapport ne dit pas ce que contient `+0x54`.
- Autres écritures de la phase D : `+0x275`, `+0x282` (= `AIValues[1]`, compteur de 40) et `+0x11C` (+160 px).

**3.5 Les ronces : les données portent la propagation et un lien avec les coffres.**
- Une ronce qui brûle (animation 1) a `AttackForAnimation(1) = Attacks[0] = (6, 1)` et les flags `0x47`. Elle touche donc les autres ronces (ClassA) et le héros. [FAIT données ; le rejeu manque]
- Les ronces 238 sont aussi sur des cartes de l'histoire : roster `first_story_map` = « 14: Overworld 3,1 », avec les cartes 2, 3, 4, 7, 13, 14 dans la liste. Q1-B les laisse indestructibles là aussi.
- En carte 86, chaque groupe de torches est dans la même zone qu'un groupe de ronces. Le groupe (98 à 100 ; 46 à 48) est au pied du coffre (rec 8, en (100 ; 38)). [INFÉRENCE forte] Q1-B rendrait ces coffres inaccessibles au joueur.
- Enregistrer D34 n'allumera rien tout seul. Dans les 13 cartes qui ont à la fois des ronces et des attaques de feu (`fire_near_brambles.txt`), la plus proche attaque de feu autre qu'une torche est à plus de 5 cases d'une ronce.
- Une torche posée a `HitsClassB` seul : elle ne touche pas les ronces (ClassA). Seule la torche lancée (`0x76`, `HitsClassA`) les touche.

**3.6 Piège dans `TickCarried`.** [FAIT]
- La branche C4 est le `_ =>` du `switch` sur `ReleaseFlagsPolicy`. Ajouter une valeur `C70` sans cas explicite tomberait **silencieusement** dans la logique C4 (flags inchangés pour les codes 1 à 3).
- Il faut restructurer le `switch` avec un cas `C70`, et un test qui tue le mutant « C70 pris pour C4 ».

**3.7 Aucun appelant caché par script.**
- Balayage des 483 cartes : aucun `0x41` ne câble C70, E20, D34, C71 ni A19 à l'exécution. Les seuls `0x41` sont `(2,4)`, `(2,13)`, `(2,64)`… et deux D.
- Le roster confirme que seuls 238, 239, 391 et 393 portent ces clés.

**3.8 Tests de garde à ajouter**, sur le modèle de `LiftableAiTests:130` : « les types dont l'en-tête porte C70 ou E20 sont {393} », « A19 et C71 sont {391} », « D34 est {238, 239} » dans l'export. Ils tuent le mutant « type câblé sans sa clé » sans passer par un monde.

**3.9 Détails d'implémentation qui sortent du rapport.**
- `SpawnEntityBySpriteIndex` : le Z est dans le repère de la DLL ; l'interface ajoute l'unité brute (D-E0A-1). Passer `PosZ` tel quel et le comparer à l'oracle (z = 0,0).
- Un son d'apparition peut être demandé par `RequestAppearanceSound`, alors que l'oracle donne `snd(0)` à la naissance de la flamme. Le journal des sons du T0 doit le comparer.
- Détruire une torche portée (C71 sur le héros) appelle `ClearCarrierOf`, qui met `CarriedEntity` à `null`. L'état du héros (posture portée sans objet, `CarryLockCounter`) n'est exercé nulle part. À sonder avant la phase D.
- Les gouttes sont aussi dans la carte 158 (« Fairy cave », 14 gouttes, **aucune torche**). La phase D change donc une carte sans torche (hors chaîne des 30 cartes, d'après le roster).

---

## 4. Tests existants

La liste du rapport est **exacte** (4 sommes de clés, 1 liste de 25 types, 2 témoins « non câblé »), sept tests au total :
- `NativeSpriteRegistryTests.cs:58` ;
- `LiftableAiRegistrationTests.cs:77` ;
- `ChargedArrowAiTests.cs:239` ;
- `WaveSwordLifeTests.cs:185` ;
- `LiftPureTests.cs:140` ;
- `LiftWorldTests.cs:41` et `:293` ;
- `LiftMap72ArcTests.cs:39` et `:237`.

J'ajoute :
- Les deux témoins d'un type genre 1 non câblé se rabattent sur les graines (global 1) ou la bombe (global 2). Ce sont les deux seuls types de genre 1 de l'export restés hors ensemble après g2c2. Une valeur synthétique (par exemple 511) marche aussi.
- Le test de liste des préréglages (`TestSavesTests`) bouge à chaque préréglage ajouté. b2 et c2b en ajoutent aussi : la liste exacte dépendra de l'ordre d'exécution.
- Pour le reste, je n'ai trouvé que `C4WorldTests.cs:631-633` (cartes 86, 87, 93) et `C4RegistrationTests.cs:37`, qui restent verts. Aucune autre carte de Magyscar n'est chargée par un test.

---

## 5. Risques ajoutés

- **Chemin d'atterrissage de la 92** (point 3.3) : il exige que les entités nées de `0x8A` (« jamais marquées », D-T-12) prennent `DeactivateOnImpact` par `IsOnGround`. Le rapport n'en parle pas.
- **Q1-B** risque de bloquer des coffres de Magyscar et des ronces de l'histoire (point 3.5).
- **Une flamme née dans la cible** la retouche toutes les 25 ticks : à épingler par un test, sinon la cadence dépend de la passe de coups.
- Le rapport traite 31 cartes avec des ronces ou des torches : la recette est partielle (les enjeux E14.b des ennemis des cartes 86 à 94 restent à faire), il faut le dire au joueur.

---

## 6. Questions à l'auteur (défauts « comme l'original », selon ta consigne « choix par défaut »)

Je reprends les quatre questions du rapport avec ces défauts : **Q1 A** (D34 dans g2c2), **Q2 A**, **Q3 A** (après f6b), **Q4 A** (160 plus préréglage). Je précise pour Q2 :
- **Q2 A est ambigu** (« phase D ou g2c3 »). Je recommande **g2c3** à part : 89 mots, une restauration de `+0x54`, une carte sans torche changée (158), une sonde de posture du héros. C'est la seule façon de garder g2c2 petite.

Deux questions nouvelles, en termes de jeu :
- **Q5. Le levier de la salle 92 lâche quatre torches qui s'enflamment en touchant le sol.** Comme l'original (défaut) : elles brûlent en tombant, c'est un piège de flammes à l'endroit du levier. Sinon : elles restent des torches posées, ce que l'original ne fait pas.
- **Q6. Une torche lancée s'allume dès qu'elle touche quelque chose, même sans dégât.** Comme l'original (défaut) : elle se consume au premier contact (interrupteur, ronce, ennemi) et la flamme naît sur place. Sinon : elle roulerait jusqu'au sol, ce qui change les énigmes.

---

## 7. Scripts et sorties

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/g2c2-critic/`

**Scripts**
- `bx.py`
- `t1.py` à `t5.py` (tables d'animation, E20, jeu de sauts de C71, balayage de `jal 0x80039F58`, stores en `+0x28`, vérification des tables)
- `census2.py`, `scan87b.py`, `scan41.py`, `firenear.py`
- `dump_prog.py`, `dump_map.py`
- `replay/work/` (copie de `e20-g1a/work`), `replay/c4_scen.py`, `replay/c4_full.py`, `replay/f1_flame.py`
- `replay/p_selfhit.py`, `replay/p_hitswitch.py`, `replay/p_rain.py`

**Sorties dans `out/`**
- `census2.txt`
- `dis_c70_e20_d34_a19_c71.txt`
- `handler_tables_check.txt`
- `spawnwarp_dynamic_sites.txt`
- `stores_plus28.txt`
- `scan_0x87_torchmaps.txt`
- `scan_0x41_all_maps.txt`
- `fire_near_brambles.txt`
- `torch_programs.txt`
- `replay_f1_flame_critic.txt`
- `p_selfhit.txt`
- `p_hitswitch.txt`
- `p_rain.txt`

**Limites de mes rejeux**
- La cible de `p_hitswitch.py` est restée à x ≈ 166 quel que soit `tx` (placement non pris en compte) : les chiffres valent pour ce contact, pas pour une position réglable.
- `p_rain.py` pose le bit 0x10 à la main au tick 4, comme `0x62`, sans faire naître la torche par `0x8A`.