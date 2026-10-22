# Rapport T0 de lecture seule, E20.g2c (C4, plaques, torches)

HEAD lu : `1d0b6953`, branche `chantier/theme1-porter-plateforme`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8da…5b6445` (vérifié). Je n'ai rien modifié ni indexé dans le dépôt. `git status` n'y montre que des fichiers antérieurs à ma session : `tools/Saves/`, `tools/Convert-*.ps1`, deux sous-modules « m » datés des 3 et 10 octobre.

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g2c-disc/`. Il contient :
- `c4_full.py` : modèle de C4, vecteurs, mutants, goldens.
- `c4_scen.py` : scénarios S1 à S10.
- `c4_goldens.json`.
- `out/` et `MANIFEST_out.sha256`.
- `census_c4.txt`, `census_scripted.py`, `rest_census.txt`, `refused_headers.txt`, `cell_census.py`.
- `dis_c4.txt`, `dis_c70.txt`, `dis_e20.txt`.
- `dump_map.py`, `dump_prog.py`.

Les scénarios tournent sur la machine MIPS copiée de `docs/plan-e20-annexe/e20-g1a/work/` (code réel de `MovePlayer`, du spawner d'enregistrement, de C4, C23, E2 ; les autres gestionnaires sont inertes). Les écritures du harnais sont : manettes, champ de cases, placement de l'objet, zone de naissance élargie, choix d'un gabarit de placement natif, plaque ou plateforme posées. Sept scénarios rejoués deux fois donnent des sorties identiques à l'octet.

## 0. Verdicts

1. **Aucun bloc ne se pousse.** Les 7 en-têtes C4 sont tous « transportables » : on les soulève et on les lance. Le héros qui marche contre un objet posé ne le déplace pas : l'objet reste à X = 178,0 pendant 36 ticks de poussée (FACT, oracle S4). Le code de C4, d'E0 et d'E2 n'écrit aucune force. Le mot « poussable » de O-E20-21 et de D-E20-46 désigne donc ici le transport, pas la poussée.
2. **Les vrais poussables n'ont pas de propriétaire dans les plans.** Ce sont le « Pilier poussable (PRG) » (C76 natif, `0x8007C024`, 24 placements sur 8 cartes, dont 11 natifs), la « Statue poussable (événement) » (62 placements, 11 cartes, scriptée), le « Bloc à pousser/attaquer » (9 placements, 4 cartes) et l'« Interrupteur poussoir ». Une recherche textuelle dans `docs/` ne trouve C76 dans aucun plan. À signaler à l'auteur (§12).
3. **C4 est entièrement décodé** : 157 mots, `0x80066984` à `0x80066BF4`. Mon modèle égale le code réel sur 20 000 vecteurs, avec 157/157 instructions couvertes (§2).
4. **O-G-9 est tranchée par exécution.** Un tonneau, une pierre ou un tronc lancé qui touche une cible de classe A la blesse. L'objet se brise alors comme une caisse. Les 4 « Objets » (étoile, lune, eau, soleil) n'ont aucune attaque : ils ne touchent rien (§6).
5. **O-G-10 est expliquée.** Ce ne sont pas des en-têtes « refusés » : c'est la zone du spawner (`0x8003A208`-`0x8003A25C`) qui exclut la case (4, 4) du héros du harnais. Avec la zone élargie, tous les en-têtes naissent. Le verrou de contrôle est rejoué en tick complet sur AttackCount 0 et 7 (§7).
6. **L'esquisse se trompe sur le périmètre.** Les blocs de glace (sprite 304, C23) et les rochers tortue (182, C23) ne sont pas C4. C4, ce sont les 191 placements de 7 en-têtes (§1).
7. **Les plaques lisent le support** (`+0x12C`, `RidingEntity` dans la DLL). Une pierre lancée atterrit sur une plaque, y reste, et sert de support à une autre (§5).
8. **La torche n'a besoin ni d'E14 ni de f6a.** Son vol réutilise le code de portage de C23. E20 fait naître une flamme de type 12 par le spawner global, et les gestionnaires du type 12 sont ceux de **f6b**. La torche sert à brûler les « Ronces enflammées » via D34, un tout petit gestionnaire de toucher (§8).
9. **Au HEAD, la DLL ne fait rien de tout cela.** `LiftableAi`, `CarryRules`, `PutInHands` et `ProductionWiredLiftableTypes` n'existent pas. `PlayerTryAction` et `UpdatePlayerCarriedEntity` sont des bouchons. Le registre de production a 7 clés. Les opcodes `0x60`/`0x61` (sm1) sont absents. f4, f5, f6a et f6b sont ⏳ (§9).
10. **Aucune des 30 cartes de la chaîne d'histoire ne porte un C4 ou une torche.** L'intersection est vide, donc les arcs de la chaîne ne bougent pas.

## 1. Ce que sont les 191 et les 57 (FACT, `census_c4.txt`, `census_scripted.py`)

Type de page = `0x100 + indice de sprite`.

| Sprite | Type | Nom | Placements (cartes) | Natifs au chargement | Scriptés en C |
|---|---|---|---|---|---|
| 34 | 290 | Tonneau, C4/E2 | 76 (39) | 69 | 7 |
| 37 | 293 | Pierre, C4/E2 | 27 (14) | 25 | 2 |
| 107 | 363 | Objet étoile, C4/E0 | 8 (cartes 27, 29, 30, 400) | 7 | 1 |
| 111 | 367 | Objet lune, C4/E0 | 8 (mêmes cartes) | 7 | 1 |
| 112 | 368 | Objet eau, C4/E0 | 8 (mêmes cartes) | 7 | 1 |
| 113 | 369 | Objet soleil, C4/E0 | 8 (mêmes cartes) | 7 | 1 |
| 117 | 373 | Tronc (petite boule de fer), C4/E2 | 56 (70, 72, 73, 79, 140, 158) | 13 | **43** |
| 137 | 393 | Torche, C70/E20 | 57 (86 à 90, 92 à 94, 441, 442) | 49 | 8 |

Total C4 : 191 (76+27+32+56), dont 135 avec C natif dès le chargement.

Les 7 en-têtes C4 sont exactement les « objets des plaques » de l'E19 : sprites 34, 37, 107, 111, 112, 113 et 117. Ce que l'esquisse range sous C4 sans y appartenir : bloc de glace 304, rocher tortue 182.

Détails à retenir :
- **Drapeaux (`+0x6C`)** : tonneau et pierre `0x3323E4`, tronc `0x2323E4` (sans `0x100000`), objets `0x332384` (sans `0x20`). Tailles : 21×15×16 pour tonneau, pierre et tronc, 20×12×14 pour les objets.
- **AttackCount** : 1 pour tonneau et pierre, 7 pour le tronc, 0 pour les objets.
- **Un programme C scripté remplace C4** : `EventCodesC` non nul fait tourner le bytecode (`0x8004205C`) à la place du natif. Sur les 56 placements C-scriptés, 12 reviennent au natif par `40 02 00` (6 tonneaux, 2 pierres, 4 objets). Le programme dump (`C[46]` de la 27, `C[35]` de la 22) est « tomber du ciel, jouer le son 24, puis rendre la main au natif ». Les 43 troncs scriptés ont `A` et `C` de patrouille (carte 72 : `1A 07`, attendre une force ajustée, inverser la direction, redémarrer) et **aucun** `0x40`.
- **Conséquence pour l'oracle** : les gabarits d'`add_crate` doivent être choisis sur un placement natif. J'ai vu dans l'oracle un objet à C scripté rester bloqué en main : l'étape de lancer s'est jouée (`t24` = 2) mais rien n'a rendu l'objet. Cela porte sur un script que je n'ai pas exécuté (voir INCONNU §13).

## 2. Le corps de C4 (FACT : disassemblage capstone `dis_c4.txt`, puis modèle contre le code réel)

Offsets de l'entité : `+0x10` statut, `+0x24` code de lâcher en attente, `+0x28` porteur, `+0x68` type, `+0x6C` drapeaux, `+0x80` code natif E, `+0x88` animation cible, `+0x8C` direction, `+0xB8` ForceZ, `+0x13C` ForceAdjusted, `+0x144` IsOnGround, `+0x148..+0x154` pointeurs des 4 coins, `+0x18C` pente, `+0x220` HitCounter, `+0x278` verrou de rebond (octet, nouveau pour la DLL).

```
si +0x28 != 0 (porté, 39 mots, déjà modélisé en g1a) :
    t = +0x24
    si t == 0 : +0x88 = 0 ; fin
    si t == -1 : +0x88 = 3 ; si +0x80 == 2 : +0x6C = (+0x6C | 0x10) & ~0x80
    sinon      : +0x88 = TBL[0x800270C0 + 4t]     (1->3, 2->1, 3->4 ; identique à C23 et C70)
    +0x24 = 0 ; porteur.+0x2C = 0 ; +0x28 = 0 ; fin
a = +0x88
si a != 0 et non (+0x68 == 0x175 et a == 5) :
    si +0x13C != 0 (bloqué) :
        si +0x144 == 0 (en l'air) :
            si +0xB8 > 0 (signé, il monte) et +0x278 == 0 :
                +0x278 = 1 ; +0x8C = (+0x8C + 0x10) & 0x1F
                effet(0, 9, 0, X, Y, Z + 0x80000)       # 0x8003BDD8 : Y et Z passés par la pile
        sinon (au sol) :
            si un des 4 coins a (mot & 0x1001) == 0x1001 : +0x10 = 3
            +0x88 = 0
    sinon (non bloqué) :
        si +0x144 != 0 ou +0x220 != 0 :
            son 0x18 (0x800490FC) ; +0x278 = 0
            si aucun des 4 coins ne vaut 0x1001 : +0x88 = 0
si +0x68 == 0x175 (tronc) :
    si +0x88 == 0 et +0x18C == 4 : +0x88 = 5 ; fin
    si +0x88 == 5 et +0x18C != 4 : +0x88 = 0
```

- **Le mot de coin** est `walkabilité | ground_property << 8`. `0x800370C4` calcule les 4 pointeurs depuis `TILES + 0x604 + rangée·416 + colonne·8` aux coins de la boîte (écriture en `0x800371D8`). Le test `& 0x1001` est vrai quand les bits « bloque classe A » (`0x1000`) et « bloque classe B » (`0x01`) sont tous deux posés (doc de la DLL, `CellsCollisionField.cs:141-158`).
- **`+0x80`** vaut la valeur du code natif E (observé : 2 pour E2, 0 pour E0).

**Preuves.**
- 20 000 vecteurs aléatoires (graine fixe) : **0 écart**, 157/157 instructions atteintes. Classes de sorties : porté (5 000), libre sans effet, libre avec son, avec effet 9, avec statut 3.
- 150 mutants d'un mot sur le code réel : 134 tués, 16 survivants, **tous équivalents** : cadre de pile ±1 (4), décalages de sauvegarde et de restauration de `$s0` et `$ra` (4), `move` remplacé par `subu` ou `negu` (7), et `addiu $v0, 4 → 5` dans le délai de saut `0x80066B64`, mort à cause de la comparaison suivante.
- **Goldens** : `c4_goldens.json`, 1 244 vecteurs (une par classe), 726 279 octets, **SHA-256 `59167009656660d68b4847485b01f1b7a8426f914429525bcccb74d6b267fe90`**, identiques sur deux exécutions. Forme : entrée éparse, sortie différentielle, journal des appels.

## 3. Rejeu sur la machine (valeurs de référence, T = tick du front de Carré = 61)

**Lancer à plat, vers l'est** (héros en 109, objet levé à t11). Tonneau, pierre, tronc et les 4 objets donnent la même chronologie qu'une caisse :
- À T : animation cible 1 (marche tenue) ou 3 (immobile). X = 112,0 ou 110,5. Z = 36 puis l'arc de la caisse.
- Contact au sol à **T+22** (t83) : X = 178,0 ou 143,5, `IsOnGround` = 1.
- À **T+23** : son 24 et animation 0.
- Drapeaux **inchangés** : `0x3323E4` (tonneau, pierre), `0x2323E4` (tronc), `0x332384` (objets). Le statut reste 2 pour toujours.
- Contraste, bloc de glace C23 : drapeaux `0x3323C4 → 0x332374` à T, statut 3 à T+23, statut 4 à T+25, libre à T+26.

**Rebond contre un mur.** Mur en colonne 5 (cases de 24 px, x 120 à 143), le contact a lieu à T pendant la montée.
- À T+1 (t62) pour tonneau, pierre et tronc : direction 24 → 8, verrou à 1, effet 9 à (109,0 ; 100 ; 44,0) (Z de t61 = 36, plus 8 px). Pour les objets, boîte plus étroite (X de départ 110,0) : un tick plus tard, t63, effet en (110 ; 100 ; 47,5).
- L'objet repart vers l'ouest à la même vitesse. Sol à T+24, puis à T+25 : animation 0, verrou remis à 0, son 24. Arrivée en X = 37,0 (marche) ou 73,0 (immobile).
- Mur en colonne 6 ou plus loin : aucun rebond. L'objet s'arrête en X = 133 au sommet de l'arc, glisse, atterrit à T+22.

**Mots de coin `0x1001` sous l'arrivée** (colonnes 6 à 9, x 144 à 239). L'objet **continue de glisser** à 3 px/tick avec l'animation de vol, son 24 à chaque tick. Il s'arrête en X = 250,0 dès que les 4 coins sortent de la bande (animation 0 à t108). Aucune casse. C'est le comportement des cases qui bloquent les deux classes. Réalité d'usage : ces mots sont partout (`0x1041` 146 982 cases, `0x1001` 76 774, dans 469 cartes sur 483, `cell_census.py`). Que ce soient des fosses ou des bords d'eau profonde reste une inférence (§13).

**Eau de nage** (mot `0x0800`, pente 4 par `(mot & 0xE00) >> 9`).
- Tonneau et objets (drapeau `0x100000`) : statut 4 à **T+23** avec attache d'effet 6, libres à T+24. L'objet disparaît.
- Tronc (sans `0x100000`) : atterrit, animation 1 → 5 à T+23, reste en X = 178,0 (il flotte).
- Pente 1 (mot `0x0200`) : aucun effet.

**Touche** (Beannoïde réel de la 14, X = 165) : voir §6. **Verrou de contrôle** : voir §7.

**Posé puis relevé.**
- Le héros peut remonter sur la pierre posée : Z = 16 sous lui.
- Il peut la relever (`t155`, animation 5 puis 12).
- Un objet posé sur une plateforme qui marche (Beannoïde rendu collidable) **voyage avec elle** : X identique pendant 80 ticks, `support` = la plateforme. Cet entraînement est le mécanisme généralisé de la physique (`0x80037364`).

**Au repos** (`rest_census.py`, chaque placement natif à sa vraie position avec les vraies cases de sa carte, 6 ticks, C4 ou C70+E20 réels) : **171 inertes sur 179**. Les 8 exceptions sont des troncs (cartes 70 : recs 9, 10, 11, 12, 21 ; carte 73 : recs 11, 12, 36) qui passent à l'animation 5 au chargement parce qu'ils sont sur l'eau (`+0x18C` = 4).

## 4. Ce qui pousse un bloc (FACT, sauf mention)

- **Le héros qui marche contre l'objet** : rien. Hero bloqué en X = 157,0 (`XCollisionEntity` = l'objet), objet immobile pendant 36 ticks (S4, quatre familles). Carré donne alors le soulèvement.
- **L'épée** : INFÉRENCE de lecture du drapeau. Le tonneau n'a ni ClassA ni ClassB parmi ses drapeaux bas (`0xE4` = seulement HitsClassA), donc l'épée du héros ne le cible pas.
- **Un support qui bouge** : l'objet voyage (S10). Dans la DLL, `ScriptedMotion.cs:677` ne le fait que pour le héros (`entity.IsPlayer`), donc un objet posé ne suivrait pas une plateforme au HEAD.
- **Les sorts** (`+0xCC/+0xD0`, E20.i2) : non testés.
- **Les vrais poussables** (voir §0.2) : le pilier C76 est natif. Il compte 31 ticks de contact du héros (`+0x274`), son 0x19, animation 1, direction = `GetPushablePillarPushDirection` + 0x10 (`0x8007C024`). Il est sur les cartes 26, 30, 341, 343, 344, 346, 347 et 423 (13 placements). Une sauvegarde de l'auteur s'appelle `92-ancien-sanctuaire-bug-pillier.sav` (arrivée sur la 26). La statue (298) et le bloc à pousser (229) sont pilotés par scripts, d'après les sites des opcodes `0x20`, `0x22`, `0x23` listés par l'annexe `plan-e19-h1b2-annexe`.

