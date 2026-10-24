Je n'ai pas pu écrire `critic/report.md` : l'outil Write a refusé (« les sous-agents rendent leurs résultats en texte »), donc tout est ici. Les preuves reproductibles (listings, scripts, rejeux) sont sous `scratchpad/g2c1-disc/critic/` : `c4_dis.txt`, `phys_370c4.txt`, `phys_38064.txt`, `trylift.txt`, `cdis.py`, `scan_off.py`, `evlib.py`, `t_q*.py`, `sim/out/`. Le `report.md` du lecteur n'existait pas à 14:34. J'ai jugé les énoncés du T0 du 2026-10-22 et du plan §1.24.5, et je n'ai lu les fichiers de travail du lecteur qu'après avoir dérivé mes réponses. HEAD : début `73c05530`, fin `d23804cf` (g3 T1 est arrivé pendant ma lecture). Aucun build, aucune écriture dans le dépôt.

- **Q1 : CONFIRMÉ, avec deux corrections.**
  - `+0x18C` n'est écrit qu'en `0x80038348` (`UpdateTileAttributes`, `0x80038064`). Il est lu par le choix du programme (`DestroyOnSlidingSlope`), par C4 (le tronc 373 seul), par E9 et E17 (boules et flèches) et par le code du héros.
  - La DLL a déjà un équivalent : `UpdateGroundSlope` est appelé pour les entités marquées `HasLogicalZ` (`EntityScriptProxy.cs:1822-1827`), et `PutInHands` pose ce marqueur. Seuls manquent les 8 troncs posés sur l'eau et jamais soulevés (carte 70 recs 9-12 et 21, carte 73 recs 11, 12, 36) ; leurs mots de coins `0x0800`/`0x1800` le confirment dans l'export.
  - Le défaut « type 373 seul » est suffisant.
  - Le chiffre « 444 instances » n'est pas reproduit : je compte 2 277 placements à `0x100000` avec gravité, dont aucun sur l'eau profonde (NON ÉTABLI).
- **Q2 : CONFIRMÉ, et la question « peut-on les soulever » reçoit sa réponse : non.**
  - Il y a 56 troncs de page de carte, 13 natifs et 43 scriptés, sur les cartes 72, 73, 79, 140 et 158. Aucune n'est dans la chaîne.
  - Aucun `0x40` dans leurs programmes, et aucun `0x42`/`0x43` ne les vise.
  - Le script A de chacun efface `0x80` (Collidable) : flags finaux `0x232306`, `0x232302` ou `0x236306`. Ce sont des « boules de fer » qui patrouillent et blessent.
  - Sans Collidable ils ne sont jamais contact ni porteur, donc non soulevables (INFÉRENCE solide). L'hypothèse « collé aux mains » tombe.
- **Q3 : RÉFUTÉ dans la forme « une seule fonction ».**
  - `0x800370C4` est l'unique producteur des 4 pointeurs `+0x148..`.
  - `0x80038064` en est un premier lecteur : mots de 32 bits, coin qualifié par `hauteur+1 == +0x1E0`, il produit `+0x180`/`+0x184`.
  - C4 est un second lecteur : `lhu` des mêmes pointeurs, sans qualification de hauteur, test `& 0x1001`.
  - La DLL n'a que le premier, pour le héros seulement (`UpdateVramFlags`). Les lectures de `E2` et du choix du programme sont donc mortes pour les objets.
  - Les briques du second existent dans `Alundra/Physics` : `SampleGroundProperty`, `SampleRawWalkability`, `BinaryColumn/Row`. Aucun besoin du moteur.
  - O-G-2 : la mort a lieu au tick d'atterrissage, pas « pendant le vol ».
- **Q4 : CONFIRMÉ.**
  - `+0x80` est l'octet `ProgramDeactivate` de l'en-tête (écrit à `0x80039E04`) : 2 pour 290, 293, 373, 0 pour 363-369.
  - La DLL l'expose déjà : `SpriteProgramIndexes[ProgramEDeactivate]` (`:162`, `EntitySpawnFactory.cs:779`).
  - Il est variable à l'exécution (`0x41 [E,0]` sur les blocs de glace), donc C4 doit lire la valeur vive.
- **Q5 : CONFIRMÉ.**
  - sm1 n'est pas exécutée : aucun `case 0x60` dans le runner ; elle est READY mais « à réviser » (D-E19-115, D-E19-122).
  - La carte 22 (save 66) n'a aucun site `0x60` : sa recette ne dépend pas de sm1.
  - Les cartes 27 (24 sites, save 96) et 29 (8 sites, save 91) en dépendent pour les plaques.
  - Défaut : g2c1 ne dépend pas de sm1 ; sa recette se limite à la 66.
