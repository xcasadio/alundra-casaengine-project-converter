# Architecture Decision Records

This folder records the architecture decisions of this repository: architecture, data formats, public APIs, backends, and the rules that govern the AI agents working on it.

## Rules

- One file per decision, named `NNNN-short-title.md` with an increasing four-digit number. Decisions taken together on the same theme may share one file.
- Written in English, from the template [template.md](template.md): Status, Date, Source, Context, Decision, Consequences.
- Every decision taken during a plan or a discussion is recorded here, at the time it is taken (skill `adr`).
- Backfilled decisions keep a `Source` pointing at the original document. Existing audits are read-only: the decision is copied here, the audit is not edited.
- A decision is never rewritten: a change is a new record that supersedes the old one, whose status becomes `Superseded by ADR-XXXX`.

## Index

| ADR | Title | Status | Date |
|---|---|---|---|
| ADR-0001 | The game's XAML screens embed their XAML in the Alundra DLL | Superseded by ADR-0002 | 2026-09-21 |
| ADR-0002 | The game's screens are project assets bound to observable view models | Accepted | 2026-09-24 |
| ADR-0003 | The sound effect manifest carries the VAB volume and pan attributes | Accepted | 2026-09-25 |
| ADR-0004 | Background music follows the executable's sequence state | Accepted | 2026-09-25 |
| ADR-0005 | The inventory's opening portrait reaches the DLL through map_alundra.json and a one-id index | Accepted | 2026-09-26 |
| ADR-0006 | All Alundra text is authored as Yarn and the raw text tables are no longer exported | Accepted; partly superseded by ADR-0025 (numeric codes and `\Y`); `\X` bullet superseded by ADR-0007 | 2026-09-27 |
| ADR-0007 | The falcon update command keeps the state it replaces, and each `\X` reads on the original's side of the update | Accepted | 2026-09-27 |
| ADR-0008 | The dialogue box uses font3 and draws each glyph marker as the matching font3 character | Accepted | 2026-09-28 |
| ADR-0009 | font3 cells map to Unicode through their CP1252 byte, for proven characters only | Accepted | 2026-09-28 |
| ADR-0010 | Game flags stay in the DLL, and Yarn reads and writes them as `$flag_n` and `$tmp_flag_n` | Accepted; D-E16-16 range superseded by ADR-0011 | 2026-09-28 |
| ADR-0011 | Yarn flag variables cover only the original's 64-word banks (ids 0 to 2047) | Accepted | 2026-09-28 |
| ADR-0012 | The Alundra save game holds the original's saved state and is validated field by field before it is applied | Accepted | 2026-09-28 |
| ADR-0013 | A save is loaded through the warp departure path and applied at the arrival map's entry; the recipe keys exist only in Debug builds | Accepted | 2026-09-28 |
| ADR-0014 | The save book reproduces the original's memory-card save screens over four file slots | Accepted | 2026-09-29 |
| ADR-0015 | The event interpreter ports the missing opcodes from the binary, with a production loop guard and the E4.d detour for scripted walks | Accepted | 2026-09-29 |
| ADR-0016 | A blocked step on the cell field advances to contact, and ForceAdjusted keeps its shortfall rule | Accepted; D-E19-10 superseded by ADR-0017 | 2026-09-29 |
| ADR-0017 | ForceAdjusted rises only on a tick without progress, like the binary | Accepted | 2026-10-01 |
| ADR-0018 | The NPC animation lag is kept, arcs load real prefabs, and the engine gets an exact animation clock | Accepted; D-E19-16 refined by ADR-0019 | 2026-10-01 |
| ADR-0019 | Animation ends become exact on a logical clock while sprites stay in real time, and loops are counted | Accepted | 2026-10-01 |
| ADR-0020 | Opcode 0x24 is ported with a census of its waits, and opcodes 0x40/0x41 are ported in full | Accepted | 2026-10-01 |
| ADR-0021 | Entity contacts, scripted jumps and the hero's water and ice rules follow the binary, and test saves feed the recipes | Partially superseded by ADR-0091 | 2026-10-02 |
| ADR-0022 | Native-destruction obstacles stay solid, the walk detour waits on entities, and overlaps stay faithful | Partially superseded by ADR-0091 | 2026-10-02 |
| ADR-0023 | The hero jumps, falls and lands like the binary, with the take-off sound, and dialogue fidelity joins E19.f | Accepted; partly superseded by ADR-0028 (the "take-off only" sound scope) | 2026-10-02 |
| ADR-0024 | Destroyed entities are recycled through the engine like the binary | Accepted | 2026-10-03 |
| ADR-0025 | Numeric text codes and \Y are positioned Yarn markers | Accepted | 2026-10-03 |
| ADR-0026 | Absolute writes of Z follow the DLL convention (binary minus 1) at spawn, 0x8A and 0x64 | Accepted | 2026-10-03 |
| ADR-0027 | The native image is 320x240 at an integer scale with black bands | Accepted | 2026-10-03 |
| ADR-0028 | Every animation switch plays its sound, appearances included | Accepted | 2026-10-03 |
| ADR-0029 | The dialogue text box follows the binary to the tick | Accepted; D-E19-64 (the one-tick entity shift) superseded by ADR-0053 | 2026-10-05 |
| ADR-0030 | The extractor writes a per-texel alpha code, effect sheets, dialogue portraits and the odd last column | Accepted | 2026-10-03 |
| ADR-0031 | The entity sprite sheets are written in the Compact layout, one cell per Signature | Proposed | 2026-10-03 |
| ADR-0032 | The cellular rain respawn draws the C library rand() of the binary | Accepted | 2026-10-03 |
| ADR-0033 | The converter writes the PSX semi-transparency mode of each entity sprite | Accepted | 2026-10-03 |
| ADR-0034 | The backdrops get no tick during a warp departure, like the binary | Accepted | 2026-10-03 |
| ADR-0035 | The backdrop layers render the PSX semi-transparency per texel, like the binary | Accepted | 2026-10-05 |
| ADR-0036 | The font3 glyph rectangles come from the glyph table of the binary | Proposed | 2026-10-05 |
| ADR-0037 | The backdrop overlay tint draws with the PSX mode of the map, like the binary | Accepted | 2026-10-06 |
| ADR-0038 | The trailing spaces of a centred last line are kept, guarded by empty | Accepted | 2026-10-06 |
| ADR-0039 | The dialogue portraits are exported as sprites and linked from sprite-records.json | Accepted | 2026-10-06 |
| ADR-0040 | The save screen ticks after the dialogue pass, like slot 10 after slot 3 in the binary | Accepted | 2026-10-06 |
| ADR-0041 | The target of 0x22 is the binary's target in the DLL's frame, with a per-controller minimum move distance | Accepted | 2026-10-06 |
| ADR-0042 | The dialogue shows the speaker's name and portrait through the text box screen | Accepted | 2026-10-06 |
| ADR-0043 | The effects are exported as raw companions and sheets, not as sprites | Accepted | 2026-10-06 |
| ADR-0044 | The quad corners of deformed entity images are exported as a Corners track appended to the .anim2d | Accepted | 2026-10-06 |
| ADR-0045 | The effect reservoir lives in the DLL and ticks like the binary | Accepted | 2026-10-06 |
| ADR-0046 | The effect opcodes queue their operation on the effect stage and apply it at the tick that emitted it | Accepted | 2026-10-06 |
| ADR-0047 | The save screen reads the pad word of the previous tick, like the dialogue pass | Accepted | 2026-10-06 |
| ADR-0048 | The life vessel raises the stats at once and queues its effects from a pure native function | Accepted; the life seam (variant A) superseded by ADR-0056 | 2026-10-07 |
| ADR-0049 | The world pushes the effect quads to the engine every rendered frame, ordered like the entities | Accepted | 2026-10-08 |
| ADR-0050 | The inventory trigger runs at the end of the tick, after the dialogue pass and the map events, like the binary | Accepted | 2026-10-08 |
| ADR-0051 | The game streams its music with the loop point of the sequence | Accepted | 2026-10-08 |
| ADR-0052 | The hero's run, attack, carrying and item use, with the shared hit pass, form task E20 apart from E14 | Accepted | 2026-10-09 |
| ADR-0053 | The DLL drives each logic tick in the binary's order, and E20 owns swimming, loot and the hero's projectiles | Accepted | 2026-10-09 |
| ADR-0054 | The hurt hero keeps the original's rules before E18, and the next E20 slices follow the author's order | Accepted | 2026-10-09 |
| ADR-0055 | Native enemy handlers are registered per sprite type, read per-tick values, and arrive with the Muruta and the Beannoide | Accepted | 2026-10-09 |
| ADR-0056 | The hero's life lives on the entity and the stats mirror it at the end of every MovePlayer, as the binary does | Accepted | 2026-10-10 |
| ADR-0057 | The hero creates its own effects at once, attached effects sort with their anchor, and the stale aura handle is fixed | Accepted | 2026-10-10 |
| ADR-0058 | The weapons own their projectile handlers, E14.a comes before the weapons, collisions mean carrying and solid crates, and the deep-water wall comes with the currents | Accepted | 2026-10-10 |
| ADR-0059 | The hero's aura releases its slot at its tick and is recreated when its slot no longer holds it | Proposed | 2026-10-10 |
| ADR-0060 | The break effect is created for every DestroyEntity caller, as the binary does | Accepted | 2026-10-10 |
| ADR-0061 | The break-effect byte is exported in every sprite record | Accepted | 2026-10-10 |
| ADR-0062 | The sprite-table key of each prefab and the valid indices of each map are exported | Accepted | 2026-10-15 |
| ADR-0063 | The apparition by sprite index takes Z in the DLL's frame through a record-less core and checks each page against its own table | Accepted | 2026-10-15 |
| ADR-0064 | The entity slot table hands out the lowest free slot up to 63, and the spawned list follows the slots | Accepted | 2026-10-15 |
| ADR-0065 | An entity born by the sprite-index interface takes part in the tick of its birth, through a marker confined to that interface | Accepted | 2026-10-15 |
| ADR-0066 | The shared random stream steps once per logic tick, through an optional per-tick callback of the effect stage | Accepted | 2026-10-15 |
| ADR-0067 | The hero's shot reads the direction of the tick and wires types through a set that is empty until their handlers exist | Accepted | 2026-10-15 |
| ADR-0068 | An entity born by the sprite-index interface takes the map state first; the logical Z, the vertical step and the map edge follow in the next slices | Accepted | 2026-10-16 |
| ADR-0069 | An entity born by the sprite-index interface keeps its Z in the logic tick and steps vertically before the XY step | Accepted | 2026-10-16 |
| ADR-0070 | The step of an entity born by the sprite-index interface is bounded to the map edge before the mover and flags the same tick | Accepted | 2026-10-16 |
| ADR-0071 | An entity born by the sprite-index interface counts its first animation tick and reads its frame box before the engine integrates it | Accepted | 2026-10-16 |
| ADR-0072 | The sword wave is born in production, E10 is handled in line and C61 calls a named stub | Accepted | 2026-10-16 |
| ADR-0073 | Every creation rolls its loot as its last act, like the binary, on the real shared stream | Accepted | 2026-10-16 |
| ADR-0074 | An entity made from a record by a script opcode takes part in the tick that sees it born and makes its first animation switch at its creation | Accepted | 2026-10-16 |
| ADR-0075 | The Alundra gameplay DLL is restructured: one folder, one namespace, no `Alundra` prefix in type names | Accepted | 2026-10-16 |
| ADR-0076 | The XA tracks are converted to WAV by the converter and played by the DLL | Accepted | 2026-10-22 |
| ADR-0077 | Pickup sparks and grass-cut effects go through the effect queue | Accepted | 2026-10-22 |
| ADR-0078 | The engine draws entity shadows and the DLL pushes their state | Accepted | 2026-10-22 |
| ADR-0079 | The map header's byte 9 is exported as `TileCutEffectId`, renamed at the source | Accepted | 2026-10-22 |
| ADR-0080 | The test project mirrors the DLL tree by the type each file is named after | Accepted | 2026-10-22 |
| ADR-0081 | The structure of the DLL and its tests is guarded by rules, not by a table | Accepted | 2026-10-22 |
| ADR-0082 | The loot drop and the pickup of objects are ported, with named stubs for the fanfare, the chest and flagged objects | Accepted | 2026-10-23 |
| ADR-0083 | The cut of the grass keeps the binary's draws and order and queues only the effect | Accepted | 2026-10-23 |
| ADR-0084 | The charged arrow of William is born in production with C94 as a native handler, E1 in line and the cut of the grass called at every tick | Accepted | 2026-10-23 |
| ADR-0085 | The pickup of a key object cuts the music and restarts it from the top through the BGM fade director | Accepted | 2026-10-23 |
| ADR-0086 | The pushable pillar is ported from C76 and its stop of the sound is a named stub until g5c | Accepted | 2026-10-23 |
| ADR-0087 | The ground slope of the entities born by the interface is fed in the active statuses, for the marked entities with gravity only | Accepted | 2026-10-23 |
| ADR-0088 | The passengers follow their carrier in the order forces then moves, with the XY force of a pile added once per carrier | Accepted | 2026-10-24 |
| ADR-0089 | The object in the hands follows the hero by a carried branch of the motion, the three modes of UpdatePlayerCarriedEntity are pure rules, and C23 is registered for the seven liftable types | Accepted; amended by ADR-0092 | 2026-10-24 |
| ADR-0090 | The stop of one sound is the binary's StopSoundEffect (opcodes 0xBE and 0x75, the end of C76) and the push sound of the pillar loops | Accepted | 2026-10-24 |
| ADR-0091 | The soulevables are solid obstacles like every other collidable, and the movement obstacle probe skips no flag | Accepted | 2026-10-24 |
| ADR-0092 | C4 is the program of the transportable objects of the family C4, the key (C, 4) is registered for 19 types, and the wired set of the lift grows to 25 types | Accepted | 2026-10-25 |
| ADR-0093 | A thrown object falls in the hole or the exit floor it lands on: the hook of the ground slope also feeds the tile words of the marked entities | Accepted | 2026-10-25 |
| ADR-0094 | The treasure chest opens through the native F255 with the object it gives birth to, and the magic seed is one body with three entries | Accepted | 2026-10-25 |
| ADR-0095 | The opcode 0xC2 reads the death-retry counter, and a test-save preset may set it | Accepted | 2026-10-25 |
| ADR-0096 | The hero is hurt by an entry, a hurt block and the state 0x31, and every active entity blinks from its invulnerability counter | Accepted | 2026-10-25 |
| ADR-0097 | The first step of an entity born in the game is bounded by the halving search of the binary | Accepted | 2026-10-25 |
| ADR-0098 | The fire and ice balls run C60 and E9 as native handlers, the keys are registered in production, and the wired set of the shot grows to the types 11, 12, 14 and 15 | Accepted | 2026-10-25 |
| ADR-0099 | The use of an item has a frame (start table, driver, cancellation, menu guard) and the items themselves are named stubs | Accepted | 2026-10-26 |
| ADR-0100 | The bomb explodes and vanishes through D30 and E12, and the lids vanish through E19, all registered in production | Accepted | 2026-10-26 |
| ADR-0101 | The torch burns through C70 and E20 and the brambles through D34, all registered in production, and the torch joins the wired set of the lift | Accepted | 2026-10-26 |
| ADR-0102 | The drops of water fall through A19 and C71 and put out the torch under them, all registered in production | Accepted | 2026-10-26 |
| ADR-0103 | The bomb is born in the hands by the Circle and runs C62, with a start, a driver and a cancellation registered in production | Accepted | 2026-10-26 |
| ADR-0104 | The deep water is a wall for a hero without the swim boots, through a pseudo-bit of the walkability mask | Accepted | 2026-10-27 |
| ADR-0105 | The tomes start and are cast through a start function, the states 0x32 to 0x34 and a slope guard, inert in production | Accepted | 2026-10-27 |