## 5. Ce que lisent les plaques (FACT : `plan-e19-sm-annexe/sm1/report.md` §3, plus oracle)

- `0x60 N` : un membre du groupe N a `RidingEntity` égal à l'entité logique (la plaque). `0x61 N` : l'entité logique repose sur un membre. Aucun statut testé.
- La « Plaque à motif » (sprite 108) fait 48×32×16, `MoreFlags` `0xC0` (collidable). Elle est présente sur les cartes 27, 29, 30 et 400. La carte 113 n'a pas de sprite 108 : ses tonneaux vont sur des « Interrupteur (piétiner) » (sprite 45, recs 8 et 9).
- Oracle S5 : tonneau ou objet lancé sur la plaque. Atterrissage à **T+20** (t81) en (172,0 ; 100 ; 16,0), `support` = la plaque pendant les 40 ticks suivants, statut 2.
- Oracle S7 : un deuxième objet lancé après le premier atterrit sur lui. Z = 30,0 (16 + 14), X = 163,0, `support` = le premier (qui a la plaque pour support). C'est la mécanique de la « pile de quatre » (27 `C[36]`, `60 2D` à `60 30`). Une pile haute (sommet à 16 + 3·14 = 58) se lance depuis la plaque (Z héros 16, départ 52, sommet 66) : INFÉRENCE, non exécutée.
- Soulever un objet qui dépasse la dalle depuis le sol est possible (S5b).