- **Q6 : CONFIRMÉ, oui depuis D5b.**
  - `ScriptedMotion.cs:651-660` ajoute la force planifiée de chaque porteur à toute entité ; `EntitySupport.cs:296-320` calcule `RidingEntity` pour `(Flags & 0x4100) == 0x100`, ce que remplissent les 7 types.
  - ADR-0088 (`567d8080`) ; tests `PassengerPlatformFollowTests.cs:149`, `:269` (PileXYA), `:313`.
  - L'énoncé « `:677` héros seulement » est périmé. Réserve : aucun test n'a pour porteur une plaque (`NoRiders`), c'est la sonde P4.
- **Q7 : CONFIRMÉ, avec trois corrections.**
  - `+0x278` existe déjà : `DelayOrAngleOrEntityId` (`EntityScriptProxy.cs:1350`).
  - L'ensemble câblé n'existe pas au HEAD : seul `ProductionWiredWeaponTypes` est présent. Le nom prévu est `ProductionWiredLiftableTypes` (D-G-8, §1.24.4 phase B). `PlayerTryAction` est encore un bouchon (`PlayerManager.cs:955`).
  - Le lâcher est à paramétrer : C23 fait toujours `|0x30 & ~0x80`, C4 ne change aucun drapeau aux lancers 1-3, et au code -1 il fait `|0x10 & ~0x80` seulement si `+0x80 == 2`. Les trois tables de lâcher sont identiques (0, 3, 1, 4).
  - Il manque à créer : `C4Tick` (`LiftableAi.cs:102` ne fait que C23), les clés `(C,4)` (libres), le lecteur de coins, l'étincelle sans forces (`EffectStage.Emit`), et un ajout à trois pins de somme (`WaveSwordLifeTests:185`, `ChargedArrowAiTests:237`, `LiftableAiRegistrationTests:75`). Rien ne demande le moteur.
- **Q8 : CONFIRMÉ pour les valeurs, avec réserves.**
  - J'ai rejoué S2, S3, S5, S6 et S7 sur la machine MIPS réelle. P1 : effet 9 à T+1 en (109 ; 100 ; 44) pour 290, 293, 373, et à T+2 en (110 ; 100 ; 47,5) pour les objets. P2 : glisse jusqu'à X=250, flags inchangés. P3 : `HitCounter` à t82 (T+21), statut 3 à t83. P4 : atterrissage à t81 (T+20) en (172 ; 100 ; 16), `support == plaque`.
  - Cartes réelles : tonneaux de la 22 (recs 21 et 33, murs `0x1041` en colonne 48 près du rec 33), pierre scriptée rec 34. Des cases `0x1001` pures existent sur la 22 (123), la 24 (30), la 70 (19) et la 113 (26) ; `0x1041` est un mur.
  - Je n'ai pas rejoué les sondes sur cartes réelles : le test de la DLL doit asserter la relation dérivée, pas une constante.
- **Q9 : MANQUE MAJEUR, voir ci-dessous.**

Manques :
1. **C4 est aussi acquis à l'exécution par script (`0x41 [C,4]` puis `0x40 [C,0]`).** Le T0 comptait par en-tête et concluait « aucune carte de la chaîne ne porte un C4 » ; c'est faux à l'exécution.
   - 26 articles de boutique, 11 types globaux (48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113), sur les cartes 134, 163, 258, 298 et 398. La **163 (Inoa, boutique de Jess) est dans la chaîne** et en porte 5.
   - Aussi 4 blocs de glace (cartes 103, 105, 108, qui passent aussi en E0).
   - Le registre étant par type, ces types sont hors des 7 prévus.
   - Je n'ai pas trouvé d'achat par transport dans les scripts de la 163 (NON ÉTABLI).
   - Question à l'auteur : en boutique, l'original laisse soulever et jeter potions, herbes et vestes. Faut-il les porter (11 types de plus et lecture des scripts de boutique) ou les laisser immobiles comme aujourd'hui ? Défaut proposé : les laisser, et documenter l'écart.
2. **Les objets C4 lancés restent Collidable et à gravité** (C23 les rend non collidables).
   - L'arrêt de g1b « la boucle sans bouton relève un objet lancé » doit être limité aux types C23.
   - Un tonneau lancé dans un couloir le bouche : c'est fidèle, à dire à l'auteur.
3. **O-G-2 reste sans propriétaire.** Les 7 types portent `0x200000` : atterris sur un trou, ils disparaissent dans l'original et restent debout dans la DLL. Un appel à `UpdateVramFlags` pour les marqués le corrigerait, mais tuerait aussi les autres marqués à `0x200000` (décision avec f6).
4. **Le rebond contre un mur n'a lieu que pour un contact pendant la montée** (mur à moins de 4 px du héros). La recette ne le montrera pas.
5. **Les 12 placements C-scriptés qui rendent la main à C4** (par exemple la pierre de la 22, rec 34) ne sont pas rejoués avec C4 réel après `0x40` (NON ÉTABLI) ; à mesurer dans la sonde.
6. **Huit troncs bougent au chargement** si on alimente `+0x18C` (cartes 70 et 73, zone Coastal Cave) : changement visible à signaler.

Non établi : le chiffre « 444 », l'achat en boutique, les valeurs des sondes sur cartes réelles, et la construction de la liste des collidables au binaire.