# O-G2C2-2 : an unburnt bramble hurts the hero on contact, in the binary too

Read-only check of 2026-10-26 (workflow `wf_648e39d5-c2e`): the report and its fresh critic, verbatim, in French. Verdict: CONFIRMED, the original behaves as the DLL (one point of life every 120 ticks while the hero touches the bramble at rest). The scripts and the copies of the oracle stayed in the session scratchpad (`scratchpad/bramble/check/`, `scratchpad/bramble/critic/`).

## Report

# Les ronces non brûlées (types 238 et 239) blessent-elles Alundra au binaire ?

## Réponse

**Oui.** Au binaire ALUN_CD.EXE (France), une ronce au repos blesse Alundra dès qu'il la touche. Je l'ai établi par lecture du binaire et du disque, puis rejoué sur l'oracle de c1/c2 (code réel du binaire). L'original et la DLL se comportent donc pareil sur ce point. O-G2C2-2 se ferme sans écart, et aucun code ni aucun test existant ne bouge.

## Faits

Les adresses sont celles de `ALUN_CD.EXE`. Les octets du disque viennent de `DATA/BALANCE.BIN` et `DATA/DATAS.BIN` du dossier extrait de la version France.

### La fiche d'équilibre

1. **[FAIT]** Pour 238 et 239, la fiche est lue à l'offset 4840 (238) ou 4859 (239) de `BALANCE.BIN`. Les 19 octets sont identiques pour les deux types :
   - `ff 13 01 | 80 80 80 80 80 10 80 80 80 80 80 | 02 | 06 01 | 01 01`
   - niveau 255, taille 19, MaxHp 1, une seule fiche (pas de chaîne de niveaux) ;
   - réponses : 0x10 (normal) pour l'attribut 6 seul, 0x80 (immune) pour tous les autres ;
   - deux attaques : `Attacks[0]` = (attribut 6, feu, puissance 1) et `Attacks[1]` = (attribut 1, puissance 1) ;
   - le bit 7 n'est jamais posé, donc pas de terme d'équipement.
2. **[MESURÉ]** Ces 19 octets égalent l'export (`balance.json`). Les 512 fiches de premier niveau du disque égalent aussi l'export (`s2_disc.py`).
3. **[MESURÉ]** La vraie fonction `0x80044550` rend ces mêmes octets pour 238 et 239, à tous les niveaux de carte essayés (0, 2, 4, 7, 9, 255). La fiche ne dépend donc pas de la carte (`r1_basic.py`).
4. **[FAIT]** Les bits de classe de coup ne sont pas dans la fiche d'équilibre : ils sont dans les drapeaux d'en-tête (fait 5).

### Les drapeaux d'en-tête

5. **[FAIT]** `InitializeEntity` (`0x80039DA4-0x80039DD0`) construit `+0x6C` = `lhu 0x10(hdr)` | `lbu 0x12(hdr) << 16`. Les 16 octets d'en-tête du disque, trouvés une seule fois dans `DATAS.BIN` (offsets 219592 pour 238, 221044 pour 239), égalent `map_alundra.json` et l'export de la DLL (`alundra-project/Data/sprite-records.json`).

| Entité | MoreFlags | CanPickup | Drapeaux `+0x6C` | Classes |
|---|---|---|---|---|
| Ronce 238 et 239 | 0xC3 | 0x21 | `0x0321C3` | ClassA (0x01), HitsClassB (0x02), DeactivateOnAnimationEnd (0x40), Collidable (0x80) |
| Héros (type 0) | 0x8C | 0x11 | `0x03118C` | HitsClassA (0x04), ClassB (0x08), Collidable (0x80), HitsClassC (0x1000) |

   - La ronce frappe la classe B et le héros est de classe B.
   - Elle n'a pas HitsClassA : c'est ce qui explique que la torche posée ne touche pas les ronces.

### La passe de coups `0x80039300`

6. **[FAIT]** Un attaquant est retenu si toutes ces conditions tiennent (`0x80039364-0x800393C0`) :
   - il a une boîte d'image (`+0x1D4 != 0`) ;
   - son attaque courante (`+0x1C8`) est non nulle et son premier octet est non nul ;
   - son masque de classe est non nul, avec `mask = ((F>>2)&1) | ((F<<2)&8) | (F&0x1000 ? 0x800 : 0)`.