## 6. La touche (O-G-9 : tranchée)

Oracle S6, Beannoïde réel de la 14 en X = 165, vol à 3 px/tick :
- Caisse C23 (référence), tonneau, pierre, tronc : touche à **T+21** (t82), PV 9 → 0 (retour « mort » 1), E2 à T+22 (statut 3, animation 2). Drapeaux : caisse `0x332374 → 0x332344`, tonneau et pierre `0x3323E4 → 0x3323C4`, tronc `0x2323E4 → 0x2323C4`. Statut 4 à T+24, libre à T+25.
- Objet étoile (AttackCount 0, sans `0x20`) : aucune touche. Il atterrit en 178,0 et reste.

## 7. Les « trois en-têtes refusés » (O-G-10)

- `0x8003A1B8` avec `a2` = 0 (naissance au chargement) refuse l'enregistrement quand la case du héros (`+0x120/+0x124`) sort de `[XMin..XMax]×[YMin..YMax]` (`0x8003A208-0x8003A25C`). Il refuse aussi quand `SpriteDirection & 0x40` est clair (naissance par script seulement, `0x8003A260-0x8003A274`).
- Les trois en-têtes sont les familles AttackCount 0 et 7 dont le premier placement exclut la case (4, 4) : herbe (sprite 41, carte 70), objets (carte 27), tronc (cartes 70 et 73). Avec la zone élargie, tout naît (S1).
- Verrou de contrôle en tick complet (S9, 40 ticks à partir de t60) : caisse, tonneau, pierre et tronc (AttackCount 1, 1, 1, 7) sont avalés à **t81** (21 ticks, conforme au mode 1 de g1a). Objets (0) et herbe (0) sont gardés en main. O-G-10 est fermée, hors le genre 2, absent du corpus (aucun placement).

