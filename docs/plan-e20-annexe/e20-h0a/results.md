# E20.h0a T0 — résultats (annexe, aucun code)

Exécuté au HEAD `19bd20e1` (branche `chantier/theme2-attaques-objets`). Plan : `docs/plan-e20-heros.md` §1.30.1 (révision de clôture) et §1.30 (faits 1 à 13,
D-H-1 à D-H-6 ; D-H-7 caduque). Preuves : `probes/` (scripts, sorties, câblage temporaire). Les numéros de ligne sont ceux du HEAD
(`Alundra/Hero/PlayerManager.cs`) ; ils se relisent par symbole.

## 0. Verdict

Aucun arrêt du plan n'est atteint : les 63 états ont une ligne d'appel ou une tranche (a) ; les traces de l'oracle rejouées sont identiques octet pour octet (b) ;
le câblage temporaire ne déplace aucun test hors de la liste prédite (d). Six écarts de texte du plan à connaître avant T1 : section 6.

## 1. (a) Les 63 états du Rond rapportés à la DLL

Source : la table `0x80022D3C` lue dans `ALUN_CD.EXE` (France) par `probes/table.py` (qui importe `exelib.py` d'`e20-h4/work`). Les 17 états sans réaction
(`03 0D 1A 1B 1E 27 36 37 38 3D 43 45 47 48 4A 4C 4D`) vont à `0x80031FF4` (le 03), `0x800325E8` (le 36) ou `0x80032758` (les 15 autres) : ce sont exactement
les 15 états `Skip` (`SwimTable`, `PlayerManager.cs:169-190` ; `table.out` : un seul gestionnaire `0x80032758`) plus `0x03` et `0x36`. 63 + 17 = 80.

Les 17 sites d'appel de `TryUseItem` au HEAD (le plan en comptait 13) : `:464` (Idle, Moving), `:483` (sauts et air : `02 2B 2C 2D`), `:502` (échelle `0E 35`),
`:610` (sortie de nage vers la terre `0F 1D 28 3A 3B`), `:635` (blessure `31`, c2b), `:649` (groupe d'attaque au sol), `:659` (groupe d'attaque en l'air),
`:751`, `:757`, `:761` (les trois cas de `SwimTick`), `:782` (`SwimSteerTick`), `:808` (`SwimHurtEndTick`), `:838` (`PrepareSprintTick`), `:943` (`StopSprintTick`),
`:997` (`BounceTick`), `:1498` (`LiftGestureTick`, g1a), `:1518` (`CarryGroupTick`, g1a).

| state | binary handler (0x80022D3C) | site / slice |
|---|---|---|
| 0x00 | 0x80031E38 | DLL `TryUseItem` in the Idle/Moving case (PlayerManager.cs:464) |
| 0x01 | 0x80031E38 | DLL `TryUseItem` in the Idle/Moving case (PlayerManager.cs:464) |
| 0x02 | 0x80031E84 | DLL jump states case (:483) |
| 0x04 | 0x80031E18 | DLL ground attack group (:649) |
| 0x05 | 0x800322F0 | DLL LiftGestureTick (:1498, g1a/g1b) |
| 0x06 | 0x8003232C | DLL CarryGroupTick (:1518, g1a) |
| 0x07 | 0x8003232C | DLL CarryGroupTick (:1518, g1a) |
| 0x08 | 0x80031E18 | DLL ground attack group (:649) |
| 0x09 | 0x80031E18 | DLL ground attack group (:649) |
| 0x0A | 0x80031DF8 | DLL air attack group (:659) |
| 0x0B | 0x800322B4 | DLL LiftGestureTick (:1498, g1a/g1b) |
| 0x0C | 0x8003232C | DLL CarryGroupTick (:1518, g1a) |
| 0x0E | 0x800321D4 | DLL climb case (:502) |
| 0x0F | 0x80031DD0 | DLL swim-exit case (:610), land side; in deep water SwimTick (:751-808) |
| 0x10 | 0x80031E18 | DLL ground attack group (:649) |
| 0x11 | 0x80031E18 | DLL ground attack group (:649) |
| 0x12 | 0x80031E18 | DLL ground attack group (:649) |
| 0x13 | 0x80031E18 | DLL ground attack group (:649) |
| 0x14 | 0x80031DF8 | DLL air attack group (:659) |
| 0x15 | 0x80031DF8 | DLL air attack group (:659) |
| 0x16 | 0x80031DF8 | DLL air attack group (:659) |
| 0x17 | 0x80031DF8 | DLL air attack group (:659) |
| 0x18 | 0x80031E18 | DLL ground attack group (:649) |
| 0x19 | 0x80031E18 | DLL ground attack group (:649) |
| 0x1C | 0x80032558 | DLL BounceTick (:997) |
| 0x1D | 0x80031DD0 | DLL swim-exit case (:610), land side; in deep water SwimTick (:751-808) |
| 0x1F | 0x80031DF8 | DLL air attack group (:659) |
| 0x20 | 0x8003251C | NOT in DLL; no slice (cape, h2 / Q-H2) |
| 0x21 | 0x80032680 | NOT in DLL; no slice (cape, h2 / Q-H2) |
| 0x22 | 0x800324B8 | NOT in DLL; no slice (cape, h2 / Q-H2) |
| 0x23 | 0x800324B8 | NOT in DLL; no slice (cape, h2 / Q-H2) |
| 0x24 | 0x8003251C | NOT in DLL; no slice (cape, h2 / Q-H2) |
| 0x25 | 0x80031E18 | DLL ground attack group (:649) |
| 0x26 | 0x80031DF8 | DLL air attack group (:659) |
| 0x28 | 0x80031DD0 | DLL swim-exit case (:610), land side; in deep water SwimTick (:751-808) |
| 0x29 | 0x80031F54 | DLL PrepareSprintTick (:838) |
| 0x2A | 0x80032118 | DLL StopSprintTick (:943) |
| 0x2B | 0x80031E84 | DLL jump states case (:483) |
| 0x2C | 0x80031E84 | DLL jump states case (:483) |
| 0x2D | 0x80031E84 | DLL jump states case (:483) |
| 0x2E | 0x8003232C | DLL CarryGroupTick (:1518, g1a) |
| 0x2F | 0x8003232C | DLL CarryGroupTick (:1518, g1a) |
| 0x30 | 0x8003232C | DLL CarryGroupTick (:1518, g1a) |
| 0x31 | 0x80032680 | DLL hurt case (:635, c2b) |
| 0x32 | 0x800325A4 | NOT in DLL; slice h4a (tomes) |
| 0x33 | 0x800325A4 | NOT in DLL; slice h4a (tomes) |
| 0x34 | 0x80032680 | NOT in DLL; slice h4a (tomes) |
| 0x35 | 0x800321D4 | DLL climb case (:502) |
| 0x39 | 0x80031DD0 | NOT in DLL; slice E18 (death states) |
| 0x3A | 0x80031DB4 | DLL swim-exit case (:610), land side; in deep water SwimTick (:751-808) |
| 0x3B | 0x80031DB4 | DLL swim-exit case (:610), land side; in deep water SwimTick (:751-808) |
| 0x3C | 0x80031DD0 | NOT in DLL; slice E18 (death states) |
| 0x3E | 0x80032558 | DLL BounceTick (:997) |
| 0x3F | 0x80031E18 | DLL ground attack group (:649) |
| 0x40 | 0x80031E18 | DLL ground attack group (:649) |
| 0x41 | 0x80031DF8 | DLL air attack group (:659) |
| 0x42 | 0x80031DF8 | DLL air attack group (:659) |
| 0x44 | 0x80031E18 | DLL ground attack group (:649) |
| 0x46 | 0x80031DF8 | DLL air attack group (:659) |
| 0x49 | 0x80031E18 | DLL ground attack group (:649) |
| 0x4B | 0x80031DF8 | DLL air attack group (:659) |
| 0x4E | 0x80031DD0 | NOT in DLL; slice E18 (death states) |
| 0x4F | 0x80031DD0 | NOT in DLL; slice E18 (death states) |

Comptes : **51** états portés par une ligne d'appel de la DLL (2 Idle/Moving, 4 sauts, 2 échelle, 5 sortie de nage, 1 + 1 + 2 course, 1 blessure, 14 + 11 groupes d'attaque,
2 levée, 6 portage) ; **12** sans ligne mais avec une tranche : 5 états de cape (`20`-`24`, h2 : §1.30.6), 3 états de tomes (`32 33 34`, h4a), 4 états de mort
(`39 3C 4E 4F`, E18). Aucun état sans ligne ni tranche.

Notes.
- `0x39 / 0x3C / 0x4E / 0x4F` vont tous à `0x80031DD0` (le gestionnaire de `0F 1D 28`). Le fait 1 du plan ne les nomme pas. Ce sont des états de mort
  (`e20-i/swim-binary.md:169` : PV 0 donne `0x3C` sur la pente 4 et `0x39` ailleurs ; `e20-c2b-disc/binary/report-main.md` F34 les compte parmi les « Morts » ;
  `e20-c2b-disc/dll/report-main.md` D28 : la DLL n'a ni branche `Hp == 0` ni ces états). Tranche : E18 (« Mort et Réessayer », `docs/plan-conversion-totale.md:908`). Le site `0x80031DD0`
  est porté (`:610`) pour `0F 1D 28 3A 3B` seulement ; la DLL ne rangera `39 3C 4E 4F` qu'avec E18. Pas un arrêt : une tranche les portera.
- `0x34` (tome, fin du lancer) appelle `0x80032680`, le site de `31`, `3A` et `21` (`e20-h4/report.md` §1, « États du héros pendant le sort »).
- `0x21` : `0x80032680` ; `0x22 0x23` : `0x800324B8` ; `0x20 0x24` : `0x8003251C` : les gestionnaires de la cape. Aucune tranche d'objet ne les porte avant h2 (§1.30.6, en pause de relecture).
- Les 15 états `Skip` ne démarrent rien (aucun ne figure parmi les 63) ; le test d'annulation `SwimHandlerOf(état) == Skip` (D-H-5) les couvre, et les états `>= 0x50`.

## 2. (b) Les traces de l'oracle rejouées

Rejeu depuis une COPIE de `docs/plan-e20-annexe/e20-d` (Python 3, capstone 5.0.7, disque France présent), `python -B`, depuis la racine du dépôt :

```
python -B <copie>/oracle/base/sscen.py W3 W9 --cross     # 1 min 53 s ; --cross : le second interpréteur est égal tick par tick
python -B <copie>/oracle/base/rscen.py G7 --cross        # 1 min 12 s
diff -rq (originaux contre rejeu) : out-swim/w3, out-swim/w9, out/g7 -> identiques
```

Les traces à front de Rond (motif « hold contient O » sur `e20-d/oracle/out*`) : W3-O-idle, W3-O-swim, W9-circle-idle, W9-circle-swim, R7-circle-windup, R7-circle-sprint,
W9-item1F à W9-item32 (20). Aucune autre trace d'`e20-d` n'a de front du Rond.

**Sonde P1** (`probes/probe_items.py`, sortie `probe_items.out`) : crochets dans la machine de l'oracle sur `0x8003499C` (valeur de `s0`, retour) et sur le son 3
(adresse de retour `ra`). Dans les 20 scénarios W9-item, `s0` vaut l'objet équipé (donc `0x8004E0F8` le promeut), le départ rend 1, et le son 3 du tick 10 vient de :
- `ra = 0x80034AB4`, le **refus central** (le trou de la table) : objets `0x21`, `0x22`, `0x28`, `0x2A` ;
- l'intérieur du **départ de l'objet** lui-même, qui refuse en eau profonde (pente 4) : `0x1F` (`0x80034308`), `0x20` (`0x80034528`), `0x23` (`0x80034668`), `0x24` (`0x800346D8`),
  `0x25` (`0x80034748`), `0x26` (`0x800347B8`), `0x27` (`0x80034850`), `0x29` (`0x800348C8`), `0x2B` à `0x32` (`0x80034980`, la garde `+0x88 < 2` des tomes).

| Trace | h0a la rend exacte ? | Pourquoi |
|---|---|---|
| W3-O-idle, W3-O-swim, W9-circle-idle, W9-circle-swim, R7-circle-windup | **oui** | pas d'objet du Rond : `0x8004E0F8` rend -1, refus `>= 0x62`, son 3 (le câblage temporaire de la section 4 les rend égales, sons compris) |
| R7-circle-sprint | oui, déjà | `0x03` n'appelle pas `TryUseItem` (`Sprint_DoesNotCallTryUseItem`) : pas de son 3, l'oracle non plus |
| W9-item21, W9-item22, W9-item28, W9-item2A | **oui** | les quatre trous : le refus central |
| W9-item1F, 20, 23, 24, 25, 26, 27, 29, 2B à 32 (16 traces) | **non : attendent la tranche de leur objet** | le son 3 vient du départ de l'objet en eau profonde ; le bouchon de D-H-3 est silencieux |

Attribution des 16 : `0x1F` h3c, `0x23` h3b, `0x20` h2, `0x24 0x25 0x26 0x27 0x29` h1, `0x2B`-`0x32` h4a. (Le bouchon silencieux est l'écart transitoire que D-H-3 a décidé.)

## 3. (c) Les sondes

- **P1** ci-dessus (origine du son 3 des 20 scénarios W9-item).
- **P2** : la relecture au HEAD de ce qu'E20.c2b a posé (section 5).
- **P3** : l'ordre du pilote, lu dans `e20-h4/report.md` §0.9 et §1 (mesuré [M] par la tranche h4) : le premier appel du pilote a lieu au tick qui suit le départ, avec le compteur à 0 ; le compteur
  est incrémenté après l'appel du pilote de l'objet (ce que (a5) de T1 demande). Non rejoué ici : h0a ne porte aucun objet ; ce sera l'oracle des tranches d'objet.

## 4. (d) La liste prédite des tests déplacés, mesurée par un câblage temporaire

Méthode. `probes/temp-wiring.patch` (jamais committé, annulé ensuite) remplace le corps du bouchon `TryUseItem` par : couture `TryUseItemResultForTests` prioritaire ; sans front du Rond : 1 ; sans hôte : 1 ;
mot de contrôle non nul : son 3 et 1 ; sinon `SetItemIdFromCurrentItemId` (l'équivalent de `0x8004E0F8`) et, si le résultat est `>= 0x62`, hors de `0x1F`-`0x32` ou l'un des quatre trous : son 3 ; puis 1
(les départs d'objet restent silencieux : D-H-3). Le pilote, l'annulation et le champ d'objet en cours n'y sont pas (aucun test ne peut les observer tant qu'aucun départ ne pose d'objet en cours).
Dans le même état temporaire, les deux exceptions du son 3 ont été retirées (`HeroRunTests.cs:614`, `HeroSwimTests.cs:62`).

Résultats (Release, copie neutralisée, `--blame-hang-timeout 60s`). La suite entière dure **16 min 23 s** au HEAD (6 921 tests, 0 échec : au-dessus du plafond de 10 min d'une commande) ; elle se mesure en quatre
tranches par `--filter` dont la somme est 6 921 :

| Tranche (`FullyQualifiedName`, préfixe `Alundra.Tests.`) | Tests | Échecs avec le câblage, exceptions retirées |
|---|---|---|
| `~Hero` | 1 705 | 0 |
| `~Arcs`, `~Maps`, `~Support`, `~Combat` | 686 | 0 |
| `~Ai`, `~Audio`, `~Cameras`, `~Cells`, `~Data`, `~Dialogues`, `~Entities`, `~Events` | 2 662 (10 min 8 s) | 0 |
| tout le reste (`!~` des onze précédents, `Hero` compris) | 1 868 | 0 |

Contrôle négatif : mêmes exceptions retirées **sans** le câblage : 4 échecs sur 369 tests de `HeroSwimTests` et `HeroRunTests`, tous « sounds: dll [], oracle [3] » :
`W9W10_TheOtherCases_AreTheOracle("w9/W9-circle-idle")`, `("w9/W9-circle-swim")`, `W3_TheCircle_DoesNotBurst_ItIsTheItemButton` (W3-O-swim) et
`G7_InThePreparation_TheSquareSwingsTheSword_TheCrossAndTheCircleDoNothing` (R7-circle-windup).

**Liste prédite recomptée au HEAD** : exactement les sites qui ignorent le son 3 ; ils deviennent exacts et perdent leur exception ; **aucun autre test ne bouge**.
- `Alundra.Tests/Hero/HeroRunTests.cs:614` (`Equal("g7/R7-circle-windup", ignore: (tick, field) => tick == 10 && field == "sounds")` ; plan : `AlundraHeroRunTests.cs:612`) ;
- `Alundra.Tests/Hero/HeroSwimTests.cs:62` (`StubbedSounds` : `id == 3 ||` ; plan : `AlundraHeroSwimTests.cs:60`) ; le paramètre `ignoreSound` de `RunMontage` reste utilisé pour 21 et 6 ;
- `Alundra.Tests/Support/RunMontage.cs:226` (plan : `AlundraRunSupport.cs:402-405`) : seulement le commentaire « the sound 3 of the Circle is E20.h » à corriger, pas de code.
Commentaires à corriger avec eux : `HeroSwimTests.cs:22`, `HeroRunTests.cs:22`, `HeroRunTests.cs:613`.

Inchangés à h0a : `ItemUseDriverHookForTests` (c2b, `HeroHurtTests`), `TryUseItemCallCountForTests`, `TryUseItemResultForTests` (`HeroRunStatesTests`, `HeroSwimBranchTests`, `HeroHurtTests`, `HeroSwimTests`).

## 5. Relecture au HEAD de ce qu'E20.c2b a posé

- **Le bloc de blessure** (`HurtBlock`, `:697-707`) appelle `UpdateItemUseDriver` **une fois**, au tick de la blessure, après `UpdatePlayerCarriedEntity(player, 2)` et **avant** de ranger l'état
  (`TargetAnimationId = slopeAtHead == 4 ? 0x3A : 0x31`), la direction et `DamagedTickCounter = 0x78` ; puis `EndOfMovePlayer` et la fin du tick. Comme le binaire (`0x80031C6C`, avant que
  l'animation passe à `0x31/0x3A`) : le pilote fait un dernier pas avec l'ancienne animation. Le tick libre normal l'appelle en `:385`, après le bloc de blessure (`0x80031CC4`).
- **`case 0x31`** (`:633-636`) appelle `TryUseItem` (`0x80032680`), résultat ignoré, et le tick finit. Avec h0a ce n'est plus un bouchon : un front du Rond pendant `0x31` (et `0x3A`, `0x3B` par
  `SwimTick` `:757` et `:808`) tente l'objet. **F27** (`e20-c2b-disc/binary/report-main.md:204`) : les départs `0x80034224` (0x1F), `0x8003453C` (0x23) et les cinq soins instantanés n'ont aucun
  test d'état ; les tomes (`0x800348E0`) exigent `+0x88 < 2`, la cape (`0x80034320`) l'état `< 2` et la pente 3. Un Rond pendant la blessure peut donc lancer une bombe, des graines ou boire un
  soin, comme le binaire. Dans h0a tous ces départs sont des bouchons silencieux : l'effet n'apparaît qu'avec h1, h3 et h2 ; h4a ne le subit pas (garde d'état). Chacune a donc à couvrir « Rond pendant `0x31` »
  (h1 le fait, §1.30.5). L'objet en cours **survit** à une blessure (F29 : aucune annulation, le pilote continue au tick libre suivant).
- **Le chemin verrouillé** (`:347-374`) : après les deux remises à zéro (`:352-353`) et `UpdatePlayerCarriedEntity(player, 1)` (`:357`) vient le bloc `== ForcedSequence` de c2b (entrée de la blessure et
  `DamagedTickCounter = 0x78`). L'annulation `0x800350C0` du binaire est à `0x80031BFC`, **entre** les deux (après `UpdatePlayerCarriedEntity(1)`, avant `0x80031C0C`) : T1 (a6) pose l'appel de
  `CancelItemMode` à cet endroit, pas « juste avant `EndOfMovePlayer` ».
- Les coutures de c2b restent : `ItemUseDriverHookForTests` (appelée dans `UpdateItemUseDriver`, `:1766`) sert `HeroHurtTests` ; D-H-4 s'y ajoute.

## 6. Écarts de texte du plan à connaître avant T1 (aucun n'est un arrêt)

1. **Numéros de ligne** : les 13 lignes d'appel du plan sont 17 au HEAD (section 1) ; le pilote est appelé en `:385` (tick libre) et `:702` (`HurtBlock`), pas en `:353` ; `CancelItemMode` est en `:822`
   (appelé par `SwimTick` `:746`) ; le commentaire « NOT PORTED » de la chaîne principale est en `:672` ; `TryUseItem` en `:1014`, `UpdateItemUseDriver` en `:1764`.
2. **(a6)** : `0x05` n'est plus un état « non porté » (g1a : `LiftGestureTick`, `:1498`). Exemples d'états non portés qui ne sont pas `Skip` : `0x20` (cape ; h2 le portera, D-H2-8 : prendre alors un autre
   exemple), `0x32` (tome), `0x39` (mort).
3. **(a6) verrouillé** : l'ordre de la section 5 (avant le bloc de la séquence forcée de c2b).
4. **(a7)** : « pour chaque état de la DLL de la table T0 (a) » = les 51 états « DLL » de la section 1 ; pour les 17 sans réaction le départ n'est pas appelé (`03` et `36` n'appellent pas `TryUseItem` dans la DLL).
5. **Suites** : la suite Release entière dépasse 10 minutes (16 min 23 s) ; T1, T2 et T3 la rendent par les quatre tranches de la section 4 (la plus longue : 10 min 8 s ; à scinder en `~Ai` d'un côté et
   le reste de l'autre si elle dépasse).
6. **Fait 1 du plan** : y ajouter les quatre états de mort `39 3C 4E 4F` (E18) à la liste des états que la DLL ne porte pas (section 1).