7. **[FAIT]** Une cible est écartée (`0x8003941C-0x8003945C`) si l'une de ces conditions tient :
   - `+0x1D0 != 0` ;
   - `+0x1CC != 0` ;
   - `+0xB4 & 0x40` ;
   - `(Flags & mask) == 0`.

   Vient ensuite un test de recouvrement sur les trois axes, élargi d'une unité brute (`0x8003946C-0x8003951C`).
8. **[FAIT]** L'immunité est testée sur `+0x1C4` de la cible (`0x800395D4-0x800395EC`) : si `(r & 0xC0) == 0x80`, `+0x224` n'est pas écrit. La fiche du héros (index 0) a toutes ses réponses à 0, donc il n'est jamais immune. Les étincelles ont lieu dans tous les cas.
9. **[FAIT]** Le bloc de changement d'animation (`0x80038B74-0x80038BAC`) charge l'attaque courante ainsi : `Attacks[anim+1]` si `anim+1 < nombre d'attaques`, sinon `Attacks[0]`.
   - Animation 0, la ronce au repos : `Attacks[1]` = (attribut 1, puissance 1). Ce n'est pas l'attaque de feu.
   - Animation 1, la ronce qui brûle : `Attacks[0]` = (6, 1).
10. **[FAIT]** La boîte d'attaque de l'animation 0 est identique sur les 4 directions. Elle est relue sur le disque : l'octet de collision `eb f3 00 2a 1a 1f` est à l'offset `219968`, et l'octet `0xD` du jeu d'animations 0 vaut 0 (pas de bit 0x40, donc la ronce au repos est touchable et attaque).

| Type | Corps (décalage et taille) | Boîte d'attaque, animation 0 |
|---|---|---|
| 238 | (−20, −12, 0), 40×24×30 | (−21, −13, 0), 42×26×31 |
| 239 | (−8, −5, 0), 16×10×30 | (−9, −6, 0), 18×12×31 |

    - La boîte dépasse le corps d'un pixel de chaque côté.
    - L'animation 1 est la ronce qui brûle : `+0xB4 = 0x40` (elle-même intouchable) et des boîtes jusqu'à 62×26×31 (238).
11. **[FAIT]** Aucun programme de la ronce ne la modifie au repos :
    - A[0] (`0x8006174C`) n'écrit qu'un demi-mot dans `+0x280` ;
    - C[0], D[0] et F[0] sont nuls ;
    - E[0] ne part que si `+0xAC != 0`, et l'animation 0 boucle ;
    - D34 (`0x8007E8AC`) ne tourne que si la ronce est elle-même touchée.

    La ronce n'a pas DeactivateOnHit, donc blesser le héros ne la détruit pas.
12. **[INFÉRENCE]** Au binaire, la ronce au repos touche donc le héros : statut 2, classes compatibles, attaque non nulle, boîte présente, héros jamais immune. La DLL applique les mêmes règles :
    - `Flags` est construit comme au binaire (`EntitySpawnFactory.cs:740`) ;
    - `ClassMaskOf`, la passe de coups et `BalanceRecord.AttackForAnimation` suivent les règles ci-dessus.

### Rejeu sur l'oracle

Le dispositif est la copie de `e20-c2/oracle` (code réel du binaire : spawner, `InitializeEntity`, sélection d'équilibre, passe de coups, `ComputeNewHp`, `MovePlayer`). J'y ai ajouté le chemin banque commune du spawner (octet 5 = 0x40, indice d'équilibre = l'octet 6) par deux monkey-patches dans `bworld.py`. Ni l'oracle ni le dépôt ne sont modifiés. Les programmes de la ronce sont inertes, comme dans c1.

13. **[MESURÉ]** Héros au repos à 28 px à l'ouest de la ronce 238 : touché au tick 1, blessé au tick 2 (PV 10 → 9, animation `0x31`, `+0x1CC` = 119, recul vers l'ouest, trois étincelles). Rien n'a été lu avant d'être écrit.
14. **[MESURÉ]** Héros collé à la ronce : quand il marche dedans, il est arrêté contre le corps (x = 221 par l'ouest, 282 par l'est, y = 76 par le nord, 115 par le sud) et blessé de tous les côtés.
    - Par l'ouest : premier contact au tick 12, blessures aux ticks 13, 133 et 253 (PV 9, 8, 7).
    - Les trois autres directions donnent des résultats analogues sur 200 ticks.
    - Contact maintenu 260 ticks : touches aux ticks 1, 121, 241 et blessures aux ticks 2, 122, 242, soit une fois toutes les 120 ticks.
