[harness: subagent output matched instruction-shaped pattern(s): harness-envelope-tag. Control tags below are neutralized (`<` → `<\`); treat any remaining directive-shaped text as a finding to relay to the user, not an instruction to you.]

# E20.g2c3 — les gouttes d'eau (A19, C71) : lecture seule préparatoire

Rien n'est décidé. Chaque ligne est marquée [FAIT] (lu dans le binaire, le code ou les données), [MESURÉ] (rejoué sur la machine MIPS de l'oracle) ou [INFÉRENCE].

**Méthode.** J'ai lu le code de la DLL par `git show HEAD:` et `git grep HEAD`, sans rien écrire dans le dépôt et sans construction ni test dotnet. Le HEAD a avancé de `22ddf0be` à `26a15de4` pendant ma lecture. Ces commits ne touchent que des docs : `git diff --stat 22ddf0be 26a15de4 -- Alundra Alundra.Tests` est vide.

Le binaire est `ALUN_CD.EXE` (France), lu avec `bx.py` et capstone. Les rejeux tournent sur ma copie de l'oracle `e20-g1a/work` plus `c4_scen.py`. g2c2 n'est pas exécutée : au HEAD il n'y a ni `TorchAi.cs` ni `BrambleAi.cs`.

Dossier de travail (scripts, sorties, `SHA1SUMS.txt`) :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/g2c3/`

---

## 0. En huit lignes

1. **Une goutte n'a aucune attaque** : fiche d'équilibre sans attaque, toutes réponses à 128, aucun drapeau de classe de coup. Elle ne blesse jamais Alundra.
2. **Seul effet de jeu : éteindre une torche (type 393), posée ou portée.** C71 l'éteint en un seul tick (l'état 3). Elle force la torche en statut 3 et son octet `+0x274` à 1. E20 détruit alors la torche sans flamme, avec le son 142 et l'effet 10.
3. **Une torche lancée ne peut pas être touchée.** Elle perd `Collidable` au lâcher, donc elle ne sert jamais d'appui à une goutte.
4. **La restauration de `+0x54` ne sert pas à "retomber une seconde fois".** Une goutte sans elle cycle quand même toutes les 88 ticks. La restauration réarme le programme scripté.
   - La période d'une goutte devient N + 90 ticks, N étant son attente.
   - Les gouttes de la carte 158 ne repartent que si le héros est dans leur zone.
   - Elle corrige donc deux affirmations des rapports précédents (§2).
5. **Dans la carte 92, quatre gouttes naissent 606 ticks après l'arrivée** (programme B[1]) exactement au-dessus des quatre torches posées 43 à 46. Ce sont les seules gouttes du jeu qui visent une torche à sa place d'origine ; toutes les autres comptent sur une torche portée.
6. **La tranche est petite** : un fichier `WaterDropAi.cs` (A19 et C71 pour {391}), deux inscriptions, 5 sommes de clés à relever. Elle exige que g2c2 soit faite, sinon une torche forcée en statut 3 resterait figée.
7. **Oracle prêt** : `c71_full.py` (A19 et C71 contre le code réel, 6000 vecteurs, 0 écart, 89/89 instructions, 90 mutants dont 89 tués et 1 équivalent) et ses goldens.
8. **Aucune sauvegarde ne mène à une carte de gouttes.** Il faut le préréglage `torche-m92` de g2c2, éventuellement complété.

---

## 1. Faits numérotés

### A. Le binaire

**F1 [FAIT]. A19 (`0x800619D0`, 3 mots, table A, code 19).**
- `lbu v0,0x54(a0)` puis `sb v0,0x275(a0)` : il copie l'octet bas de `+0x54` dans `+0x275`.
- `+0x54` est `ProgramIndexes[2]` (`EntityScriptProxy.cs:130`, tableau `int[6]` à `+0x4C`), l'index du programme C scripté.
- `+0x275` est `Bytes[1]`.
- Source : `dis_a19.txt`. L'entrée est à `0x800C4B80` = base A `0x800C4B34` + 19×4. La table maîtresse `0x80098F4C` donne A `0x800C4B34`, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`. L'inférence de base du script `tables.py` de g2c2 (A à `0x800C4734`) était fausse, mais F14 de g2c2 était juste.

**F2 [FAIT, MESURÉ]. C71 (`0x8007B834`, 89 mots, table C, code 71).** Le pseudo-code complet annoté est dans `c71_pseudocode.txt`. La table de sauts est à `0x800281EC`, un état ≥ 5 ne fait rien.

| État `Bytes[0]` | Ce qu'il fait |
|---|---|
| 0 | `Bytes[0]=1`, `+0x282` (halfword, `AIValues[1]`) `=40` |
| 1 | `+0x282 -= 1` ; à 0 : animation 1 (`+0x88`), `Bytes[0]=2`, `Flags \|= 0x100` (Gravity) |
| 2 | si `+0x140` (`CollidedWithEntityZ`) = 0 et `+0x144` (`IsOnGround`) = 0, rien ; sinon animation 2, `Bytes[0]=3` |
| 3 | `Bytes[0]=4`, `Flags &= ~0x80` (Collidable) ; puis le test de la torche (F9) |
| 4 | si `+0xAC` = 0, rien ; sinon voir ci-dessous |

L'état 4, à la fin de l'animation :
- `Bytes[0]=0`, animation 0, `+0xB8` (ForceZ) `=0` ;
- `Flags = (Flags & ~0x100) | 0x80` ;
- `+0x54 = Bytes[1]` (restauration de l'index du programme C) ;
- `+0x11C += 0xA00000`, soit 160 px en 16.16 ;
- et un store dans `[0x8013DA20]` (voir F3).

Vérification : `replay/c71_full.py` joue le **vrai code** de A19 et C71 dans l'interpréteur MIPS (harnais `h.py`, héros en slot 0 à `0x80127D30`) contre un modèle Python écrit d'après le désassemblage.
- 6000 vecteurs, 0 écart.
- Couverture 89/89 instructions pour C71 et 3/3 pour A19.
- Onze classes de vecteurs (état 0 à 4, état ≥ 5, contact, torche touchée, autre type, pas d'objet).
- 90 mutants d'un mot de C71 : 89 tués ; le survivant `move a1,a0` → `subu a1,a0,zero` est équivalent. A19 : 2 mutants sur 2 tués.

**F3 [FAIT]. Le store `[0x8013DA20]=1` de l'état 4 est mort.**
- `0x8013DA20` est `g_clearProgramState` (`StaticVariables.cs:12961`). Seuls quatre sites y touchent :
  - le store de l'opcode 0x40 (`0x8003E7CC`) ;
  - le store de C71 (`0x8007B95C`) ;
  - la remise à zéro à l'entrée de `RunScript` (`0x8004226C`) ;
  - la lecture après chaque opcode (`0x800422FC`, remise à zéro en `0x80042310`).
- C71 s'exécute hors de `RunScript`, donc son store est effacé avant toute lecture. **Il ne faut pas le porter.** C'est le détail à documenter au site.
- Source : `scan_global.py`, `dis_destroy.txt` et `bx.py 8004205c-800420a0 80042250-80042290 800422e0-80042330`.

**F4 [FAIT]. DestroyEntity (`0x8003A59C`).**
- Ordre : l'aide `0x80032B90` (butin), statut 4, `+0x228=-1`, libération de `+0x1B8`, effet de bris (octet `+0x1E` de l'en-tête pour l'argument −1).
- Puis, pour finir (`0x8003A61C-0x8003A62C`) : si `+0x28` (le porteur) est non nul, `porteur.+0x2C = 0`.
- Le binaire efface donc le pointeur d'objet porté du héros. La DLL le fait déjà (`WorldProxy.cs:3366`, `ClearCarrierOf` à `:3375`).

**F5 [FAIT]. Le script du côté du binaire.**
- `Script_55_037` (`0x8003E3DC`), l'attente `37 [N]` : le premier appel suspend (il mémorise le pc), puis N appels suspendent, le (N+2)ᵉ avance. C'est exactement le `Wait` de la DLL (`EventProgramRunner.cs:2700`).
- `Script_64_040` (`0x8003E7B8`) : `g_clearProgramState=1` puis `ProgramIndexes[v1]=v2`. La DLL est identique (`:856-868`, avec la même fin de programme qui efface l'état du C).

### B. Le cycle d'une goutte (rejeu complet)

Montage `s1_drop_cycle.py` :
- une goutte de la carte 92, enregistrement 15, c'est-à-dire le placement sans programme scripté, donc natif ;
- vrais A19 et C71, vraie machine d'états de statuts (statut 1, puis A, puis statut 2), vraie physique, vraie animation, vraie passe de coups ;
- sol plat à Z=0, goutte posée à Z=+160 px ;
- seule écriture du harnais : position et Z.

**F6 [MESURÉ]. Chronologie en ticks** (sortie : `out_s1_native.txt`). R est le tick de restauration (état 4 vers 0).

| Tick | Événement |
|---|---|
| R | état 4 vers 0 : Z+160, Collidable remis, animation 0 |
| R+1 | état 0 vers 1, compteur 40 |
| R+2 … R+41 | compteur 39 … 0 |
| R+41 | état 2 : animation 1, Gravity |
| R+65 | atterrissage (`IsOnGround=1` et `CollidedWithEntityZ=1` ensemble) |
| R+66 | état 3 : animation 2, **son 145** |
| R+67 | état 4 : `Collidable` retiré |
| R+87 | `+0xAC` = 1, animation 2 finie |
| R+88 | restauration, **période 88** |

- La chute de 160 px dure 24 ticks. Elle est de 22 ticks sur la tête du héros (appui à Z=32) et de 23 ticks sur une torche posée (appui à Z=15).
- L'animation 2 dure 22 ticks, de R+66 à R+87.

**F7 [MESURÉ]. Sons et effets du cycle.**
- Un seul son : 145, au début de l'animation 2.
- Les `snd(0)` du journal sont les changements d'animation sans son.
- Aucun effet entité émis par la goutte.

**F8 [MESURÉ]. Avec le script** (sortie : `out_s1b_scripted_N30.txt`). Le harnais modélise `37 [30] ; 40 [2,0]` à partir de F5.
- Le script dure N+2 ticks, puis les 88 du natif, soit une période N+90.
- La restauration a bien lieu : `+0x54` vaut 0 pendant le natif et 0xB1 de nouveau à R.
- Sans restauration (`p54=0`), la goutte cycle quand même toutes les 88 ticks, tout le temps natif.

### C. Le contact

**F9 [FAIT, MESURÉ]. Ce que C71 teste à l'état 3, une seule fois (le tick d'entrée).**
- Il lit le support `+0x12C`. Si c'est le héros (slot 0), il prend `héros.+0x2C` (l'objet porté).
- Si le résultat est non nul et que son type (`+0x68`) vaut 0x189 (393, la torche), alors il écrit `torche.+0x10=3` (statut Deactivated) et `torche.+0x274=1`.
- Le type vaut 0x189 pour 393, comme E20 teste 0x187 pour 391.
- 161 vecteurs "torche touchée" sur 6000, tous conformes.
- **La goutte ne lit ni `HitCounter` ni `TouchingEntity`.**

**F10 [MESURÉ]. Alundra porte une torche** (`out_s2_carry.txt`). Montage `s2_drop_on_torch.py` :
- le vrai `MovePlayer` soulève la vraie torche (carte 86, sprite 137) ;
- la goutte est placée au-dessus de la tête du héros ;
- vrais C70, E20, E0, A19 et C71.

d est le tick de l'état 2 de la goutte.

| Tick | Événement |
|---|---|
| d+22 | atterrissage sur la tête du héros (Z=32) |
| d+23 | état 3 de la goutte, support = héros (`0x80127D30`), son 145 |
| d+24 | état 4 de la goutte ; **torche en statut 3, `+0x274=1`** |
| d+25 | torche animation 2, **son 142** |
| d+27 | `+0xAC=1` |
| d+28 | torche détruite (statut 4), effet 10 attaché, pointeur porté du héros à 0 |
| d+29 | torche recyclée, héros en état 0 (debout) |

- Aucune flamme : pas d'entité de type 12 en fin de rejeu.
- Le héros est **debout, mains vides, sans étourdissement**, PV inchangés (10).
- L'ordre torche→goutte dans les slots compte : la torche, slot 1, passe avant la goutte, slot 2, d'où l'animation 2 au tick suivant.

**F11 [MESURÉ]. Alundra sans torche** (`out_s2_nocarry.txt`).
- L'état 3 lit `héros.+0x2C=0` et s'arrête.
- PV inchangés, état inchangé, seul le son 145.

**F12 [MESURÉ]. Une torche posée** (`out_s2_posed.txt`).
- La goutte se pose sur le dessus de la torche (Z=15), support = la torche.
- Torche en statut 3 à d+25 ; son 142 à d+26 ; détruite à d+29, avec l'effet 10 ; recyclée à d+30.
- Pas de flamme.

**F13 [FAIT, MESURÉ]. Une torche lancée ou en flamme ne peut pas être touchée.** Ses drapeaux sont `0x332376` en vol et `0x332346` en statut 3, tous deux sans `0x80`. Elle n'est donc pas dans la liste des appuis possibles. Une torche portée n'y est pas non plus (`+0x28 != 0`) ; c'est pourquoi le support de la goutte est le héros, pas la torche.

**F14 [MESURÉ]. Fenêtre de contact** (`out_s3_hit_window.txt`, `s3_hit_window.py`).
- La goutte éteint la torche portée si son centre tombe à dx de −11 à +12 px et dy de −8 à +8 px du centre du héros (24×17 px).
- En dehors, elle tombe à terre, rien ne se passe.

**F15 [MESURÉ]. Après le contact la goutte traverse le héros.**
- `Collidable` est retiré à l'état 3 mais `Gravity` reste : la goutte finit sa chute de 31,5 à 0 pendant que le splash joue.
- La restauration ajoute 160 px **à ce Z-là** (0), pas au Z du plafond.

**F16 [MESURÉ]. Course avec le bouton Carré** (`out_s4_press_square.txt`, `s2_drop_on_torch.py carry 100 <\tick>`).
- Appui au tick du contact d'état 3 (d+24) : le `MovePlayer` écrit le code de lancer, le C70 de la torche (slot plus bas) lâche la torche avant le C71 de la goutte. La torche est **sauvée** : elle part normalement et donne sa flamme.
- Appui pendant que la torche brûle (d+25 à d+28) : le héros lance l'animation de lancer (état 9, son 26) mais **rien ne part**. La torche en statut 3 n'exécute plus C70, elle est détruite, le héros garde l'animation de lancer à vide.
- Cette fenêtre d'un tick dépend de l'ordre des slots. [INFÉRENCE : la DLL numérote par ordre d'apparition, le binaire par plus petit slot libre. Écart sans enjeu, à ne pas épingler.]

**F17 [FAIT code]. Posture du héros dans la DLL.**
- `CarryRules.CarryGroup` renvoie `IdleState` (sol) ou `FallState` (air) quand `HasObject` est faux (`CarryRules.cs`, fonction `CarryGroup`).
- `ClearCarrierOf` met `CarriedEntity=null` (`WorldProxy.cs:3375`).
- Le résultat attendu est donc le même qu'en F10 : un tick après la destruction, le héros est debout.
- À confirmer par sonde (S-D4).

### D. Le script et la restauration de `+0x54`

**F18 [FAIT code]. Natif contre script.**
- Le programme C est natif seulement si `(ProgramIndexes[2] & 0x7F) == 0` (`EntityScriptProxy.cs:2034`, `EventProgramRunner.cs:305`).
- L'index placé (177, 178…) a le bit 0x80 : il choisit l'entrée `index & 0x7F` de la table C de la carte (`:343-360`).
- Les index de placement sont donc des index de table décalés de 128. Le décodeur `decode_events.py` ne marche qu'avec `& 0x7F`.

**F19 [MESURÉ]. Programmes C des gouttes** (`census_drops.txt`, `classify_progs.py`).

| Type | Nombre | Forme |
|---|---|---|
| Attente simple | 33 | `37 [N]` puis `40 [2,0]`. N de 5 à 240 : 5, 6, 6, 8, 10, 12, 15×3, 17, 19, 20×2, 21, 24, 25×2, 30×4, 31, 35×3, 40×3, 120×2, 150, 180, 240 |
| Conditionné par un drapeau temporaire | 8 | `30 [flag]` : si posé, attendre B ; sinon attendre A, poser le drapeau, passer au natif. Cartes 87 (flags 0x81F4, 0x8208, 0x821C), 88 (0x8190, 0x819A, 0x81A4, 0x81B8), 92 (0x8104) |
| Conditionné par une zone | 14 | carte 158 : `00 ; 3B [x1,x2,y1,y2,z1,z2] ; 04 -8 ; 37 [N] ; 40 [2,0]` |

- Les drapeaux sont temporaires (≥ 0x8000), vidés à chaque changement de carte (`GameState.cs:370`). Le premier cycle d'une carte est donc plus court que les suivants. [INFÉRENCE sur la durée.]
- Pour les 14 gouttes de la 158 : le script boucle (opcode 0x3B "héros dans la boîte", puis saut en arrière) tant que le héros est hors de la boîte. Elles ne tombent que si Alundra est dans la zone.
- Total : 55 placements scriptés plus 1 gabarit natif (92, enregistrement 15) = 56 placements activés.

**F20 [FAIT]. La restauration rend le script, pas la chute.**
- `A19` mémorise l'index scripté ; l'état 4 de C71 le remet dans `ProgramIndexes[2]`.
- Le prochain tick exécute `RunScript`. L'état du programme C a été effacé par son propre `40` (fin d'appel), donc il **repart de son premier opcode**.
- Sans cela la goutte tournerait en natif pur à 88 ticks (F8).

**F21 [FAIT code]. Pourquoi les gouttes restent immobiles au HEAD.**
- Leur script attend N ticks puis `40 [2,0]` met `ProgramIndexes[2]` à 0.
- Le registre natif n'a ni `(A,19)` ni `(C,71)` : `RunSpriteEvent` ne trouve rien, c'est une non-opération comptée.
- Les drapeaux d'en-tête `0x032080` n'ont pas Gravity : la goutte garde son Z de placement.
- Au HEAD, A19 non plus ne s'exécute pas (+0x275 jamais écrit).

### E. Les données

**F22 [FAIT]. Le sprite de carte 135 = type 391 (0x187).** Sources : `sprite-records.json`, `balance.json` entrée 391.
- Drapeaux 0x032080 : MoreFlags 0x80 (Collidable), CanPickup 0x20 (genre 0 : non soulevable), type d'ombre 3.
- Corps 4×3×8, décalages (−2, −1, 0). `BreakEffect` 0, `Contents` 0.
- Équilibre : PV 0, onze réponses à 128, **`Attacks` nul** ; aucun drapeau `HitsClassA/B`.
- Animations : 0 et 1 sont des boucles à un cadre ; la 2 est un "Once" de 8 clés de 0,06 s (jusqu'à 0,42 s), Sfx 145 (`bank135_anim2_down.anim2d`).
- Prefab `Goutte d’eau.entity` : un `CollisionComponent`, une `AnimatedSpriteComponent`, pas de composant de script.

**F23 [MESURÉ]. Recensement** (`census_drops.txt`, `near.txt`, `near.py`).
- Gouttes par carte : 87 : 6 ; 88 : 13 ; 90 : 7 ; 92 : 9 (8 + le gabarit) ; 94 : 7 ; 158 : 14.
- Torches des mêmes cartes : 87 : 8 ; 88 : 3 ; 90 : 4 ; 92 : 16 ; 94 : 8 ; 158 : 0.
- Le rapport précédent donnait "61 programmes scriptés" ; je compte 55. Le 61 n'est pas reproduit.
- Hauteurs de placement : 8 à 36 unités de 8 px (`census_drops.txt`). Les écarts avec le sol vont de 136 à 224 px (`drop_heights.txt`, centre de cellule). Seuls les cycles suivants valent 160 px au sol.

**F24 [FAIT]. La carte 92 : le gabarit et les quatre gouttes du programme B[1].** Le programme B[1] (`dump_prog.py 92 B1`) fait : attendre 240, 240 puis 120 (606 ticks), puis quatre `0x8A [138, 15, x, y, 240px]` aux points (756, 168), (804, 168), (852, 168), (900, 168).
- L'enregistrement 15 est un gabarit sans programme C (natif pur), de direction `0x80`. Le bit 0x40 est absent, donc non né au chargement (`EntitySpawnFactory.cs:92`).
- Ces quatre points sont **exactement** ceux des torches 43 à 46 (même x, y ; torches à Z=80, goutte née à Z=240).
- `overlap.py` : ce sont les **seules** gouttes qui tombent sur une torche à sa position d'origine. Sur les cartes 87, 88, 90, 94 aucune goutte ne chevauche une torche posée.
- Les gouttes de ces quatre points partent en natif pur et cyclent toutes les 88 ticks, tant que la carte est chargée.

**F25 [MESURÉ]. Accès par sauvegarde** (`census_saves.py`, `census_saves.txt`). Sur les 122 `.sav` de `tools/Saves/` :
- Aucune n'arrive dans une carte de gouttes (87, 88, 90, 92, 94, 158).
- `160-magyscar.sav` arrive en carte 86 (torches, 0 goutte) ; `162-magyscar-bug.sav` en 93 (torches) ; 161 en 91 et 163 en 95 (sans torche).
- Aucun préréglage `TestSaves` n'arrive en 92. Le préréglage `torche-m92` prévu par g2c2 (D-G2C2-7) reste à écrire.
- La couloir bas de la 92 (gouttes 25 à 32, torches 21 à 24 posées à (324..372, 840..872)) est marchable à la hauteur 2, p. ex. la cellule (16, 51), [INFÉRENCE : candidate d'arrivée `(16, 51, 2)`, à vérifier]. Zone de naissance des huit gouttes : tuiles x 0..27, y 44..59.

### F. La DLL au HEAD

**F26 [FAIT code].** Les briques existent déjà.
- `NativeSpriteRegistry.Register(slot, code, types, handler)` ; les inscriptions de production sont dans `CreateForProduction` (12 appels, dont `BallAi.Register` : f6b est faite).
- Modèle d'une classe de ce genre : `PillarAi.cs` (une fonction pure `Step`, un `Tick`, `Keys`, `Register`).
- `PickupAi` montre qu'un A natif s'exécute au premier pick (statut 1 vers 2).
- `AIValues[1]` = `+0x282`, `Bytes[0..1]` = `+0x274/+0x275`.
- Le hero se reconnaît par `ReferenceEquals(item.RidingEntity, hero.LogicContextEntity)` (`PlayerManager.cs:1599`).
- `EventProgramRunner.RunPickedEvent` rejoue le script dès que `ProgramIndexes[2] & 0x7F != 0`.

**F27 [FAIT code]. Ordre d'un tick de la DLL** (`WorldProxy.cs:2895`).
- Ordre : `MovePlayer`, programmes dans l'ordre des slots, puis la passe des compteurs (qui efface `RidingEntity`), puis les listes (qui le recalculent), puis l'horloge d'animation, la physique et la passe de coups.
- C'est l'ordre de l'oracle : le support se pose au tick d'atterrissage + 1 et C71 le lit au tick suivant. Le rejeu F10 l'a mesuré.
- `UpdateRidingEntities` ne travaille que sur la liste des collidables (Collidable, sans le bit d'animation `NoEntityCollision`, sans `PlatformEntity`). La goutte doit donc y figurer avec Gravity au moment du contact.

**F28 [FAIT code]. `PushLogicalPositionToRoot`.**
- Un écrit direct de `PosZ` sur une entité à contrôleur n'est repris sur la racine qu'après `PushLogicalPositionToRoot()` (`EntityScriptProxy.cs:2622`, usage dans `EventProgramRunner.cs:2575`).
- La téléportation de 160 px de l'état 4 doit le prévoir. À sonder (S-D6).

---

## 2. Corrections aux rapports précédents

1. **"Sans cette restauration une goutte ne tombe qu'une fois" (critique, §3.4) : faux.** Elle cycle à 88 ticks en natif pur (F8). La restauration rend le délai du script et, pour la 158, la condition de zone.
2. **"61 programmes scriptés" : je compte 55** (F23).
3. **F12 de g2c2 ("Collidable retiré" en état 2) est déjà corrigé par la critique** : c'est à l'état 3. Je confirme avec les vecteurs : le test de la torche n'a lieu qu'à ce tick-là.
4. **Une goutte "sur une torche" : il faut préciser.** Seul un appui sur une torche posée (non soulevée) ou sur le héros porteur compte (F9, F13). Une torche lancée n'est jamais touchée.
5. **Le store de `g_clearProgramState` par C71 est mort** (F3) : à ne pas porter, à documenter.
6. **Ajout à D-G2C2-6 / O-G2C3-1 :** les quatre gouttes de B[1] (F24) ne figuraient pas dans les rapports. Elles font de g2c3 une tranche qui change le jeu de la 92 dès l'arrivée.
7. **g2c2 doit rester fidèle à son plan sur E20** : si g2c3 s'exécute avant que `(E, 20)` soit inscrit, une torche forcée en statut 3 reste figée. L'ordre g2c2 puis g2c3 est donc une vraie dépendance de code, pas un choix.

---

## 3. Ce que la tranche doit faire (périmètre probable)

**Fichiers.**
- `Alundra/Ai/Objects/WaterDropAi.cs` (neuf), sur le modèle de `PillarAi` :
  - constantes : `SpriteType=391`, codes 19 et 71, attente 40, montée `160<<16` ;
  - `Keys = {(A,19), (C,71)}` ;
  - `Register(registry)` : A19 pour {391}, C71 pour {391} ;
  - `Load(entity)` : `Bytes[1]=(byte)ProgramIndexes[2]` ;
  - une fonction pure `Step(...)` : état, compteur, contacts, animation finie, drapeaux, Z, index mémorisé, et la décision "éteindre la cible" ;
  - `Tick(entity, context, state)` : lit et écrit les champs de la proxy, détecte le héros par `RidingEntity`, lit `CarriedEntity?.GameplayProxy`, compare `SpriteType` à `TorchAi.SpriteType` ;
  - `Extinguish(target)` : `Status=Deactivated`, `Bytes[0]=1`.
- `Alundra/Ai/NativeSpriteRegistry.cs` : l'appel `WaterDropAi.Register(registry)` et la doc de la classe.
- Aucun changement attendu dans `EventProgramRunner.cs`, `LiftableAi.cs` ou `PlayerManager.cs` (la goutte n'est pas soulevable).
- Facultatif : `Alundra/SaveGames/TestSaves.cs` + `docs/test-saves.md` (un préréglage), à ne pas modifier en même temps que b2, c2b, A362, g2c2.
- Annexe `docs/plan-e20-annexe/e20-g2c3/` (cette lecture, `c71_full.py`, goldens), un ADR neuf (numéro libre recompté), le plan (ligne O-G2C3-1 de §2, ligne d'enveloppe).

**Dépendances exactes à g2c2.**
1. `TorchAi.SpriteType` (393) pour le test de type, à ne pas recopier.
2. `(E,20)` inscrit en production pour {393} : sinon une torche forcée en statut 3 reste figée (dérive de F2 et F21). Un test de registre doit le dire.
3. L'octet `Bytes[0]` d'E20 comme marque "sans flamme" : g2c3 écrit le même octet, valeur 1 (mutant "`+0x274` ignoré" de g2c2).
4. 393 dans `ProductionWiredLiftableTypes` (phase C de g2c2) pour les essais torche portée.
5. Le préréglage `torche-m92` pour la recette.
6. L'écart documenté de D-G2C2-6 ("les torches ne s'éteignent pas sous une goutte") se referme ici : ADR, plan.
7. Pas de dépendance à c2b : la goutte ne blesse pas.

**Découpage proposé** (par analogie avec g2c1 et g2c2, un `executor` par tâche).
- **T0.** Verser cette lecture et rejouer ses sorties identiques ; sondes de la DLL (ci-dessous) ; liste prédite des tests déplacés ; case du préréglage.
- **T1.** Tests d'abord : `Step` contre les goldens (11 classes), A19, registre (r1), garde des données (`ProgramLoad==19` et `ProgramTick==71` exactement {391}) ; puis le code.
- **T2.** Tests de monde rouges avant les inscriptions :
  - le cycle sur une goutte native (chronologie F6) ;
  - la période N+90 d'une goutte scriptée (F8) et la restauration de `ProgramIndexes[2]` ;
  - le contact : torche portée (F10), torche posée (F12), héros sans torche (F11), objet porté qui n'est pas une torche ;
  - la fenêtre (F14), partielle ;
  - une goutte de la 158 qui ne tombe que dans sa zone ;
  - un arc de la 92 : les quatre gouttes de B[1] éteignent les torches 43 à 46 environ 650 ticks après l'arrivée.
- **T3.** Mutants (liste en §5), suites Release puis Debug, ADR.
- Tâches finales : deux verifiers frais, plus la recette de l'auteur.

**Sondes DLL à prévoir en T0.**
- **S-D1.** Une goutte née par `0x8A` (92 B[1]) : A19 s'exécute au premier pick, `Bytes[1]` vaut 0.
- **S-D2.** La chronologie F6 pour une goutte native sur sol plat : ticks de chaque état, durée de l'animation 2 en ticks de la DLL (0,42 s exportés, 22 ticks mesurés au binaire), son 145.
- **S-D3.** `IsOnGround` et `CollidedWithEntityZ` valent 0 en chute libre, 1 à l'atterrissage ; quand `RidingEntity` du héros apparaît par rapport au tick de contact (F27).
- **S-D4.** Posture du héros : debout un tick après la destruction de la torche portée (F10, F17), PV inchangés.
- **S-D5.** Une torche posée touchée : mêmes ticks que F12, sans flamme.
- **S-D6.** `PosZ += 160 px` : la racine et le contrôleur suivent-ils dans le même tick ? (F28)
- **S-D7.** Restauration : le script repart de son premier opcode (attente N+2) et non d'un état conservé.

---

## 4. Tests existants qui bougeraient

Cinq sommes de clés lisent le registre de production, à relever d'un `WaterDropAi.Keys.Count` (2) :
1. `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58`
2. `Alundra.Tests/Ai/Objects/LiftableAiRegistrationTests.cs:78`
3. `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs:240`
4. `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:185`
5. `Alundra.Tests/Ai/Projectiles/BallFlamesTests.cs:239` (ajoutée par f6b ; g2c2 n'en avait que quatre)

À mesurer par un câblage temporaire en T0 (liste prédite, arrêt au-delà de 10 tests) :
- **Les tests de g2c2 qui font tourner la 92 plus de 606 ticks** (w3, w5, w8 selon leur durée) : les quatre gouttes de B[1] éteignent alors les torches 43 à 46 et peuvent changer ce qu'ils observent. [INFÉRENCE : en pratique ces tests lancent leur torche dans les 100 premiers ticks.]
- `Alundra.Tests/Ai/Objects/C4WorldTests.cs`, témoins M11 des cartes 86, 87 et 93 (`:632-633`) : les gouttes de la 87 sont dans la zone tuiles x 35..51, y 43..59, loin des arrivées du héros (lignes 19 à 35). Elles ne devraient pas naître. [INFÉRENCE]
- `TestSavesTests` (liste des préréglages) seulement si un préréglage s'ajoute.

Aucun test existant ne mentionne le type 391 ni les cartes 88, 90, 92, 94 ou 158. `LiftableBreakProductionTests` charge la 91 (sans goutte) ; `PickupAiDropWorldTests` et `BackdropStageDefinitionTests` parlent de Fairy cave 159/160, pas de la 158.

---

## 5. Oracles et clés possibles

**Oracles.**
- `replay/c71_full.py` (F2) et `replay/c71_goldens.json` (22 vecteurs sur 11 classes, SHA-256 `40071ab5e80cbf4dae6bf6d33bcf067958abb78cf918f0847f312c9ffac6214b`). Les goldens sont en mots bruts d'entité, à convertir en champs de proxy.
- Les chronologies pinnées au tick près : `out_s1_native.txt`, `out_s1b_scripted_N30.txt`, `out_s2_*.txt`, `out_s3_hit_window.txt`, `out_s4_press_square.txt`. La DLL peut s'écarter d'un tick (latence D-E19-13) ; les écarts sont à nommer en T2.

**Clés.** `(A,19)` et `(C,71)` pour exactement {391} ; garde des données : `ProgramLoad==19` et `ProgramTick==71` pour exactement {391} (modèle `LiftableAiTests.cs:130`) ; `(E,20)` présent pour 393 (dépendance).

**Mutants à prévoir** (au moins 16, chacun tué par un test nommé) :
- attente 40 → 41 ou 39 ;
- état 2 qui ignore `CollidedWithEntityZ` (la goutte ne s'arrête pas sur la tête du héros) ;
- état 3 qui oublie de retirer `Collidable` ;
- type de torche 393 → autre ;
- indirection du héros retirée (support = héros sans lire l'objet porté) ;
- contrôle "objet porté nul" retiré ;
- `Extinguish` sans `Bytes[0]=1` (une flamme naît) ;
- `Extinguish` sans le statut 3 (la torche brûle toujours) ;
- montée 160 px → 128 ou 161 ;
- `ForceZ` non remis à zéro à la restauration ;
- `Gravity` non retiré à la restauration ;
- `Collidable` non remis à la restauration ;
- `ProgramIndexes[2]` non restauré ;
- restauration depuis le mauvais octet (`Bytes[0]` au lieu de `Bytes[1]`) ;
- A19 absent ;
- état ≥ 5 traité comme l'état 0.

---

## 6. Risques

1. **Appui d'une goutte sur le héros ou une torche dans la DLL.** `RidingEntity` n'est recalculé qu'aux listes de chaque tick et exige que le héros soit dans les collidables (F27). Si le hero n'y figure pas dans une pose (bit `NoEntityCollision` d'animation), la goutte tombe à travers. À sonder avec S-D3 sur une pose portée.
2. **Fidélité de la chute.** Le binaire donne 24 ticks pour 160 px ; la DLL a sa propre loi de force (e0k2). L'écart éventuel est à nommer, pas à corriger.
3. **`PosZ += 160 px` sans reprise de la racine** (F28).
4. **Durée du splash.** 22 ticks au binaire, 0,42 à 0,48 s dans le prefab. L'écart est à mesurer en T0 (S-D2) ; le test de la chaîne de destruction de la torche en dépend d'un tick.
5. **Les quatre gouttes de la 92 changent le jeu dès l'arrivée** : les torches 43 à 46 disparaissent à environ 650 ticks si le joueur ne les a pas prises. C'est fidèle à l'original (F24) mais le joueur le verra : question Q1.
6. **Ordre des slots dans la fenêtre de course du bouton Carré** (F16) : la DLL peut sauver ou perdre la torche différemment du binaire ; ne pas épingler.
7. **Les 14 gouttes de la 158** (sans torche, hors chaîne) changent une carte qui n'a aucun test et aucun accès par sauvegarde.
8. **g2c2 non encore exécutée.** Tous les points de dépendance du §3 supposent son plan tel qu'écrit (révision 2, READY). Si TorchAi change de forme (nom de la marque `Bytes[0]`), g3 suit.
9. **`TestSaves.cs` est partagé** avec b2, c2b, A362 et g2c2 : jamais en même temps.

---

## 7. Questions à l'auteur (en termes de jeu, défaut "comme l'original")

**Q-G2C3-1 — Les quatre gouttes de la salle 92.** Environ dix secondes après qu'Alundra est entrée dans la salle 92, quatre gouttes se mettent à tomber au-dessus de quatre torches posées sur la rangée du haut, et les éteignent (elles disparaissent) si le joueur ne les a pas prises avant. Défaut : **comme l'original, les torches disparaissent**. Sinon (A) ne pas faire naître ces quatre gouttes, ce qui ôte le piège et laisse les torches en sécurité pour toujours ; coût : un écart au jeu que l'auteur devra documenter, et une question d'équilibre de l'énigme.

**Q-G2C3-2 — Alundra porte une torche et une goutte lui tombe sur la tête.** La torche s'éteint dans ses mains en une fraction de seconde (son d'extinction, petit effet de bris) et disparaît ; Alundra reste debout, les mains vides, sans dégât ni étourdissement. Défaut : **comme l'original**. Il n'y a pas d'autre option naturelle.

**Q-G2C3-3 — Alundra sans torche.** Une goutte lui tombe dessus : seul le bruit d'éclaboussure, ni dégât ni recul. Défaut : **comme l'original**.

**Q-G2C3-4 — La grotte des fées (carte 158).** Ses 14 gouttes, sans aucune torche, tombent dès qu'Alundra est dans leur zone (bruit d'éclaboussure, ambiance). Défaut : **les porter**. Sinon (B) les laisser pendues et silencieuses ; coût : la grotte reste figée, et l'écart ressemble à un défaut.

**Q-G2C3-5 — Où l'essayer.** Aucune sauvegarde n'arrive dans une carte de gouttes. Défaut : **réutiliser `torche-m92`** de g2c2 si son arrivée est à moins de quelques pas du couloir bas ; sinon un second préréglage `gouttes-m92`, candidat à vérifier `(16, 51, 2)`. Autre option : aucune recette sur la 158, qui reste sans point d'entrée.

---

## 8. Scripts et sorties

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/g2c3/`

**Scripts** (tous à lancer avec `python -B -I -X utf8`, depuis le dossier ou `replay/`) :
- `s1_drop_cycle.py [n [N]]` : le cycle d'une goutte (sans N : natif ; avec N : modèle du script `37 [N] ; 40 [2,0]`) ;
- `s2_drop_on_torch.py carry|nocarry|posed [n [tick-Carré]]` : le contact ;
- `s3_hit_window.py` : la fenêtre XY ;
- `replay/c71_full.py [N] [mut] [gold]` : A19 et C71 contre le code réel, mutants, goldens ;
- `census_drops.py`, `classify_progs.py`, `near.py`, `overlap.py`, `drop_heights.py`, `census_saves.py` : recensements ;
- `scan_global.py [adresse]` : qui accède à un mot global ;
- `bx.py`, `dump_map.py`, `dump_prog.py`, `census_g2c2.py`, `census_scripted.py` : outils copiés de g2c2 ;
- `replay/` : copie de l'oracle (`work/`, `c4_scen.py`, `f2_drop.py`, `f2b_drop_sweep.py`, `f1_flame.py`).

**Sorties** :
- `c71_pseudocode.txt`, `dis_a19.txt`, `dis_c71.txt`, `dis_destroy.txt` ;
- `out_c71_full.txt`, `replay/c71_goldens.json` ;
- `out_s1_native.txt`, `out_s1b_scripted_N30.txt`, `out_s2_carry.txt`, `out_s2_nocarry.txt`, `out_s2_posed.txt`, `out_s3_hit_window.txt`, `out_s4_press_square.txt` ;
- `census_drops.txt`, `near.txt`, `drop_heights.txt`, `census_saves.txt` ;
- `EntityScriptProxy.HEAD.cs`, `EntitySupport.HEAD.cs`, `EventProgramRunner.HEAD.cs` (copies du HEAD lues) ;
- `SHA1SUMS.txt`.

**Limites de mes rejeux.**
- Le script d'une goutte n'est pas exécuté par la machine (`RunScript` y est un bouchon) ; son attente `37 [N] ; 40 [2,0]` est modélisée d'après le binaire (F5) et la DLL.
- La physique de la machine est celle d'un sol plat ; les chutes réelles dépendent du relief de chaque carte.
- La DLL n'a pas été exécutée : toutes les durées de la DLL (animation, chute, latence D-E19-13) sont des sondes de T0, pas des faits.