## 8. Les torches (C70, E20)

- **C70** `0x8007B7B0` : 33 mots, uniquement la moitié portée (modélisée et vérifiée en g1a, 5 000 vecteurs). Table de lâcher `0x800281DC`, drapeaux `| 0x34 & ~0x80` au lâcher.
- **E20** `0x8007F6C8` : 65 mots. Premier appel : animation 2, `(drapeaux | 0x40) & ~0x30`, et si le contact `+0x130` est du type `0x187` (391, Goutte d'eau, C71), l'octet `+0x274` passe à 1 (pas de flamme). Appel suivant, quand l'animation est 2 et `+0xAC` vaut 1 : si l'octet est 0, `SpawnWarpEntity(torche, table 0, type 12, X, Y, Z, direction)` fait naître la flamme (six mots remis à 0, animation 3, drapeaux `(& ~0x10) | 2`). Puis `DestroyEntity(torche, -1)`.
- Oracle S8 (C70 et E20 réels) : contact au sol à T+22, E20 à T+23 (son 142), statut 4 à T+26 avec la flamme (type `0xC`, animation 3, drapeaux `0x132146`) à l'endroit de l'arrivée et attache d'effet 10, torche libérée à T+27 (son 41).
- Fiche d'équilibre de la torche : une attaque (6, 6, 1) et `MaxHp` 10 (`sheets-census.txt`). D34 `0x8007E8AC` (touche des « Ronces enflammées », 96 placements sur 10 cartes) lit le type d'attaque de l'attaquant : s'il vaut 6, animation 1 et drapeaux `| 0x46 & ~0x80`. L'usage (brûler les ronces) est une INFÉRENCE. D34 n'est dans aucune tranche que j'aie trouvée.
- Les gestionnaires du type 12 (C60, E9) sont dans **f6b** (`plan-e20-heros.md:6392`). La torche ne dépend donc pas d'E14. Toutes les cartes de torches (86 à 94, 441, 442) sont hors chaîne.

## 9. La DLL au HEAD (FACT, lecture)

- `Alundra/Ai/NativeSpriteRegistry.cs` : production à 7 clés (Beannoïde 3, Muruta 3, onde de l'épée 1). Pins : `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:51-55` et `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:169-190` (`3 + 3 + 1`).
- `EventProgramRunner.RunSpriteEvent` (`:264`) : E0 et E1 en ligne (`IsInlineDeactivateCode`, codes 0, 1, 10). Toute autre clé native absente est un no-op compté. Le natif ne tourne que si `(ProgramIndexes[slot] & 0x7f) == 0`, comme dans le binaire.
- `Alundra/Ai/Objects/` ne contient que `SaveBook.cs`. Aucune trace de `LiftableAi`, `CarryRules`, `PutInHands`, `ThrowCarried`, `ProductionWiredLiftableTypes`. `PlayerManager.cs` : `PlayerTryAction`, `TryUseItem` et `UpdatePlayerCarriedEntity` sont des bouchons.
- Les objets existent comme entités de carte, `Collidable` (donc dans `BuildCollidables`, `EntitySupport.cs:44-59`), mais **traversables** : la sonde de mouvement passe `PickupKindMask` (D-E19-28). Un objet posé reste donc un support que le héros peut escalader.
- Lecteur `0x60`/`0x61` : aucun `case 0x60`. Les plaques sont inertes et les 17 sites « attente de plaque » bouclent.
- `ScriptedMotion.cs:677` : le transport par un support est réservé au héros.
- Pas de champ pour `+0x278`. Pas de lecteur de mots des 4 coins d'une boîte. `+0x18C` n'est alimenté que pour les entités marquées (f6a, ⏳) ; un tronc né d'un enregistrement ne l'est pas, donc les 8 troncs flottants ne flotteraient pas sans décision (§12 Q1).
- f4, f5, f6a, f6b sont ⏳. g1a, g2a, g2b, g3, g1b ne sont pas commencées.

## 10. Découpage proposé

Tranches liées à ce qui précède : f4, f5, f6a (pente), g1a, g2a, g2b (E2 des 10 types dont 290/293/373), g3 (+ g4a), g1b. E19.sm1 est requis pour la **recette** des plaques, pas pour les tests de g2c1.

### g2c1, C4 (7 types)

- **Périmètre** : `LiftableAi.cs` (`C4Tick`), partage du lâcher avec C23 (table identique, drapeaux propres à C4), clés `(C, 4)` pour 290, 293, 363, 367, 368, 369, 373, champ de verrou `+0x278`, lecteur des mots des 4 coins, passage de `ProductionWiredLiftableTypes` de 7 à 14 types.
- **Phase A** (T1, sans changement de comportement) : fonction pure de C4 contre les 1 244 goldens, mutants.
- **Phase B** : registre, verrou, lecteur de coins. Pas totalement inerte : les 8 troncs flottants passent à l'animation 5 si `+0x18C` leur est fourni (Q1).
- **Phase C** : l'activation, qui amende le texte de g1b (« 7 types C23 »).
- **Tests de monde** (valeurs de §3 comme attendus) : lancer à plat des 7 types, rebond colonne 5 (et rien en colonne 6), eau (disparition vs flottement), touche (tonneau tue, objet ne touche pas), verrou (avalés à T+21 / gardés), plaque et pile de deux, inertie au repos (171/179 plus les 8 troncs), objets relevés.
- **Mutants** (au moins 12) : `ForceZ > 0` en `>= 0`, verrou non posé, direction `+0x10` en `+0x08`, Z de l'effet `8 px` en `16`, masque `0x1001` en `0x1000`, boucle sur 3 coins, `HitCounter` ignoré, son 0x18 en autre, pente 4 en 1, animation 5 en 4, test `+0x80 == 2`, `| 0x10 & ~0x80`.
- **Arcs** : carte 22 (tonneaux natifs recs 21 et 33 ; la pierre rec 34 est scriptée, C[35]) ; carte 70 (5 troncs flottants au chargement, 6 ticks) ; carte 27 après sm1 (objet sur plaque) ; carte 113 (tonneau sur interrupteur à piétiner).
- **Recette** (sauvegardes de l'auteur de `tools/Saves/`, JSON, arrivée lue dans `data.initialMapId`) : `66-crypte-de-lars.sav` (carte 22), `96-ancien-sanctuaire.sav` (carte 27), `91-ancien-sanctuaire-bug.sav` (carte 29). Aucune sauvegarde n'arrive sur la 70, la 113 ni la 400 : un préréglage `c4-m70` serait à écrire.

### g2c2, torches

- **Périmètre** : `(C, 70)` (moitié portée seule), `(E, 20)`, type 393 dans l'ensemble câblé, mise à jour du témoin négatif de g1b (m5 utilise la torche de la 86), D34 (optionnel).
- **Prérequis** : g1b, f6b.
- **Valeurs** : §8. Tests : vecteurs de E20 à produire (analogue de §2), flamme de type 12 à (X, Y, Z) d'arrivée, règle de la Goutte d'eau.
- **Recette** : `160-magyscar.sav` (carte 86), `162-magyscar-bug.sav` (carte 93).
- **Registre** : 7 → 9 (g1a) → … (+1 pour g2c1) → +2 pour g2c2 : à dériver par chaque tranche.

### Hors g2c (à placer)

Transport des objets posés par un support qui bouge (écart O-G-5 bis), les poussables C76 et compagnie, D34 si non retenu en g2c2.

## 11. Corrections à porter à l'esquisse §1.24.5 et aux prérequis

1. « Blocs de glace » et « rochers » ne sont pas C4 (C23). C4 = 7 en-têtes (tableau §1).
2. « Plus de 150 instructions » : 157 mots, dont 39 de moitié portée.
3. « La casse d'une pierre qui touche une cible est une inférence » : devient un FACT (§6).
4. « Poussables » : lire « transportables » ; les vrais poussables sont hors de la tranche (§4).
5. « Torches avec E14 » : non. E20 dépend de f6b (type 12), et D34 est un petit gestionnaire de toucher.
6. « Trois en-têtes refusés » : artefact de zone (§7).
7. g2c a besoin de g1b (et pas seulement g2b), car g1b fixe l'ensemble câblé à 7 types C23.
8. Le témoin négatif (m5) de g1b est la torche : g2c2 doit le remplacer.

## 12. Questions

### Pour le plan

- **Q1** : alimenter `+0x18C` pour les troncs nés d'enregistrement : ciblé (type 373 seulement), pour toutes les entités à gravité (change 444 instances qui portent `DestroyOnSlidingSlope`, E19.h2 / Q-K1), ou rien (les 8 troncs ne flottent pas) ?
- **Q2** : les 43 troncs scriptés (patrouille) sont soulevables par leur en-tête et ne rendent jamais la main à C4. Lire leurs scripts (ou exécuter le vrai bytecode) pour établir ce que fait l'original quand on en soulève un.
- **Q3** : le lecteur des mots des 4 coins et O-G-2 (`+0x180`, `+0x184`, `DestroyOnVramFlags`) sont-ils une seule fonction ?
- **Q4** : g1b est-elle amendée (14 types câblés d'emblée) ou g2c1 étend-elle l'ensemble ?
- **Q5** : un objet posé sur un support mobile voyage dans l'original ; la DLL ne le fait que pour le héros.
- **Q6** : ordre g2c1 / sm1 pour la recette des plaques.
- **Q7** : D34 dans g2c2 ou en E14 ?
- **Q8** : `+0x80` (code natif E) est lu par C4 au lâcher forcé (`t24` = -1, blessure, mort, nage) : la DLL doit offrir cette valeur.
- **Q9** : sondes de la DLL à écrire au T0 de g2c1, après g1a, g2a, g2b, g3 : P1, `ForceAdjusted` d'un objet lancé contre un mur au tick du lancer (attendu : effet 9 à T+1 pour tonneau, pierre, tronc ; T+2 pour les objets) ; P2, mots des 4 coins sur un tonneau au bord d'une bande `0x1001` ; P3, `HitCounter` à T+21 ; P4, `RidingEntity` d'un objet posé sur une plaque de 16 px (attendu : la plaque).

### Pour l'auteur, en termes de jeu

- **A1** : les piliers qu'on pousse en marchant contre eux (carte 26, 24 piliers sur 8 cartes), les statues poussables (62, 11 cartes) et le bloc à pousser/attaquer (9, 4 cartes) sont des mécaniques différentes de celle des objets de plaque. Sans tranche, ils restent immobiles. Les voulez-vous dans ce chantier ?
- **A2** : les 57 torches (Magyscar, 441, 442) servent à lancer du feu (brûler des ronces, sans doute). Voulez-vous les porter avec les tonneaux, ou après f6b ?
- **A3** : un tonneau, une pierre ou un objet de plaque lancé dans l'eau de nage disparaît avec une éclaboussure, comme l'original ; le tronc flotte. Les puzzles de plaques supposent donc que l'objet soit recréé par script (un placement par type en naissance par script seule). Aucune décision nécessaire, à confirmer en recette.
- **A4** : soulever un tronc qui patrouille sur l'eau (cartes 72, 73, 79, 140, 158) : dans l'original le tronc scripté pourrait rester collé aux mains. À mesurer avant de poser la question.
- **A5** : un objet posé sur un radeau ou une plateforme qui bouge reste sur place dans le port actuel, alors qu'il voyage dans l'original. À reporter ou à porter ?

## 13. Non établi (INFÉRENCE ou INCONNU)

- Ce que sont en jeu les mots `0x1001` / `0x1041` : cases qui bloquent les deux classes ; fosses, bords d'eau profonde ou murs invisibles non distingués.
- Le comportement des 43 troncs scriptés et d'une pierre ou d'un tonneau scripté à la fin de leur programme C (QUELQUES scripts lus : `C[46]` de la 27, `C[35]` de la 22, `C[12]`, `C[17]`, `C[18]` de la 72).
- Les phases de 24 appels des plaques (27 `C[2]`, 400) : lues dans sm1, non rejouées. Je n'ai pas exécuté le bytecode des plaques.
- Une pile de quatre complète, une épée contre l'objet, les sorts qui poussent, le genre 2.
- L'usage des torches (brûler les ronces) est déduit de D34 et de la fiche (6, 6, 1).
- Aucune sonde de la DLL n'a été lancée (pas de build dans un dépôt où un autre exécuteur travaille).

## 14. Reproductibilité

Depuis le dossier de travail : `python -B -X utf8 c4_full.py 20000 mut gold` (vecteurs, mutants, goldens ; pas de `-I`, capstone est dans le site-packages utilisateur) ; `python -B -I -X utf8 c4_scen.py s1_flat s2_wall s3_landing_words s3b_water s4_hero_and_stone s5_plate s5b_lift_from_plate s6_touch s7_stack s8_torch s9_lock s10_ride` ; `python -B -I -X utf8 rest_census.py` ; `refused_headers.py`, `census_c4.py`, `census_scripted.py`, `cell_census.py`. Les sorties sont dans `out/` (28 fichiers hachés dans `MANIFEST_out.sha256`).