15. **[MESURÉ]** Fenêtre de contact au repos, ronce 238 :
    - x du héros ∈ [221, 282], soit exactement de « collé à l'ouest » à « collé à l'est » ;
    - à dx 28, y du héros ∈ [76, 115] (dy ∈ [−20, 19]).

    Être collé au corps suffit donc toujours pour être touché.
16. **[MESURÉ]** Ronce 239 : touché pour dx ∈ [−18, 19]. Collé en marchant, le héros s'arrête à x = 233, premier contact au tick 17, blessé aux ticks 18 et 138.
17. **[MESURÉ]** Dégâts : exactement 1 PV par blessure avec chacun de ces 7 équipements : tenue de départ, cuir, antique, argent, sans armure, bracelet, charme. Le minimum de 1 s'applique car la puissance est 1.
18. **[MESURÉ]** Témoins qui disent « non » :
    - ronce sans son bit HitsClassB : aucun contact ;
    - héros sans son bit ClassB : aucun contact ;
    - hors de la fenêtre : aucun contact.
19. **[MESURÉ]** Trois patchs d'un mot du binaire émulé suppriment le contact (terme de masque `0x800393A8`, test de classe `0x8003945C`, saut sur « a une boîte » `0x8003936C`). Le témoin « même mot réécrit » ne change rien.
20. **[MESURÉ]** Fiabilité du rejeu :
    - aucun octet non initialisé n'a été lu ;
    - le second interpréteur (`mips.py`) donne 0 différence de colonne et des octets d'entité identiques à chaque tick sur 3 scénarios ;
    - le modèle indépendant du plan (`scen.hit_model_multi`) n'a aucun écart avec le réel sur environ 300 runs de balayage.
