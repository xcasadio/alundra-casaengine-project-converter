# E20.g2c2 – les torches : lecture seule préparatoire

Rien n'est décidé. Tout ce qui suit est un fait ([FAIT], [MESURÉ]) ou une inférence ([INFÉRENCE]), marqué comme tel.

**Méthode.** Je n'ai rien écrit dans le dépôt, lancé ni construit de test. Le code de la DLL a été lu par `git show HEAD:` et `git grep HEAD`.

Pendant ma lecture, le HEAD est passé de `8174c0cb` à `5e67722d`. Les trois commits de l'executor (E20.b2) ne touchent que `Alundra.Tests/`. `git diff` est vide sur `Alundra/Ai`, `Alundra/Hero/PlayerManager.cs`, `Alundra/Entities` et `Alundra/Combat`.

Le binaire est `ALUN_CD.EXE` (France), lu avec `bx.py` et capstone. Les rejeux ont tourné sur une copie de l'oracle de `docs/plan-e20-annexe/e20-g1a/work/` et `e20-g2c/disc/`.

---

## 0. En cinq lignes

1. La torche est le type 393, lisible par le héros seul. On la lance, elle fait naître une flamme de type 12 qui vit 39 ticks, puis disparaît. Elle brûle trois choses : les ronces (D34), les interrupteurs d'allumage de la carte 92 (16) et les ennemis. Elle brûle aussi Alundra si elle reste dans la flamme.
2. **La flamme n'utilise pas les gestionnaires de la boule de feu.** E20 efface les six codes natifs de la flamme juste après sa naissance. Sans cela, elle émettrait des étincelles toutes les 4 ticks et exploserait (rejeu, F9). La prémisse de D-E20-64, « les gestionnaires du type 12 sont ceux de f6b », est donc fausse pour la torche. L'ordre « après f6b » reste un choix de l'auteur, mais ce n'est pas une dépendance de code.
3. Au HEAD, la DLL n'a ni `(C,70)` ni `(E,20)`. Elle n'a pas non plus `(D,34)` (les ronces) ni `(A,19)` et `(C,71)` (les gouttes d'eau). C71 vise explicitement la torche (type 0x189) : une goutte qui tombe sur une torche, ou sur le héros qui la porte, l'éteint. Ces trois familles de dangers n'ont aucun propriétaire dans les plans (E14.b ne liste que des ennemis et des boss).
4. Les 3 sous-éléments `ProductionWiredLiftableTypes` (+393), `ReleaseFlagsPolicy` (+C70, drapeaux `|0x34`) et `SpawnEntityBySpriteIndex` existent déjà. La tranche est donc petite pour la torche seule. Elle grossit si l'auteur retient D34 et les gouttes.
5. 7 tests existants bougeraient, par analogie avec g2c1. Aucun n'est un test de comportement.

---

## 1. Faits numérotés

### A. Les données : qui porte une torche

**F1 [MESURÉ].** La torche est le sprite de carte 137, soit le type 393 (`0x100 + 137`).
- En-tête : C70 en slot C et E20 en slot E.
- `MoreFlags` 0xE2 et `CanPickup` 0x23, donc genre 1 (se soulève sans gant).
- Drapeaux `0x3323E2`, corps 21×15×15, `BreakEffect` 10, `Contents` 0.
- Fiche d'équilibre : `MaxHp` 10, réponses 16 à tout, une seule attaque (attribut 6, puissance 1).
- Aucun autre type ne porte C70 ou E20.
- Sources : `alundra-project/Data/sprite-records.json` (BankKey `137`), `Data/balance.json` entrée 393, roster E14. Sortie : `out/census_g2c2.txt`.

**F2 [MESURÉ].** 57 placements actifs sur 10 cartes.

| Carte | 86 | 87 | 88 | 89 | 90 | 92 | 93 | 94 | 441 | 442 |
|---|---|---|---|---|---|---|---|---|---|---|
| Torches | 6 | 8 | 3 | 4 | 4 | 16 | 4 | 8 | 3 | 1 |

Les cartes 86 à 94 sont Magyscar. Les cartes 441 et 442 sont « Meia Dream ». Aucune n'est sur la chaîne d'histoire de 30 cartes. Le recensement E14 trouve aussi 57.

**F3 [MESURÉ].** 48 torches naissent au chargement (direction `0xC0`). 9 naissent par script (direction `0x80`).
- 87 recs 54-56, par C[63].
- 90 rec 4, par C[4].
- 94 recs 16-19, par B[1] au chargement de la carte.
- 92 rec 68, recréé en 4 points par C[15] avec l'opcode `0x8A`.

8 de ces 9 portent un programme C scripté. Il est identique octet pour octet à C[46] de la 27, qui fait tomber un objet C4 : `0x70`, `0x03`, `0xBD 55`, `0x25`, `0xBD 24`, `0x40 [2]`. C'est un objet qui tombe du ciel, joue les sons 55 et 24, puis passe au natif. Les opcodes existent tous dans `EventProgramRunner.cs` (g2c1 les a exercés).

**F4 [FAIT].** Seul le héros soulève.
- Les trois écritures de `+0x28` (le porteur) rangent toutes le héros `0x80127D30` (plan §1.24, fait [binaire, critique]).
- Les 102 appels à `SpawnWarpEntity` (`0x80039F58`) ne désignent jamais la page de carte, index 137. Aucun ennemi ne porte ni ne crée de torche (`out/spawnwarp_callsites.txt`).

**F5 [MESURÉ].** Les points d'entrée de recette.
- `tools/Saves/160-magyscar.sav` arrive en carte 86 : 6 torches, 1+8 ronces, 0 goutte.
- `162-magyscar-bug.sav` arrive en carte 93 : 4 torches, 7+12 ronces, 0 goutte.
- `161-magyscar-bug.sav` arrive en carte 91, qui n'a pas de torche.
- La carte 92 (16 torches, 13 ronces, 9 gouttes, 16 interrupteurs d'allumage) n'a aucun point d'entrée par sauvegarde ni préréglage.

### B. Le binaire

**F6 [FAIT]. C70 (`0x8007B7B0`, 33 mots) : seulement la moitié portée.**
- Non porté (`+0x28 == 0`) : retour immédiat. Une torche posée ne fait rien.
- Porté sans code de lancer (`+0x24 == 0`) : animation 0.
- Porté avec un code : animation lue dans la table `0x800281DC`. Les 4 premiers mots (0, 3, 1, 4) sont identiques à `0x80027400` (C23) et `0x800270C0` (C4) [MESURÉ]. Le code −1 donne l'animation 3.
- Puis `+0x24 = 0`, `porteur.+0x2C = 0`, `+0x28 = 0`.
- Drapeaux écrits : `(f | 0x34) & ~0x80`. C23 écrit `| 0x30`. C4 n'écrit rien pour les codes 1 à 3.
- La torche lancée prend donc `HitsClassA` (0x04) en plus de son `HitsClassB` (0x02), perd `Collidable`, garde les bits d'arrêt 0x10, 0x20 et 0x40. Soit `0x3323E2 → 0x332376`.

**F7 [FAIT]. E20 (`0x8007F6C8`, 65 mots), lu et rejoué.** E20 tourne à chaque tick tant que le statut est 3.

```
si (+0x88 == 2 et +0xAC == 1) :              // l'animation 2 demandée est finie
    si octet[+0x274] == 0 :
        f = SpawnWarpEntity(self, page 0, type 12, +0x114 X, +0x118 Y, +0x11C Z, +0x8C direction)
        f.codes[+0x70..+0x84] = 0            // six mots effacés
        f.+0x88 = 3 ; f.+0x6C = (f.+0x6C & ~0x10) | 2
    DestroyEntity(self, -1)
sinon :
    +0x88 = 2 ; +0x6C = (+0x6C | 0x40) & ~0x30
    c = +0x130 ; si c != 0 et c.+0x68 == 0x187 : octet[+0x274] = 1   // type 0x187 = 391 = Goutte d'eau
```

Le son 142 vient de l'animation 2 (`Sfx` de la fiche d'animation), pas d'E20.

**F8 [MESURÉ, rejeu `s8_torch`].** Identique à `out/s8_torch.txt` du T0 de g2c. Pour T = 61, le tick du lancer :

| Événement | Tick |
|---|---|
| Atterrissage | T+22 (t83) |
| Premier appel d'E20 : statut 3, animation 2, drapeaux `0x332346`, son 142 | T+23 (t84) |
| Flamme née et torche détruite (statut 4), effet d'attache 10 | T+26 (t87) |
| Torche libérée, son 41 | T+27 (t88) |

La flamme naît en (178,0 ; 100 ; 0,0) pour un lancer en marche, en 143,5 pour un lancer à l'arrêt.

**F9 [MESURÉ, rejeu `out/replay_f1_flame.txt`].** J'ai ajouté E0, C60 et E9 réels à la machine pour voir ce que fait la flamme.
- **Variante normale.** La flamme est présente de t87 à t125 (39 ticks). Ses codes valent `[0,0,0,0,0,0]`. Aucun effet 17 n'est émis. Elle meurt par E0 (`DestroyEntity`) quand l'animation finit.
- **Variante où je neutralise la boucle d'effacement** (un mot de code à zéro dans la mémoire de la machine). Les codes restent `[0,0,60,0,9,0]`. C60 émet l'effet 17 à t91, 95, …, 123. E9 explose à t125. La vie passe à 76 ticks.

L'effacement est tout le sens de la torche : elle transforme le prototype de la boule chargée en simple nappe de feu.

**F10 [FAIT]. La flamme.**
- Le type est le global 12, « Boule de feu chargée » (prefab `alundra-project/Entities/Boule de feu chargée/`, animation 3 « Once », 0,72 s).
- Drapeaux finaux `0x132146` : `HitsClassA` et `HitsClassB`, `DeactivateOnAnimEnd`, gravité, `DestroyOnSlidingSlope` (donc détruite en eau profonde, effet 6), pas de `DeactivateOnImpact`.
- Attaque : `BalanceRecord.AttackForAnimation(3)` donne `Attacks[4]` = (attribut 6, puissance 1), pas les (134, 12) des boules. Cela vient de la table (`Combat/BalanceRecord.cs:61`, `Data/balance.json` entrée 12).
- Boîte d'animation 3 : 21×15×15 (prefab).
- Elle touche le héros (classe B, réponses 0) et tout ce qui est ClassA.

**F11 [FAIT]. D34 (`0x8007E8AC`, 17 mots), pour les types globaux 238 et 239 « Ronces enflammées ».**

```
a = (self.+0x224).+0x1C8 ; si (a[0] & 0xF) == 6 :        // l'attaquant a un attribut de type 6 (feu)
    +0x88 = 1 ; +0x6C = (+0x6C | 0x46) & ~0x80           // animation 1, HitsClassA/B, plus Collidable
```

- Les fiches de 238 et 239 ne sont touchables (réponse 16) que par le nibble 6, immunes (128) aux autres.
- Leurs drapeaux d'en-tête sont `0xC3` : ClassA, HitsClassB, `DeactivateOnAnimEnd`, `Collidable`.
- Après D34, une ronce brûle (animation 1 « Hold », son 39) et ne bloque plus. À la fin de l'animation, E0 la détruit (effet de bris 8, `Contents` 255).
- Nibble 6 = feu : la boule de feu est 134 = `0x86`, la torche et la flamme ont l'attribut 6.
- Sa dernière animation attaque en nibble 6 avec HitsClassA : brûler se propage probablement aux ronces voisines [INFÉRENCE, non rejouée].
- Les ronces globales n'ont pas d'enregistrement dans les JSON de carte. L'oracle ne peut pas les faire naître sans les données globales.
- Placements actifs : 63 (type 238) et 98 (type 239) sur 24 cartes, dont les 10 des torches. Le roster E14 donne 61 et 96. L'écart de 4 vient probablement de cartes de test écartées par lui [INFÉRENCE].

**F12 [FAIT]. A19 et C71 (la « Goutte d'eau », type 391, sprite de carte 135).**
- A19 (`0x800619D0`, 3 mots) copie l'octet `+0x54` dans `+0x275`.
- C71 (`0x8007B834`, 89 mots) est une machine à 5 états sur `+0x274` :
  - 0 : arme un compteur de 40 ticks.
  - 1 : décompte, puis chute (animation 1, gravité).
  - 2 : au sol ou contact, animation 2 (éclaboussure, son 145), `Collidable` retiré.
  - 3 : **si le support `+0x12C` est le héros, lit son objet porté `+0x2C` ; si c'est une entité de type 0x189 (la torche), force son statut à 3 et son octet `+0x274` à 1.** E20 fera alors la torche sans flamme.
  - 4 : à la fin de l'animation, recommence et remonte de 160 px.
- 56 gouttes sur 6 cartes (87, 88, 90, 92, 94 et 158). Les 61 programmes scriptés attendent (`0x37`) puis passent au natif par `40 02 00`.
- Corps de la goutte : 4×3×8. Au HEAD elles restent immobiles à l'état 0.
- [MESURÉ, rejeu `out/replay_f2b_drop_sweep.txt`] Une torche lancée à plat n'a jamais une goutte posée au sol dans son `+0x130`. La règle de contact latéral d'E20 est donc un cas rare. Le cas qui compte est celui de C71.

**F13 [FAIT, `out/scan_0x87.txt`].** Les seuls programmes de carte qui testent le feu (`0x87 [128, 6]`) sur une carte à torches sont ceux de la 92.
- 16 usages, sur 16 « Interrupteur (allumage) » (sprite de carte 51, `MoreFlags` 0x81, 24×16×32).
- Chaque interrupteur est entouré de 4 torches (recs 21-24 / 33-36, 43-46 / 39-42, 56-59 / 52-55, 0-2).
- Au total 99 interrupteurs d'allumage existent sur 14 cartes. Seule la 92 a des torches.

**F14 [FAIT].** Les tables natives sont à `0x800C4B34` (A), `0x800C4F34` (C), `0x800C5334` (D) et `0x800C5734` (E), au pas de `0x400`. Les adresses de C70, E20 et D34 sont rattachées à ces tables (`out/handler_tables.txt`).

### C. La DLL au HEAD

**F15 [FAIT].** `NativeSpriteRegistry.CreateForProduction` (`Alundra/Ai/NativeSpriteRegistry.cs:36`) appelle 11 enregistrements : Beannoïde, Muruta, onde de l'épée, objets ramassables, coffre, flèche chargée, flèches, pilier, C23, E2 et C4. Il manque :
- `(C,70)` et `(E,20)` pour {393} ;
- `(D,34)` pour {238, 239} ;
- `(A,19)` et `(C,71)` pour {391}.

**F16 [FAIT].** `PlayerManager.ProductionWiredLiftableTypes` (`Alundra/Hero/PlayerManager.cs:972`) compte 25 types. Il est écrit comme `LiftableAi.SpriteTypes` plus `C4ProductionTypes`. 393 n'y est pas. Règle de fermeture : un type n'est jamais câblé sans sa clé.

**F17 [FAIT]. Briques réutilisables.**
- `LiftableAi.TickCarried(entity, ReleaseFlagsPolicy)` avec l'enum `{C23, C4}` (`Alundra/Ai/Objects/LiftableAi.cs:118,182`). Une variante C70 `|0x34 & ~0x80` s'y ajoute.
- `LiftableAi.Break` (E2, `:71`) est le modèle exact d'E20 : lit `TargetAnimationId` (animation demandée), `ForceResetAnimationFlag`, `Flags`, puis `ScriptHost.DestroyEntity(e, -1)`.
- `IEntityWorldContext.SpawnEntityBySpriteIndex(parent, isMapPage, index, x, y, z, direction)` (`Alundra/Maps/IEntityWorldContext.cs:81`). `ChestAi.cs:78` est le seul exemple d'un gestionnaire natif qui fait naître un objet. Il renvoie null si la table de cases est pleine, et l'appelant doit le tester.
- `SpriteProgramIndexes` est le tableau des six codes (`+0x70`). `RunSpriteEvent` le lit en direct (`EventProgramRunner.cs:272`), donc l'effacement d'E20 prend effet. Le code E 0 est traité en ligne par `IsInlineDeactivateCode` (`:336`).
- `EntityScriptProxy.Bytes[0]` est `+0x274`. `XCollisionEntity` est `+0x130`, écrit par `MoveControllerAndPullPosition` (`EntityScriptProxy.cs:369,2660`). `TouchingEntity` est `+0x224`. `CurrentAttack` est `+0x1C8`.
- `ThrowCarried` écrit la direction par `SpawnDirections`, donc toujours cardinale (0, 8, 0x10, 0x18). Les diagonales ne se posent pas.
- La passe de coups (`Combat/HitPass.cs`), `HitOpcodes.IsTouchedByAttackType` (le nibble 6 pour `0x87 [x, 6]`) et l'effet d'attache 4 pour les nibbles 6 et 10 (`EffectOfNibble`, f3b) sont tous là.
- g2b2 compte déjà la torche parmi les 16 types de carte à gravité plus `DestroyOnVramFlags` (`e20-g2b2/results.md` §c) : une fois portée, elle est détruite au-dessus d'un trou, sans flamme.

**F18 [FAIT].** La blessure du héros est absente (c2b, à faire avant, D-E20-83). Tant que c2b n'est pas faite, la flamme et la plaque de la torche posée touchent le héros par étincelles seulement, sans perte de PV.

### D. Dépendances

**F19.** Théme 1 clos (HEAD `7fbbf2dd`) : g1a, g1b, g2a, g2b, g2b2, g2c1, g3, g4a, g4b et g5 sont faites et CONFIRMED. e0a (naissance par indice), f3b (effets 4 et 5), f6a (pente des entités marquées) et le tir de f4 et f5 sont faits. Il n'y a pas de dépendance de code de g2c2 envers f6b.

**F20.** f6b (C60, E9, ensemble {11, 12, 14, 15}) n'est pas commencée au HEAD (pas de `BallAi.cs`). La seule raison technique de passer après f6b est de tester que la flamme reste sans effet quand `(C,60)` est enregistrée pour le type 12. Avant f6b, ce test est vide.

---

## 2. Corrections à apporter à l'esquisse §1.24.5 et à D-E20-64

1. **D-E20-64** (« la Torche fait naître le type 12 dont les gestionnaires viennent de f6b ») : faux pour la flamme de la torche (F7, F9). Le choix d'ordre de l'auteur reste valable, mais il n'est pas imposé par le code.
2. **O-F6-3** (« La Torche E20 … copie le type 12 et efface ses six codes natifs ») : propriétaire inscrit « E14 ». Il devient g2c2 (D-E20-64 le dit déjà).
3. **Q-G2C-7** (D34 dans g2c2 ou en E14) : E14.b ne contient que des ennemis et des boss. Les dangers D34, E14 (rocher tombant), C66, C67, E18 (boules de fer), C70/E20 et A19/C71 n'ont aucun propriétaire [FAIT, roster E14 §4 et plan E14 §1.4].
4. **Gouttes d'eau** : non mentionnées par l'esquisse. C71 est le jumeau d'E20 (F12).
5. Le témoin négatif (m5) de g1b et g2c1 doit encore changer de type (voir §4).
6. L'esquisse dit « 8 scripté ». Il y en a 8 scriptés et 1 de plus né par script (92 rec 68).

---

## 3. Ce que la tranche doit faire (périmètre probable)

**Noyau, indépendant des réponses de l'auteur.**
- Nouveau fichier `Alundra/Ai/Objects/TorchAi.cs`. Il suit le modèle de `PillarAi` et `ChestAi` : `SpriteTypes {393}`, `Keys`, `Register`, `Tick` (C70 par `TickCarried` avec une politique C70), `Burn` (E20), des coutures de test pour le spawn et le contact.
- `LiftableAi.cs` : une valeur `ReleaseFlagsPolicy.C70` (`|0x34 & ~0x80`).
- `NativeSpriteRegistry.cs` : l'appel `TorchAi.Register`, plus la doc.
- `PlayerManager.cs:972` : `ProductionWiredLiftableTypes` gagne les types de `TorchAi`.
- `Alundra/SaveGames/TestSaves.cs` et `docs/test-saves.md` : un préréglage si l'auteur le veut (voir Q5).
- Tests, annexe `docs/plan-e20-annexe/e20-g2c2/`, ADR (numéro libre recompté, le dernier connu est 0094), plan.

**Selon les réponses de l'auteur.**
- D34 : `Alundra/Ai/Objects/BrambleAi.cs`, clé `(D,34)` pour {238, 239}.
- Gouttes : `WaterDropAi.cs`, clés `(A,19)` et `(C,71)` pour {391}.

**Découpage proposé, par analogie avec g2c1.**
- **T0** : oracle d'E20 et de D34 (vecteurs rejoués sur la machine, au moins 25 mutants au total) ; rejeu de F8 et F9 ; sondes DLL (voir §7) ; liste mesurée des tests déplacés.
- **Phase A** (aucun changement de comportement) : `C70`, `Burn`, D34 contre les vecteurs.
- **Phase B** : l'enregistrement des clés. Aucune torche n'est encore soulevable, donc rien ne change en jeu, sauf les ronces touchées par du feu (aucune source avant l'activation).
- **Phase C** : l'ensemble câblé gagne 393.
- **Phase D** (si retenue) : les gouttes.

Budget comparable à g2c1 : un `executor` par phase, deux tentatives chacun, `plan-verifier` avant, deux verifiers frais après.

**Tests nouveaux à écrire, rouges d'abord.**
- C70 : `|0x34` et non `|0x30`, table de lâcher partagée.
- E20 : les cas du pseudo-code, vecteurs de l'oracle.
- Monde : la chronologie T+22, T+23, T+26, T+27 ; la flamme en (X, Y, Z) d'arrivée, direction cardinale ; sa vie de 39 ticks.
- La flamme n'émet rien même si `(C,60)` est enregistrée pour le type 12.
- Pas de flamme en eau profonde ni au-dessus d'un trou.
- Les 16 interrupteurs de la 92 par la vraie passe de coups.
- Au repos : les 49 torches natives restent inertes 6 ticks sur leurs vraies cases.
- Les 8 torches qui tombent du ciel.
- Les ronces : le feu brûle, l'épée ne fait rien.
- Si gouttes : une goutte éteint la torche portée.

**Mutants à prévoir.** `|0x34` en `|0x30` ; l'effacement des six codes retiré ou à 5 ; l'animation 3 de la flamme en 1 ; `& ~0x10` oublié ; `| 2` oublié ; le test `+0xAC == 1` ; l'octet `+0x274` ignoré ; le type de contact 0x187 changé ; D34 : nibble 6 en 5, `|0x46` en `|0x42`, `~0x80` oublié ; un type câblé sans ses clés.

---

## 4. Tests existants qui bougeraient

Prédiction par analogie avec g2c1, qui en a déplacé 7. À mesurer au T0 par un câblage temporaire annulé.

| Fichier | Raison |
|---|---|
| `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58` | Somme des clés du registre de production. |
| `Alundra.Tests/Ai/Objects/LiftableAiRegistrationTests.cs:77` (R1) | Même somme. |
| `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs:239` (C2) | Même somme. |
| `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:185` (V3) | Même somme. |
| `Alundra.Tests/Hero/LiftPureTests.cs:140` | Liste de 25 types écrite à la main, devient 26. |
| `Alundra.Tests/Hero/LiftWorldTests.cs:41` et `:290` | `UnwiredType = 393`. |
| `Alundra.Tests/Arcs/LiftMap72ArcTests.cs:39` et `:237` | `TorchType = 393`. |

Pour les deux derniers, le témoin doit passer à un autre type genre 1 non câblé : la bombe (global 2) ou les graines (global 1).

Si une goutte ou une ronce s'ajoute, les clés de leurs classes entrent dans les mêmes sommes.

Probablement aussi : le test de liste des préréglages (`TestSavesTests.cs`) si un préréglage est ajouté, et le commentaire de `C4RegistrationTests.cs:31-37` (l'assertion « (C,4) absent pour 393 » reste vraie).

À surveiller sans y toucher : `Ai/Objects/C4WorldTests.cs:631-633` (cartes 86, 87, 93, tonneaux au repos sur des cartes qui ont aussi des torches et, en 87, des gouttes).

---

## 5. Risques

1. **La tranche ne montre rien sans D34.** Sans feu sur les ronces, la torche n'allume que les 16 interrupteurs de la 92. Les ronces sont collisionnables (0xC3) ; je n'ai pas vérifié si elles ferment un passage obligatoire de Magyscar.
2. **Hero blessé par sa propre torche** [INCONNU]. La torche à `HitsClassB` a une plaque d'attaque (animation 0, z+24, 18×12×1) et une boîte plus large en vol. Dans l'original le héros qui la porte n'est pas blessé. Si la géométrie de la DLL place la torche portée différemment (offset, `CarryRules.HeadHeight` = 32 px), la torche pourrait s'éteindre d'elle-même (`DeactivateOnHit`, HitCounter). Sonde P-T-1.
3. **E20 s'exécute à chaque tick**, pas seulement au premier. La règle de contact (`+0x130` de type 0x187) doit être évaluée à chaque tick de l'état Deactivated. La timeline T+26 dépend de `ForceResetAnimationFlag` (fin de cadre, D-E19-13), comme E2.
4. **Table pleine (63 cases).** `SpawnEntityBySpriteIndex` renvoie null. Le binaire écrit à travers un pointeur nul. Il faut garder la destruction de la torche et documenter l'écart.
5. **Rappel de la boule de feu.** Si f6b se fait ensuite, il faut un test qui prouve que le type 12 né de la torche garde ses codes à zéro. Sinon la flamme se met à émettre.
6. **Pas d'oracle pour les ronces globales** (F11). Les données globales ne sont pas dans les JSON de carte. D34, la propagation entre ronces et le butin de `Contents` 255 se testent donc dans la DLL, non contre la machine. Un montage à fournir (données du sprite 238 ou 239).
7. **Dégâts d'une flamme.** Avec c2b faite, la flamme blesse et fait reculer Alundra. Cela exige que les recettes aient un point de départ où l'on ne se tient pas dans la flamme.
8. **Les cartes de Magyscar restent incomplètes** après g2c2 : insectes (C7, D6), boules de fer (E18, C66, C67), fourneaux (C28, D5) sont à E14. La recette est partielle.
9. **Les gouttes déjà gelées au HEAD.** Elles restent suspendues à l'état 0 dans 6 cartes, indépendamment de g2c2.

---

## 6. Questions à l'auteur, en termes de jeu

Défaut = « comme l'original ».

**Q1 – Les ronces prennent-elles feu ?**
Dans l'original, une torche lancée, ses flammes, et plus tard la baguette de feu ou un ennemi de feu font flamber les ronces. Elles s'embrasent avec le son de l'explosion, cessent de bloquer, puis disparaissent. Elles peuvent aussi gagner leurs voisines.
- Aujourd'hui, 161 ronces sur 24 cartes sont des obstacles indestructibles.
- **A (défaut)** : D34 dans g2c2. La torche a un but visible.
- **B** : les ronces restent inertes jusqu'à une tranche des dangers. Les torches se lancent, produisent une flamme et allument les 16 interrupteurs de la 92, mais ne brûlent rien d'autre.

**Q2 – Les gouttes d'eau tombent-elles ?**
Dans l'original, 56 gouttes (cartes 87, 88, 90, 92, 94, 158) tombent du plafond en cycle (attente de 40 ticks, chute, éclaboussure). Une goutte qui tombe sur une torche, ou sur Alundra qui la porte, l'éteint : la torche disparaît sans flamme.
- Aujourd'hui, elles pendent immobiles.
- **A (défaut)** : porter les gouttes dans g2c2 (phase D) ou dans une tranche g2c3.
- **B** : les laisser immobiles. Les torches ne s'éteignent jamais, un écart documenté qui facilite les énigmes de Magyscar.

**Q3 – Faut-il garder l'ordre « après f6b » ?**
La flamme de la torche n'utilise pas les boules de feu. Rien de visible ne change si les torches viennent avant.
- **A (défaut)** : garder D-E20-64, juste après f6b. Cela rend le test « la flamme n'émet rien » significatif.
- **B** : avant f6b (c2b suffit pour les dégâts).

**Q4 – Quel point de départ pour essayer ?**
Les sauvegardes 160 (carte 86) et 162 (carte 93) n'ont ni interrupteur d'allumage ni goutte.
- **A (défaut)** : 160 plus un préréglage `torche-m92` (16 interrupteurs, 9 gouttes, 16 torches).
- **B** : seulement 160 et 162.

**À confirmer à la recette, sans question :**
- La flamme et la plaque de la torche posée brûlent aussi Alundra (comme l'original).
- Les ronces brûlées lâchent peut-être un butin aléatoire (`Contents` 255) via le chemin existant de `DestroyEntity` (e0c), à vérifier au T0.

---

## 7. Sondes T0 à prévoir

- **P-T-1.** La torche portée pendant N ticks reste au statut 2 : le héros n'est pas blessé et la torche ne s'éteint pas d'elle-même.
- **P-T-2.** Le lancer plat donne les ticks T+22, T+23, T+26, T+27 et la flamme en (X, Y, Z) attendus, dans la DLL.
- **P-T-3.** La flamme vit 39 ticks sans effet, même avec `(C,60)` enregistrée.
- **P-T-4.** Un tir de la flamme sur l'interrupteur de la 92 (programme D de la carte) pose le drapeau.
- **P-T-5.** D34 : une ronce touchée par la flamme passe à l'animation 1, perd `Collidable`, est détruite à la fin ; l'épée ne fait rien.
- **P-T-6.** Le butin d'une ronce brûlée.
- Gouttes : l'état 3 de C71 sur le héros portant une torche (aucun rejeu n'a encore exercé ce cas).

---

## 8. Scripts et sorties

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/g2c2/`

**Scripts**
- `bx.py` (copie de `docs/plan-e20-annexe/e20-theme2/bx.py`)
- `census_g2c2.py`
- `scan87.py`
- `spawnsites.py`
- `tables.py`
- `sprite_anims.py`
- `f1_flame.py`
- `f2_drop.py`
- `f2b_drop_sweep.py`
- `dump_map.py`, `dump_prog.py`, `census_c4.py`, `census_scripted.py` (copies des outils du T0 de g2c)
- `replay/` : copie de l'oracle, avec `c4_scen.py`, `c4_full.py` et les trois scripts de rejeu

**Sorties dans `out/`**
- `census_g2c2.txt`
- `dis_c70.txt`, `dis_e20.txt`, `dis_d34.txt`, `dis_a19.txt`, `dis_c71.txt`
- `handler_tables.txt`
- `replay_s8_torch.txt`, `replay_f1_flame.txt`, `replay_f2_drop.txt`, `replay_f2b_drop_sweep.txt`
- `scan_0x87.txt`
- `spawnwarp_callsites.txt`
- `SHA1SUMS.txt`
- `LiftableAi.HEAD.cs` (copie de `LiftableAi.cs` au HEAD, à la racine du dossier)

**Limite des rejeux.** `f2_drop.py` et `f2b_drop_sweep.py` ne reproduisent pas le contact torche-goutte (F12). Ils montrent seulement que la règle latérale d'E20 est un cas rare. Ils ne prouvent rien sur C71.