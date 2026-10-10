> Report returned by one agent of the targeted read-only discovery for E20.f1 (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

> Note (2026-10-10) : le `FreeNow` immédiat du projet ci-dessous est remplacé par `FreeAtTick` (libération en file à son tick, gardée
> par la génération) et la recréation teste aussi la génération : plan §1.9, E20.f1b révisions 1 et 2, ADR-0059.

# f1-check: adversarial check of depth-key, effect-model and oracle-ext, plus a draft revision of E20.f1

## Things that change the plan

1. **Blocker 1 is confirmed.** The aura is created when the handle is null or when the slot's `+0x7D` is **non-zero** (FACT, `0x8002F570`-`0x8002F58C`). The same inversion is in the annex `f-weapons.md:122`, which needs an erratum.
   - Consequence: a charge held long enough renews the aura every 97 ticks. Effect 3, anim 0 is `END 24 96 96 97` in `anim_table.tsv`. The oracle hold run gives creations at t33, t130 and t227.
2. **Blocker 3: synchronous creation by the hero (effect-model's Model S) is the right model, but its proposal has two defects.**
   - (a) The `FirstUpdateTick` stamp is local to the frame. `BeginSource` ticks restart at 0 each frame, and `RunFrame` loops 0..N-1 (`AlundraEffectStage.cs:196-216`). So `RunFrame` must clear the stamp at its end, including with 0 ticks. Otherwise a slot stamped 1 skips tick 0 of the next frame.
   - (b) "Exact at one tick per frame" is false for slot indices. In the binary, map events create their effects before `MovePlayer` (`0x8002E100` comes before `0x8002E108`). In the DLL those creations stay queued (rank 0), so the hero takes the lower slot. This is a documented deviation, even at N=1.
3. **Blocker 4 does not need a large port.**
   - The hero key for f1's cases is `((PosY + (IDSV<<16)) & 0xFFFF0000) + (short)(PosZ>>16)`.
   - I added the binary's sort `0x800399B8` to the oracle and ran it after each tick. On C2-steel the hero's `+0x1BC` equals this formula on **150/150 ticks**. During the charge it is `0x00690000` (IDSV 5, anim 0).
   - **f1 does not need to be split on the depth-key ground.**
4. **New visual blocker, confirmed by code reading: the DLL would draw the aura under the hero sprite.**
   - The hero sprite sorts on `100*((PosY>>16)-(PosZ>>16))`. That comes from TopDownYUp under `RenderProjectionComponent`, with `DeriveRenderPosition = (x, -(y-z), 0)` and `SortPrecision` 100.
   - The aura sorts on `100*ay`, where ay is 16 px higher. In the same bucket it therefore comes about 1600 earlier.
   - The binary draws it after the hero (key + 1).
   - This is ADR-0049 deviation (3) applied to an attached effect. The fix is a DLL-only rule for anchored slots in `AlundraEffectQuadBuilder`, plus an ADR-0049 amendment. It is a separable task with its own stop.
5. **Z conventions.**
   - Effects use the binary's Z (`PosZ + 1`), because the `+1` moves the drawn row (ADR-0049). So DLL slot Z equals oracle Z exactly.
   - The projectile stub keeps the DLL convention, so its assertion is `oracle Z = DLL Z + 1`.
6. **The oracle's per-slot fields are taken before the first update**, because the oracle never runs `0x8003C410`. DLL tests must therefore snapshot the slot after the hero's logic tick and before `RunFrame`.
7. **Stale handle.** The free path at `0x8002F730`-`0x8002F744` frees whatever occupies the old slot. That is a dangling-pointer defect of the original. Your rule is to correct proven defects, but to ask when the fix changes what the player sees, so this becomes open point O-E20-15 with a recommendation.
8. `f-check.md:203` ("`+0x7D` cannot be read from `AlundraEffectSlot`") no longer applies: `Destroying` has a public getter (`AlundraEffectReservoir.cs:93`).
9. Do not use `ZUpperBound //1bc` (`AlundraEntityScriptProxy.cs:407`). It is never written anywhere; it is only copied at `:2477`.

## Verdicts

### depth-key

| Claim | Verdict | Evidence |
|---|---|---|
| F `0x800397AC`, driver L1/L2/L3 `0x800399B8`; isolated-hero formula | CONFIRMED | Re-disassembled: `0x800397EC-0x80039804` (s2 = Y + IDSV<<16); L3 `lh 0x11E` and mask at `0x80039A94-0x80039AA4`. Oracle `keyprobe.py`: key equals the formula on 150/150 ticks; IDSV values seen are {0, 4, 5, 8, 12, 15}. |
| Carrier early-out and neighbour loop (ZTop <, XY overlap, recursive max) | CONFIRMED | `0x8003981C-0x80039978` |
| Sort runs in the tail every tick, gate open or not; effects after | CONFIRMED | `0x8003B414`, `jal 0x800399B8` at `0x8003B478`; `0x8002E108` then `0x8002E110` |
| Mode-1 key = `anchor.1BC + slot.+0x60`; common part re-applies it | CONFIRMED | `0x8003C374-0x8003C384`, `0x8003C3E0-0x8003C3F0` |
| Draw-list copy of the key | CONFIRMED | `0x8003B4AC-0x8003B4B4` writes the key to `+0x1A4`. Effects use the same layout (`0x8003C490-0x8003C4B8`). |
| "`0x8002DA04` is a signed **ascending** quicksort" | REFUTED in detail | The left scan skips keys greater than the pivot (`0x8002DA68-0x8002DA8C`), so the list is sorted in **descending** order. The draw order comes out ascending only because, within an OT slot, the last item inserted is drawn first (ADR-0049, "image 0 drawn last"). The conclusion (aura drawn after the hero) still holds, as an INFERENCE resting on ADR-0049. Caller: `0x8002E130`. |
| DLL elevation uses only `DepthKey>>20` | CONFIRMED | `AlundraEffectQuadBuilder.cs:72`; `WallPlacementOverlay.ComputeEntityElevation` |
| Aura sorts before (under) the hero in the DLL | CONFIRMED by code reading; unit pin still needed | Hero sprite: `Alundra.entity` TopDownYUp, sort anchor (0,0), `RenderProjectionComponent`; `SimulationSpacePolicy.cs:159-176`; `DepthSortable2DComponent.cs:107,13`; `AnimatedSpriteComponent.cs:621-630` (parts only add to local and stable). Effect: `coordinate = 100*ay`. |
| Hero PosZ in the DLL is the binary's minus 1 | CONFIRMED | ADR-0026; oracle rows z=1 at rest, where DLL z=0 |
| No per-tick hero snapshot needed | CONFIRMED | `MoveFollower` overwrites X/Y/Z/key on every update (`AlundraEffectReservoir.cs:574-611`) |
| "No stored ZUpperBound/RenderSortKey; the names exist only in the decompilation" | REFUTED in detail | The fields exist in the DLL (`AlundraEntityScriptProxy.cs:407-408`) but are never written |
| Hero MoreFlags `0x8C`, `+0x28` null, AnimFlags&0x80 | UNVERIFIABLE (not checked) | Not needed: the neighbour raise is out of scope for f1 |
| IDSV source is frame 0 (`BuildIdsvByAnimDirection`); hero table set at `AlundraWorldProxy.cs:1788` | CONFIRMED | Charge anim 0 has constant IDSV 5 in the oracle; anim `0x18` varies (0/8/12/15) |

### effect-model

| Claim | Verdict | Evidence |
|---|---|---|
| Aura condition inverted | CONFIRMED | `0x8002F578 beqz` (handle) leads to the create; `0x8002F588 beqz` (+0x7D) skips the create |
| Handle stored even when the result is null | CONFIRMED | `0x8002F5BC` |
| Spark: null result skips sound and draws; draw math; state = r2; Z not jittered | CONFIRMED | `0x8002F618-0x8002F71C`. Recomputed: `B806E07F/24D2C78A`, jx 86057, jy -93365, x 8274740, y 4686300. |
| `ForceX`/`ForceY` = hero `+0xC4`/`+0xC8` | CONFIRMED | Field order at `AlundraEntityScriptProxy.cs:299-303` (`PreviousAdjustedForceX //cc` follows) |
| Free path frees without any test | CONFIRMED | `0x8002F730-0x8002F744`. Reachable in the story: UNVERIFIABLE. |
| Renewal every 97 ticks | CONFIRMED | `anim_table.tsv`; oracle A1/A2 (real create code) and A5 (lifetime model) |
| Queued creation cannot work; synchronous creation needed | CONFIRMED | `RunFrame` runs after all ticks (`AlundraWorldProxy.cs:2449`); ADR-0046:22 assumes "scripts never read the reservoir", which `MovePlayer` does |
| Model S is exact at N=1 | PARTLY REFUTED | Slot index differs when a rank-0 map-event create shares the tick. Draw order (hold-spark draws in `MovePlayer`, hit-spark draws in `RunFrame`, binary has `MovePlayer` before the hit pass) is CONFIRMED exact. |
| `FirstUpdateTick` stamp as specified | DEFECT | Frame-local stamp never cleared; it must be cleared at the end of `RunFrame` |
| Renewal gap at N >= 2 | CONFIRMED by reasoning | A freed slot keeps `Destroying = true` until `CreateCore` reuses it (`:401`, `:459`). Only the hero creates synchronously, and its aura check runs first, so the aura is recreated at the next frame's first tick: one displayed frame without aura. |
| Host accessor shape (`IEntityWorldContext.cs:182`, proxy `:557`) | CONFIRMED | |
| No engine change | CONFIRMED | Builder and anchor are DLL code |

### oracle-ext

| Claim | Verdict | Evidence |
|---|---|---|
| Effect stubs removed; real alloc/init/create run; observers only journal | CONFIRMED | `ext.py:77-111`; the original stub is `tools/machine.py:165-174` |
| Deterministic; same rows as the discovery | CONFIRMED | Re-run in my copy (`oe/`): 17/17 output files and `summary.txt` are byte-identical |
| Burst tuples and spark words | CONFIRMED independently | `spark.py`: burst rows i = 0, 1, 4, 8, 15 equal its table |
| No per-frame RNG advance in the oracle | CONFIRMED | `world.py:187` PASSES; the advance at `0x8002BD0C-0x8002BD34` stores in a delay slot, so it always runs |
| `fc_in = tick - 1` while `MovePlayer` runs | CONFIRMED | The DLL already has this order: `MovePlayer` before `RunCountersPass` (`AlundraWorldProxy.cs:2671-2700`) |
| A5 renewal | MODEL, labelled as such | Acceptable: the DLL reservoir's `Animate` is the proven port (ADR-0045, G1/G3) |
| Copyable as is | NO, two paths to fix | `probe_ext.py:303` (absolute `EFFECTS_GLOBAL`) and `:63` (`DISCOVERY`, relative to the scratch layout) |

## Draft revision of section 1.9 (French, ready to paste)

```markdown
### 1.9 E20.f1 — Charge et relâchement ⏳

**Révision 1 (2026-10-10).** Relecture : **REVISE**, cinq points, corrigés ici : la condition de l'aura était inversée ; l'oracle
ne journalisait ni le réservoir ni le générateur ; le moment de création des effets n'était pas choisi ; l'aura n'avait ni ancre ni
clé de profondeur ; E20.f0 n'était pas un prérequis. Découverte : `scratchpad/disc-f1/` (`depth-key`, `effect-model`, `oracle-ext`,
contre-vérifiés par `f1-check`). Erratum : `f-weapons.md` ligne 122 inverse le test de `+0x7D` ; l'arrêt de `f-check.md` ligne 203
est sans objet (`AlundraEffectSlot.Destroying` est le `+0x7D`, lisible).

**Prérequis.** E20.f0 CONFIRMED par son verifier frais (les tests d'arc utilisent `Give` et `Equip`) ; pour la recette, la sauvegarde
`steel-m14` générée par l'auteur hors de l'app (recette d'E20.f0).

**But.** Le héros charge son arme en tenant Carré, voit l'aura dessinée au-dessus de son sprite, entend les sons de charge, et relâche
une attaque chargée exactement comme le binaire : épées et fléau d'acier frappent avec leur animation chargée par la passe de coups ;
l'arc de William et les baguettes arment leur tir, qui reste un bouchon compté jusqu'à E20.f3 (l'apparition vient d'E20.e0a). Les
branches gelée et verrouillée de `MovePlayer` effacent la charge et le tir en attente, comme le binaire.

**Faits porteurs** (`f-weapons.md` §1 à §3 avec l'erratum ci-dessus, `f-check.md` lignes 5 à 19, 33, 35, corrections C2 à C5, C13 ;
`scratchpad/disc-f1/`) :
- [binaire] `UpdatePlayerWeaponEffect` (`0x8002F49C`, appelé en `0x80031D68`, avant `PlayerTryAttack`, `0x80031EA8`/`0x80031F74`) :
  compteur `0x80126FFC`, +1 plafonné à 60 quand `(Hold | Released) & 0x80`, l'octet 0 de la fiche d'arme non nul et le drapeau b0 de
  l'animation courante, sinon 0. Compteur < 11 : si la poignée `0x80126FF8` est non nulle, `+0x68` de son emplacement à 0 puis poignée
  à 0, sans autre test (`0x8002F730`-`0x8002F744`). Compteur ≥ 11, à chaque tick : l'effet 3 attaché au héros est créé si la poignée
  est nulle **ou si le `+0x7D` de son emplacement est non nul** (`0x8002F570`-`0x8002F58C`), par `0x8003BE74(0, 3, 0, héros, 1, 0, 0,
  0x100000)`, et la poignée reçoit le retour même nul (`0x8002F5BC`) ; puis, seulement si `+0x1C & 3 == 0` (valeur d'avant la passe
  des compteurs) : son `0x2A` si le compteur est < 60 ; à 60, l'étincelle (effet `0x10` en X, Y, Z + `0x100000`) ; nulle : rien
  d'autre (`0x8002F61C`) ; sinon son `0x1AD` si `+0x1C & 7 == 0`, sinon `0x1AC`, puis deux tirages `r1`, `r2` (état = `r2`),
  `jx = hi(r1 × 0x60001) − 0x30000`, `jy = hi(r2 × 0x40001) − 0x20000` (`multu`), X += 20·jx, Y += 20·jy, forces = forces du héros
  (`+0xC4`/`+0xC8`, `ForceX`/`ForceY`) − (jx, jy).
- [binaire] Renouvellement : l'animation 0 de l'effet 3 fait 96 ticks et finit par `Destroy` (`anim_table.tsv` : `alundra 3 0 END
  24 96 96 97`) ; la mise à jour c + 96 pose `+0x7D`, `MovePlayer` du tick c + 97 recrée l'aura (il précède la passe des effets,
  `0x8002E108` puis `0x8002E110`), la passe du même tick libère l'ancienne. Charge tenue : une aura tous les 97 ticks.
- [binaire] `PlayerTryAttack` (`0x8002EAF4`) : inchangé (gerbe de 16 effets `0x10`, tables `0x80023654`/`0x80023694`, Z +
  `0x100000`, aucun tirage, son `0x2B`, pointeur, `+0x88` chargée) ; un effet nul de la gerbe saute ses seules écritures, le son
  reste (`0x8002EBBC`). Au tick de relâchement, une étincelle puis la gerbe peuvent naître (C10, C14, C16 au tick 101 : 17 créations).
- [binaire] Premier tick compté, relâchements, sept effacements : inchangés (voir la version 0).
- [binaire] Clé `+0x1BC` : le tri `0x800399B8` la calcule à chaque tick (queue `0x8003B414`, après la passe de coups, porte `0x48`
  ouverte ou non) et la passe des effets du même tick la lit (`0x8003C374`, `0x8003C3E0`). Héros sans porteur (`+0x28`) ni voisin
  collidable plus bas qui le chevauche : `((PosY + (IDSV << 16)) & 0xFFFF0000) + (short)(PosZ >> 16)` (`0x800397EC`-`0x80039804`,
  `0x80039A94`-`0x80039AA4`), IDSV = `+0x1A8` de l'image affichée. Oracle : tri lancé après chaque tick, clé = formule sur les 150
  ticks de C2-steel (`0x00690000` pendant la charge). L'aura prend la clé + 1 : le binaire la dessine juste après le héros (ADR-0049).
- [DLL] Les opérations d'effets sont mises en file (`Emit`) et appliquées par `RunFrame` après tous les ticks de l'image
  (`AlundraEffectStage.cs:196-216`, `AlundraWorldProxy.cs:2449`) ; ADR-0046 suppose que les scripts ne lisent jamais le réservoir :
  `MovePlayer` le lit (poignée, `+0x7D`, retour de création).
- [DLL] `IAlundraEffectAnchor` (`AlundraEffectReservoir.cs:11-25`) n'a aucune implémentation de production ; `ZUpperBound //1bc`
  (`AlundraEntityScriptProxy.cs:407`) n'est jamais écrit : ne pas s'en servir.
- [DLL] Tri : effets sur le seau `DepthKey >> 20` puis `100 × ay` (`AlundraEffectQuadBuilder.cs:72-73`) ; sprite du héros sur le même
  seau puis `100 × ((PosY >> 16) − (PosZ >> 16))` (TopDownYUp sous `RenderProjectionComponent`). L'aura, 16 px plus haut, passerait
  SOUS le sprite du héros (écart (3) d'ADR-0049).
- [convention Z] `PosZ` de la DLL = binaire − 1 (ADR-0026). Les effets du héros prennent le Z du binaire, `PosZ + 1` (le `+ 1` change
  la rangée dessinée quand la fraction de Y est nulle, ADR-0049) : le Z d'un emplacement de la DLL égale celui de l'oracle. Le bouchon
  de tir garde la convention de la DLL : assertion `Z oracle = Z DLL + 1`.
- [oracle] `scratchpad/disc-f1/oracle-ext/` : l'oracle de la découverte (`tools/` identique au bit près, `probe_orig.py` = `probe.py`)
  avec le vrai code de création du binaire (`0x8003B9C4`, `0x8003BB14`, `0x8003BDD8`, `0x8003BE74`, réservoir réel de 128 emplacements) ;
  chaque ligne porte le mot `0x80098708` avant et après, `+0x1C` en entrée, la poignée avec `+0x68`/`+0x7D`, et les champs de chaque
  effet créé à la fin de la logique du tick, **avant** toute mise à jour (l'oracle ne lance pas `0x8003C410`). Colonnes communes
  égales à la découverte ligne à ligne ; sorties identiques au bit près d'une relance à l'autre. Pas d'avance du générateur par image :
  les tirages se comparent par paires (entrée, sortie). La vie des effets du scénario A5 est un MODÈLE ; la vie elle-même est prouvée
  par le réservoir de la DLL (ADR-0045, G1/G3).

**Modèle de création (D-E20-25, ADR-0056).**
- Le héros crée et libère ses effets **tout de suite** (`CreateNow`, `CreateAttachedNow`, `FreeNow` du stade), garde l'emplacement
  comme poignée et lit `Destroying` et le retour au tick même. Tout le reste reste en file (ADR-0046).
- Un effet créé tout de suite au tick k de l'image n'est mis à jour par `RunFrame` qu'à partir du tick k (marque `FirstUpdateTick`,
  locale à l'image, effacée par `RunFrame` à sa fin, même avec 0 tick).
- Exact à un tick par image, sauf l'indice d'emplacement (écart 3).
- Écarts écrits dans l'ADR :
  (1) une image de N ≥ 2 ticks qui contient c + 96 et c + 97 : l'aura est recréée au premier tick de l'image suivante, donc une image
  affichée sans aura ;
  (2) N ≥ 2 : les tirages de l'étincelle passent avant ceux des étincelles de coups des ticks précédents de la même image ;
  (3) le héros prend son emplacement avant les opérations en file de l'image, celles des événements de carte du même tick comprises,
  que le binaire sert avant lui ; seuls l'ordre de deux effets de même clé et le réservoir plein en dépendent.
- Le rattrapage des ticks d'effets en retard, qui enlèverait (1) et (2), est laissé à E20.e0b.

**Ancre et clé du héros (D-E20-26).**
- `AlundraEntityScriptProxy` implémente `IAlundraEffectAnchor` explicitement : `PosX`, `PosY`, `PosZ => PosZ + 1`,
  `Status => (int)Status`, `DepthKey => AlundraEntityDepthKey.Compute(PosY, idsv, PosZ + 1)`, avec
  `Compute = unchecked((uint)(((posY + (idsv << 16)) & (int)0xFFFF0000) + (short)(posZ >> 16)))`.
- L'IDSV vient d'une seule aide, tirée d'`AlundraFrameSyncPasses.cs:436-440` (règle `~Target` comprise) et partagée avec la passe de
  tri, sans la porte `_wallPlacementOverlayApplied`.
- Écarts partagés avec le sprite du héros (l'aura et le héros restent dans le même seau) : IDSV de l'image 0 du couple (animation,
  direction) ; exact pour l'animation 0 de la charge. Pas de levée par support, voisin ou porteur. Héros clignotant non trié dans le
  binaire. Hors périmètre.

**Ordre de dessin de l'aura (D-E20-27, amendement d'ADR-0049).** Un emplacement ancré (modes 1 et 3) se trie sur la coordonnée du
sprite de son ancre plus `DepthOffset` (aura : + 1, donc après toutes les parties du héros et avant toute rangée plus basse) ;
l'ancre l'expose par un membre par défaut de l'interface (aucune ancre de test à changer) ; le mode 0 garde la règle d'ADR-0049.
Aucun emplacement ancré n'existe aujourd'hui en production : traces inchangées.

**Périmètre.** `Alundra/Scripts/` : `UpdatePlayerWeaponEffect` (bouchon, `AlundraPlayerManager.cs:533`, appel `:275`) ; la branche de
relâchement de `PlayerTryAttack` ; `IAlundraScriptHost.EffectStage` ; `AlundraEffectStage` (création immédiate, marque) ; l'ancre
du héros et `AlundraEntityDepthKey` ; la règle des emplacements ancrés d'`AlundraEffectQuadBuilder` ; les deux effacements ; le
bouchon de tir qui compte ses appels avec leurs arguments ; tests ; ADR-0056 et l'amendement d'ADR-0049 ;
`docs/plan-e20-annexe/e20-f1/oracle/`.

**Hors périmètre.** L'apparition des projectiles (E20.e0a, E20.f3) ; l'avance du générateur par image et le rattrapage (E20.e0b) ;
la levée par voisin et l'IDSV par image ; les effacements de la blessure, d'E7 et de la mort ; le rebond du fléau (E20.f2) ; l'écart
d'une unité de Z du vase de vie (`AlundraEventProgramRunner.cs:3125`, préexistant).

#### Tâches

- ⏳ **T1 — Oracle.** Copier dans `docs/plan-e20-annexe/e20-f1/oracle/` :
  - depuis `scratchpad/disc-f1/oracle-ext/` : `ext.py`, `probe_ext.py`, `probe_orig.py`, `tools/`, `out/`, `out_orig/`, `summary.txt` ;
  - depuis `scratchpad/disc-f1/f1-check/oe/` : `keyprobe.py` (tri `0x800399B8` après chaque tick, clé du héros contre la formule).

  Deux chemins seulement changent : `EFFECTS_GLOBAL` (`probe_ext.py:303`) devient relatif à la racine du dépôt ; la section des dates
  (`DISCOVERY`, `:63`) est sautée si le dossier manque. Relancer `python probe_ext.py` (≈ 30 s, sans `-I`) et
  `python keyprobe.py`.
  Critères : les 17 fichiers de `out/` égalent au bit près ceux de `oracle-ext/out/` ; `summary.txt` : colonnes communes égales à la
  découverte (seule la ligne t91 de C2-steel diffère, explication déjà dans le résumé) ; `keyprobe` : 0 écart sur 150 ticks.
- ⏳ **T2 — Stade des effets.** `IAlundraScriptHost.EffectStage` (membre par défaut `null` ; la propriété publique
  d'`AlundraWorldProxy`, `:557`, le satisfait) ; `CreateNow`, `CreateAttachedNow`, `FreeNow`, la marque `FirstUpdateTick` respectée
  puis effacée par `RunFrame` (`Tick(bool)` reste pour les tests existants) ; la boucle autonome du héros
  (`AlundraEntityScriptProxy.cs:1391-1397`) marque son tick comme le bloc des entités (`AlundraWorldProxy.cs:2669`) ; ADR-0056.
  Arrêt si l'effet 3 ou `0x10` manque des données exportées.
- ⏳ **T3 — Ancre et clé.** `AlundraEntityDepthKey.Compute`, l'aide d'IDSV partagée, l'implémentation explicite de l'ancre.
- ⏳ **T4 — Charge.** `UpdatePlayerWeaponEffect(player, in pad, host)` : compteur et poignée sur le héros (à côté de
  `WeaponStepCounter`) ; aura selon la condition corrigée ; sons ; étincelle et ses tirages seulement si elle est créée ; Z =
  `PosZ + 1 + 0x100000` ; libération < 11 selon la réponse à O-E20-15 ; sans stade : compteur, son `0x2A` et libération seuls.
- ⏳ **T5 — Relâchement.** Comme la version 0, avec la gerbe en `PosZ + 1 + 0x100000` et l'effet nul qui saute ses écritures.
- ⏳ **T6 — Effacements.** Comme la version 0 ; l'aura reste jusqu'au tick normal suivant.
- ⏳ **T7 — Ordre de dessin de l'aura.** Test d'abord : la clé de chaque quad de l'aura compare strictement plus grand que la clé du
  sprite du héros (vrai `DepthSortable2DComponent.BuildSortKey` sur la position de son `RenderProjectionComponent`), même seau. Rouge :
  la règle des emplacements ancrés et l'amendement d'ADR-0049.
- ⏳ **T8 — Tests.**

#### Acceptation

- Contre l'oracle, tick par tick, pour C2-steel, C6-william, C10-crusher, C14-ice et C16-fire : compteur, animation, pointeur,
  compteur de pas, sons, appels du bouchon de tir (tick, type, X, Y, Z avec `Z oracle = Z DLL + 1`, direction) ; pour chaque effet
  créé, lu après la logique du tick et **avant** `RunFrame` : identifiant, indice, mode, ancre, `DepthOffset`, décalages, X, Y, Z
  (égal à l'oracle), forces ; mots du générateur en paires (entrée, sortie), le générateur posé sur le mot d'entrée de l'oracle.
- Exemples tirés de C2-steel :
  - compteur 1 au tick 1, 0 au tick 2, 1 au tick 23 ;
  - aura au tick 33, emplacement 0, mode 1, `OffsetZ` `0x100000`, `DepthOffset` 1, en (6553600, 6553600, 1) ;
  - son 42 tous les 4 ticks ; compteur 60 au tick 82 ;
  - étincelle au tick 85, emplacement 1, mot `12345678 > 24D2C78A`, (8274740, 4686300), forces (−86057, 93365), son 428 ;
  - tick 89 : son 429, `24D2C78A > AFBE9EAC` ;
  - relâchement au tick 91 : animation `0x18`, 16 effets aux emplacements 3 à 18 aux positions de `summary.txt` §4, son 43 ;
  - poignée nulle au tick 92 ;
  - tir de type 27 au tick 112 (Z oracle 1, direction `0x18`).
- Tick de relâchement de C10 (tick 101) : étincelle puis gerbe, 17 créations dans l'ordre de l'oracle.
- Une image de 2 puis de 3 ticks sur le passage du compteur de 10 à 12 donne le même état final que des images d'un tick (une seule
  aura). Mutations rouges : création par `Emit` → deux auras ; sans marque → une mise à jour de trop ; marque non effacée → une mise
  à jour manquée à l'image suivante.
- Charge tenue 300 ticks à un tick par image : aura créée aux ticks 33, 130 et 227, une seule vivante après chaque mise à jour.
  L'écart (1) est épinglé : une seule image affichée sans aura.
- `Destroying` posé à la main avant le tick 50 : nouvelle aura à ce tick (oracle A1) ; avant les ticks 60 et 70 : créations aux
  ticks 33, 60 et 70, emplacement 0 réutilisé (A2).
- Réservoir plein (P1) : l'aura est retentée à chaque tick, le son 42 reste ; une étincelle nulle n'a ni son ni tirage, et la
  suivante entre avec le mot inchangé. Cinq emplacements libres (P2) : 5 effets de gerbe, 11 nuls, son 43, pointeur et `0x18` présents.
- Clé : `Compute` contre des valeurs faites à la main (`PosZ` entier < 0, IDSV 43) ; clé de l'ancre du héros = sortie de `keyprobe`
  sur les ticks de charge de C2-steel (`0x00690000`).
- L'aura est dessinée au-dessus du sprite du héros (T7).
- Poignée périmée : selon la réponse à O-E20-15.
- Relâchement à S + D + 59 : échoue ; à S + D + 60 : réussit. Relâchement en l'air : perdu. Charge interrompue par `0x34` : pas de
  tir, compteur et pointeur à 0. Le coup `0x18` de l'épée d'acier touche un Beannoïde de la 14.
- Build Release puis Debug ; `Alundra.Tests` sans échec ; traces inchangées ; aucun test existant ne change sans raison du binaire ;
  verifier frais.
- Recette de l'auteur : avec `steel-m14`, tenir Carré : l'aura au-dessus du héros et les sons, l'aura qui se renouvelle sur une
  charge longue, puis le coup chargé au relâchement.

#### Arrêts, budget, retour arrière

- **Arrêts** :
  - un écart à l'oracle que le binaire n'explique pas ;
  - un effet dont les données manquent ;
  - un besoin de changer le moteur ;
  - une clé du héros qui demanderait la levée par voisin pour égaler l'oracle sur les arcs de f1 ;
  - T7 seule : si la coordonnée du sprite du héros ne peut pas être égalée dans la DLL, T7 passe ⚠️ et devient E20.f1v, sans bloquer
    le reste ; la recette note alors l'aura sous le héros.
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits d'E20.f1.

**Point ouvert O-E20-15 (poignée périmée).** Si l'aura expire pendant un verrouillage (`0x34`, sans appel à
`UpdatePlayerWeaponEffect`) et que son emplacement est repris, le premier tick normal libère l'effet qui l'occupe
(`0x8002F730`-`0x8002F744`, aucun test).
- (a) Reproduire.
- (b) Corriger, recommandé : un défaut avéré (pointeur pendant). Libérer seulement si l'emplacement porte encore l'aura, grâce à un
  numéro de génération posé par `CreateCore`.

Le correctif change ce que le joueur voit dans ce cas rare : à trancher par l'auteur à l'approbation.
```

## Split

- **No split is needed on the depth-key ground.** The port is one pure function, a shared IDSV helper and an explicit interface implementation on the proxy, under about 60 lines.
- The only real uncertainty is the aura's draw order (T7). T7 is last and has its own stop, which sends it to a follow-up E20.f1v without blocking the charge logic.
- If the main session prefers smaller slices, the clean split is f1a = T1-T6 + T8 and f1b = T7. The author's recipe "see the aura" would then wait for f1b.

## Scratch files

All under `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-f1/f1-check/`:
- `bd.py` (capstone, skipdata), `xjal.py` (jal call-site scan), `spark.py` (spark and burst recomputation).
- `oe/`: a re-run copy of oracle-ext, its outputs byte-identical to the original. In this copy only the `DISCOVERY` path of `probe_ext.py` was changed to an absolute path.
- `oe/keyprobe.py` and `oe/keyprobe.txt`: the binary's sort `0x800399B8` run after each tick; the hero's `+0x1BC` equals the formula on 150/150 ticks.