21. **[MESURÉ, à titre d'information]** Une ronce qui brûle (`+0x88` = 1) blesse aussi le héros : attaque (6, 1), 1 PV, `+0xB4 = 0x40`.
22. **[MESURÉ]** Avec un coup d'épée au tick 1 à 21 px, le héros est touché au tick 1 et blessé au tick 2. L'épée touche la ronce (étincelles) sans l'activer, car elle est immune à l'attribut 1.

## Limites

- **[FAIT]** Le binaire ne contient pas les fiches ni les en-têtes : il les charge depuis le disque. J'ai donc vérifié les octets du disque et rejoué le code réel du binaire dessus.
- **[INFÉRENCE]** Le rejeu est sur un champ plat à hauteur 0, avec la ronce sur le même niveau que le héros. Un héros qui saute plus haut que 31 px au-dessus de la ronce ne la touche pas.
- **[INFÉRENCE]** Je n'ai pas lancé la DLL (aucun `dotnet`, consigne lecture seule). La sonde de la DLL (T2, g2c2) notait `0x31` au tick 4 pour un coup d'épée à 21 px, contre le tick 2 sur l'oracle. L'écart vient très probablement de la numérotation des ticks du monde de test (mise en place), pas de la règle, mais je ne l'ai pas vérifié.
- **[INFÉRENCE]** La seule incertitude côté DLL est l'arrondi de la position d'arrêt du héros poussé contre une ronce. La marge de la boîte n'est que de 1 px, donc un arrêt décalé d'une unité brute changerait le résultat.

## Ce qu'il faudrait changer

- **Code :** rien. L'original blesse comme la DLL.
- **Documentation :** fermer O-G2C2-2 au plan (§2) et amender D-G2C2-10 (7) en « confirmé au binaire le 2026-10-26 (oracle) ». Répondre aussi au point 6 de `e20-g2c2/t2-results.md` (« to check by the author if the brambles prick »).
- **Tests existants qui bougent :** aucun. `TorchWorldTests` W4 et `BrambleAiTests` ne dépendent pas de ce point.
- **Test optionnel à ajouter** (la preuve que la DLL égale l'oracle), avec une ronce 238 sur le même niveau que le héros :
  - héros au repos à 28 px de la ronce : touché au tick 1, blessé au tick 2, PV −1, animation `0x31`, puis de nouveau au tick 122 s'il reste en contact ;
  - héros qui marche dedans depuis 60 px : premier contact au tick 12, blessures aux ticks 13 et 133 ;
  - fenêtre de contact dx ∈ [−30, 31] à dy 0 pour la 238, et dx ∈ [−18, 19] pour la 239.

## Fichiers

Tous dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/bramble/check/` :

| Fichier | Contenu |
|---|---|
| `s1_data.py` | données exportées |
| `s2_disc.py` | octets du disque |
| `bworld.py` | extension de l'oracle (banque commune) |
| `r1_basic.py` | sélection réelle `0x80044550` et un contact au repos |
| `r2_scen.py` | marche dans la ronce, balayages dx et dy, témoins, contact maintenu |
| `r3_extra.py` | quatre approches, type 239, armures, ronce qui brûle |
| `r4_sword.py` | coup d'épée |
| `r5_two_interp.py` | second interpréteur |
| `r6_teeth.py` | patchs |
| `out/R2-walk.txt` | marche dans la ronce, tick par tick |
| `out/R3-reach.txt` | balayage dx |
| `out/R6-keep.txt` | contact maintenu 260 ticks |
| `out/B1-burning.txt` | ronce qui brûle |
| `oracle/` | copie de `e20-c2/oracle` (outils, `hworld.py`, `hscen.py`, `hmodel.py`) |
| `bx.py` | copie de l'aide de désassemblage |

Rien n'a été écrit dans le dépôt.

## Fresh critic

# Critique frais du relevé « les ronces 238 et 239 blessent-elles Alundra ? »

**Réponse : CONFIRMÉ.** Au binaire ALUN_CD.EXE (France), une ronce non brûlée blesse le héros qui la touche. J'ai refait les lectures du binaire, des octets du disque et du HEAD, et rejoué moi-même tous les scripts du relevé : les sorties sont identiques. Trois corrections de détail, dont aucune ne change la réponse. L'affirmation « la DLL se comporte pareil » n'est pas mesurable ici.

Mes scripts et sorties sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/bramble/critic/` (`run/` contient la copie rejouée de l'oracle). Je n'ai rien écrit dans le dépôt.

Un fichier suivi du dépôt, `Alundra/Ai/Items/BombAi.cs`, apparaît modifié, alors qu'il ne l'était pas au début de ma session. Cela ne vient pas de moi, mes commandes étaient en lecture seule.

## Verdicts par fait

| # | Verdict | Ce que j'ai refait |
|---|---|---|
| 1 | CONFIRMÉ | `BALANCE.BIN` du disque, offsets 4840 et 4859 : `ff130180808080801080808080800206010101`. |
| 2 | CONFIRMÉ | Disque égal à `balance.json` pour les 512 fiches de premier niveau (`s2_disc.py` rejoué). |
| 3 | CONFIRMÉ | `0x80044550` exécuté par l'oracle : mêmes 19 octets aux niveaux 0, 2, 4, 7, 9 et 255. Désassemblage : un niveau de fiche à 255 n'est jamais « inférieur » au niveau de la carte, donc pas de chaîne. |
| 4 | CONFIRMÉ | La fiche ne porte aucun bit de classe. |
| 5 | CONFIRMÉ | `0x80039DA4-0x80039DD0` : `+0x6C = lhu 0x10(hdr) | lbu 0x12(hdr) << 16`. Les 16 octets d'en-tête sont uniques dans `DATAS.BIN` (219592 pour 238, 221044 pour 239, 30636 pour le héros). Drapeaux : ronce `0x0321C3`, héros `0x03118C`. Noms de bits vérifiés dans `EntityFlags.cs`. |
| 6, 7 | CONFIRMÉ | Désassemblage de `0x80039364-0x8003945C`, conditions et ordre exacts. |
| 7 (recouvrement) | CONFIRMÉ | Recouvrement sur trois axes, élargi d'une unité brute. Recalculé à la main sur les boîtes : fenêtre en x du héros = ]220, 283[ (entiers 221 à 282), en y = [76, 115]. |
| 8 | CONFIRMÉ | `0x800395D4-0x80039670` : `+0x224` n'est pas écrit si `(r & 0xC0) == 0x80`, mais `+0x1D0 = 0x19` et les étincelles ont toujours lieu. Réponses du héros toutes à 0 au disque. |
| 9 | CONFIRMÉ | `0x80038B74-0x80038BAC` : `Attacks[anim+1]` si `anim+1 < n`, sinon `Attacks[0]`. Animation 0 donne (1,1), animation 1 donne (6,1). |
| 10 | CONFIRMÉ | Boîtes lues dans l'export et dans les octets du disque (`eb f3 00 2a 1a 1f` pour 238, `f7 fa 00 12 0c 1f` pour 239). L'animation 0 se termine par le code de boucle 1 dans les 4 directions, donc `+0xAC` reste à 0. L'octet `0xD` de l'animation 0 vaut 0. |
| 11 | CORRIGÉ (libellé) | Le fond est confirmé, voir la correction 1 ci-dessous. |
| 12 | CONFIRMÉ | Inférence correcte. Les lignes de l'ancien roster E14 (« hittable and hurts the hero ») concluaient déjà dans le même sens depuis les drapeaux. |
| 13 | CONFIRMÉ | Rejeu : touché au tick 1, PV 10 vers 9 au tick 2, animation 49, `+0x1CC = 119`, recul, 3 étincelles. |
| 14 | CONFIRMÉ | Marche dans la ronce par l'ouest : contact au tick 12, blessures aux ticks 13, 133, 253 (PV 9, 8, 7). Positions d'arrêt 221, 282, 76, 115. Contact maintenu : touches aux ticks 1, 121, 241, une fois toutes les 120 ticks. |
| 15 | CONFIRMÉ | Fenêtre dx ∈ [−30, 31] (x du héros de 221 à 282), dy ∈ [−20, 19]. Cohérente avec mon calcul à la main. |
| 16 | CONFIRMÉ | 239 : dx ∈ [−18, 19], arrêt à 233, contact au tick 17, blessure au tick 18. |
| 17 | CONFIRMÉ | 1 PV avec chacun des 7 équipements. |
| 18, 19 | CONFIRMÉ | Les 3 témoins « non » et les 3 mots patchés donnent bien « aucun contact » ; le témoin « même mot réécrit » ne change rien. |
| 20 | CONFIRMÉ | Aucun octet non initialisé lu dans 7 scénarios que j'ai relancés. Second interpréteur : 0 différence sur 3 scénarios. Modèle du plan : 0 écart sur les balayages. Ma copie de l'oracle (`hworld`, `hscen`, `hmodel` et 29 fichiers `tools/`) est identique à `e20-c2/oracle` du HEAD, aux fins de ligne près. |
| 21, 22 | CONFIRMÉ | Ronce qui brûle : (6,1), 1 PV, `+0xB4 = 0x40`. Coup d'épée à 21 px : étincelles sur la ronce sans l'activer, héros blessé au tick 2. |

## Points que j'ai vérifiés en plus

- **Le chemin du spawner (`0x8003A260-0x8003A308` et `0x80039B28`) est bien celui du relevé.** Pour un sprite global (bit 0x80 absent de l'octet 5), il prend le bloc commun `0x80126EC0`. L'indice d'équilibre est l'octet 6, sans `| 0x100`. Les 61 placements de 238 et les 96 de 239 ont tous l'octet 5 à `0x40`.
- **La ronce entre dans la liste de coups** (`0x80038548` : statut 2 ou 3 et `+0x20 == 0`). Elle atteint le statut 2 seule : `InitializeEntity` pose le statut 1 et la première passe d'événements le passe à 2 (`0x80038754`).
- **Aucun programme ne la modifie au repos.**
  - A[0] `0x8006174C` écrit un demi-mot à `+0x280` et rien d'autre.
  - Les entrées 0 de C, D et F sont nulles.
  - E[0] ne part que si `+0xAC != 0` (`0x80038824`), et l'animation 0 boucle.
  - D34 n'est choisi que si `+0x224 != 0` (`0x80038840`), c'est-à-dire si la ronce a été touchée.
  - Dans l'oracle, la vraie passe d'événements choisit le programme de la ronce, le journal des appels est vide sur 150 ticks, et `+0x224` reste nul même sous coup d'épée.

## Corrections

1. **Fait 11, libellé.** La ronce n'utilise pas « D[0] » : son emplacement D vaut 34 (octet d'en-tête `+0x15`, copié en `+0x7C`). D[0] est bien nul, mais ce n'est pas le D de la ronce. La phrase correcte est : « C[0] et F[0] nuls, D = D34 seul, qui ne tourne que si la ronce est touchée ». Le fond est inchangé.
2. **Fait 5, remarque annexe : elle est inversée.** Le relevé dit « elle n'a pas HitsClassA : c'est ce qui explique que la torche posée ne touche pas les ronces ». C'est la torche (type 393) qui n'a pas HitsClassA : son `MoreFlags` est `0xE2` dans le roster E14. La ronce sans HitsClassA ne frappe pas les entités ClassA (ennemis, autres ronces). Cela ne change rien à la réponse.
3. **Limites, dernier point.** « Un arrêt décalé d'une unité brute changerait le résultat » est faux. La marge entre le bord du corps et le bord de la boîte d'attaque est d'un pixel complet (65 536 unités brutes). Un décalage d'une unité brute ne change rien. Il faudrait que le héros s'arrête à un pixel ou plus du corps : en x, x ≤ 220 pour la 238.

## Ce qui n'est pas vérifiable ici

- **La parité chiffrée avec la DLL : NON VÉRIFIABLE** (lecture seule, aucun `dotnet`). Ce qui est établi pour la DLL est qualitatif :
  - Le code lu au HEAD suit les mêmes règles. J'ai relu `HitPass.cs`, `BalanceRecord.AttackForAnimation` et `EntitySpawnFactory.cs:740` (chemin réel `Alundra/Entities/Spawning/`, non `Alundra/Entities/`).
  - La sonde T2 de g2c2 a mesuré la DLL blessant un héros placé près d'une ronce.
- **Ce que cette sonde ne couvre pas.** Elle faisait naître la ronce à 21 px du héros, donc corps superposés, sans arrêt contre le corps. Ni le contact en marchant, ni la fenêtre, ni le cycle de 120 ticks, ni l'écart « tick 4 » (DLL) contre « tick 2 » (oracle) n'ont été vérifiés sur la DLL.
- **Si l'on veut fermer O-G2C2-2 aussi côté DLL**, le test optionnel du relevé est le bon : marche depuis 60 px, premier contact au tick 12, blessures aux ticks 13 et 133. Je le recommanderais plutôt que de le laisser optionnel.
- **Cinq placements de 238** (cartes 441 et 442, `EventCodesA_LoadIndex` 129 ou 130) ont un programme de chargement scripté, que personne n'a analysé. La réponse porte sur le cas général, soit les 152 autres placements.

## Ce qu'il faudrait changer

- **Réponse : CONFIRMÉ.** Il n'y a aucun écart de comportement connu entre l'original et la DLL. Rien dans le code ni dans les tests existants ne bouge. J'ai vérifié que `TorchWorldTests` W4 et `BrambleAiTests` n'affirment rien sur la blessure du héros. Les autres tests qui citent 238 ou 239 ne portent que sur l'enrôlement de D34.
- **Documentation, acceptable après les corrections ci-dessus :** fermer O-G2C2-2 au plan (§2). Amender D-G2C2-10 point (7), qui existe bien, en « confirmé au binaire (oracle) ». Répondre au point 6 de `docs/plan-e20-annexe/e20-g2c2/t2-results.md`. Formuler « parité qualitative confirmée, parité chiffrée non mesurée » plutôt que « se comportent pareil ».
