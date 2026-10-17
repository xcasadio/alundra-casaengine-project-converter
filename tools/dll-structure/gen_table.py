"""R1 of the DLL restructuring (docs/plan-restructuration-dll.md, section 5): generates the complete name table of the Alundra DLL.

For every top-level type of the DLL at the pinned base revision: its old file and name, and, at each stage (r2 = moved and split, no
rename; r3 = renamed; final = what R4 reaches), its file, namespace and name. Source of truth: the validated tree of section 2 of the
plan (the dictionaries below) and the declarations read from git at BASE_REVISION. The result is written to table.json and table.md next
to this script; replayed, it gives the same bytes (`git diff --exit-code`).

Read-only on the repository. Run: python -B tools/dll-structure/gen_table.py
Fails when: a file of the base revision is not mapped (or a mapped file is missing); a file has no known main type; two split types
aim at one path; a name occurs twice in a namespace at a stage (a partial type excepted).
"""
import collections
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scan_types  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))

# The revision the table is read at: the HEAD of the repository at the start of R1, after R0 (so the table does not move when R2 to R4
# move the files). It contains the result of R0 (script names written as full names).
BASE_REVISION = '5434cf6'

# folder (relative to Alundra/) for every source file of Alundra/Scripts; None = the file keeps its place in Scripts until R4 (data-cited).
FOLDER = {
    # Maps
    'AlundraWorldProxy.cs': 'Maps', 'IAlundraScriptHost.cs': 'Maps', 'IEntityWorldContext.cs': 'Maps', 'AlundraWorldIndexTable.cs': 'Maps',
    'AlundraMapSizeReader.cs': 'Maps', 'AlundraMapSpriteIndices.cs': 'Maps',
    'AlundraLogicClock.cs': 'Maps/Loop', 'AlundraFrameSyncPasses.cs': 'Maps/Loop', 'AlundraGameplayFreeze.cs': 'Maps/Loop',
    # Session
    'AlundraGameState.cs': 'Session', 'AlundraPlayerStats.cs': 'Session', 'AlundraChapterFlags.cs': 'Session',
    # Entities
    'AlundraEntityScriptProxy.cs': 'Entities', 'AlundraEntityScriptProxy.HitPass.cs': 'Entities', 'EntityFlags.cs': 'Entities',
    'EntityStatus.cs': 'Entities', 'AlundraEntitySlotTable.cs': 'Entities', 'EntitySearchService.cs': 'Entities',
    'AlundraEntityDepthKey.cs': 'Entities', 'SpriteRecordCatalog.cs': 'Entities', 'AnimationTables.cs': 'Entities',
    'AlundraEntitySpawnFactory.cs': 'Entities/Spawning', 'EntityRecordMapper.cs': 'Entities/Spawning',
    # Events
    'AlundraEventProgramRunner.cs': 'Events', 'EventProgramRunner.cs': 'Events', 'EventProgramState.cs': 'Events',
    'EventProgramDocument.cs': 'Events', 'EventOpcodeSizeTable.cs': 'Events', 'ScriptHelper.cs': 'Events',
    # Ai
    'AlundraNativeSpriteRegistry.cs': 'Ai', 'AlundraEnemyHelpers.cs': 'Ai', 'AlundraBeannoideAi.cs': 'Ai/Enemies',
    'AlundraMurutaAi.cs': 'Ai/Enemies', 'AlundraWaveSwordAi.cs': 'Ai/Projectiles', 'AlundraSaveBook.cs': 'Ai/Objects',
    # Hero
    'AlundraPlayerManager.cs': 'Hero', 'AlundraPlayerController.cs': 'Hero', 'AlundraTickPad.cs': 'Hero',
    'AlundraPlayerAnimationEffects.cs': 'Hero',
    # Combat
    'AlundraDamage.cs': 'Combat', 'AlundraHitPass.cs': 'Combat', 'AlundraHitOpcodes.cs': 'Combat', 'AlundraBalanceTables.cs': 'Combat',
    'AlundraFrameBoxTimeline.cs': 'Combat',
    # Items
    'AlundraItemTables.cs': 'Items', 'AlundraEquipmentRecords.cs': 'Items', 'AlundraWeaponTables.cs': 'Items', 'AlundraLootRoll.cs': 'Items',
    # Rng
    'AlundraRandom.cs': 'Rng', 'AlundraLibcRandom.cs': 'Rng',
    # Physics
    'AlundraScriptedMotion.cs': 'Physics', 'AlundraCellsCollisionField.cs': 'Physics', 'AlundraEntityCollision.cs': 'Physics',
    'EntitySupport.cs': 'Physics', 'AlundraTerrainProbe.cs': 'Physics', 'AlundraMovementObstacleProbe.cs': 'Physics',
    # Cells
    'AlundraCellStore.cs': 'Cells', 'AlundraCellVisualSync.cs': 'Cells', 'WallPlacementOverlay.cs': 'Cells',
    # Cameras
    'AlundraCameraDirector.cs': 'Cameras', 'AlundraCameraMath.cs': 'Cameras', 'AlundraCameraSway.cs': 'Cameras',
    # Rendering
    'AlundraBackdropStage.cs': 'Rendering/Backdrops', 'BackdropDocument.cs': 'Rendering/Backdrops', 'BackdropLoader.cs': 'Rendering/Backdrops',
    'AlundraEffectBank.cs': 'Rendering/Effects', 'AlundraEffectQuadBuilder.cs': 'Rendering/Effects', 'AlundraEffectReservoir.cs': 'Rendering/Effects',
    'AlundraEffectStage.cs': 'Rendering/Effects', 'AlundraFrozenEffectAnchor.cs': 'Rendering/Effects', 'AlundraNativeEffects.cs': 'Rendering/Effects',
    # Audio
    'AlundraMusicPlayer.cs': 'Audio', 'AlundraBgmFadeDirector.cs': 'Audio', 'AlundraMusicIndexTable.cs': 'Audio', 'AlundraSoundBank.cs': 'Audio',
    'AlundraSoundGroupIndexTable.cs': 'Audio', 'AlundraSoundPlayer.cs': 'Audio', 'AlundraSpuVoiceVolume.cs': 'Audio',
    # Transitions
    'AlundraWarpDirector.cs': 'Transitions', 'AlundraPortalTrigger.cs': 'Transitions', 'AlundraPortalRecord.cs': 'Transitions',
    'AlundraScreenFadeDirector.cs': 'Transitions',
    # Dialogues
    'AlundraDialogueDirector.cs': 'Dialogues', 'AlundraDialogueBox.cs': 'Dialogues', 'AlundraDialogueNameBox.cs': 'Dialogues',
    'AlundraChoiceBox.cs': 'Dialogues', 'AlundraEtcStringTable.cs': 'Dialogues', 'AlundraFont3Advances.cs': 'Dialogues',
    'AlundraTextProgress.cs': 'Dialogues',
    'AlundraYarnBindings.cs': 'Dialogues/Yarn', 'AlundraYarnVariableStorage.cs': 'Dialogues/Yarn', 'AlundraDialogueCapturePresenter.cs': 'Dialogues/Yarn',
    # UI
    'AlundraHudComposer.cs': 'UI/Hud', 'AlundraHudDirector.cs': 'UI/Hud', 'AlundraHudPresenter.cs': 'UI/Hud', 'AlundraHudScreen.cs': 'UI/Hud',
    'AlundraHudViewModel.cs': 'UI/Hud',
    'AlundraInventoryComposer.cs': 'UI/Inventory', 'AlundraInventoryDirector.cs': 'UI/Inventory', 'AlundraInventoryPresenter.cs': 'UI/Inventory',
    'AlundraInventoryScreen.cs': 'UI/Inventory', 'AlundraInventoryViewModel.cs': 'UI/Inventory',
    'AlundraInventoryPortrait.cs': 'UI/Shared', 'AlundraInventoryPostProcess.cs': 'UI/Shared', 'AlundraInventoryTextReveal.cs': 'UI/Shared',
    'AlundraSubInventoryComposer.cs': 'UI/SubInventory', 'AlundraSubInventoryDirector.cs': 'UI/SubInventory',
    'AlundraSubInventoryPresenter.cs': 'UI/SubInventory', 'AlundraSubInventoryScreen.cs': 'UI/SubInventory',
    'AlundraSubInventoryViewModel.cs': 'UI/SubInventory',
    'AlundraTextBoxPresenter.cs': 'UI/Messages', 'AlundraTextBoxScreen.cs': 'UI/Messages', 'AlundraTextBoxViewModel.cs': 'UI/Messages',
    'AlundraChoicePresenter.cs': 'UI/Choice', 'AlundraChoiceScreen.cs': 'UI/Choice', 'AlundraChoiceViewModel.cs': 'UI/Choice',
    'AlundraDialoguePresenter.cs': 'UI/Speech',
    'AlundraSaveScreen.cs': 'UI/Saving', 'AlundraSaveScreenDirector.cs': 'UI/Saving', 'AlundraSaveScreenPresenter.cs': 'UI/Saving',
    'AlundraSaveScreenViewModel.cs': 'UI/Saving',
    # SaveGames
    'AlundraSaveGame.cs': 'SaveGames', 'AlundraSaveGameDirector.cs': 'SaveGames', 'AlundraSaveGameRules.cs': 'SaveGames',
    'AlundraEngineSaveSlots.cs': 'SaveGames', 'IAlundraSaveSlots.cs': 'SaveGames', 'AlundraTestSaves.cs': 'SaveGames',
    # Probes
    'AlundraRandomShadow.cs': 'Probes', 'AlundraStickProbe.cs': 'Probes',
}

# The nine types the exported data cite (D-R-5): they stay in Alundra/Scripts, namespace Alundra.Scripts, under their name until R4.
DATA_CITED = {
    'AlundraWorldProxy': 'WorldProxy', 'AlundraEntityScriptProxy': 'EntityScriptProxy', 'AlundraPlayerController': 'HeroController',
    'AlundraHudViewModel': 'HudViewModel', 'AlundraInventoryViewModel': 'InventoryViewModel', 'AlundraSubInventoryViewModel': 'SubInventoryViewModel',
    'AlundraTextBoxViewModel': 'TextBoxViewModel', 'AlundraChoiceViewModel': 'ChoiceViewModel', 'AlundraSaveScreenViewModel': 'SaveScreenViewModel',
}
SPECIAL = {'AlundraMusicPlayer': 'BgmPlayer', 'AlundraRandom': 'GameRandom', 'AlundraSoundBank': 'SfxBank'}
SPECIAL.update(DATA_CITED)
# Files whose name changes beyond the prefix (the interface file freed for the interpreter).
FILE_SPECIAL = {'EventProgramRunner.cs': 'IEventProgramRunner.cs', 'AlundraEventProgramRunner.cs': 'EventProgramRunner.cs',
                'AlundraPlayerController.cs': 'HeroController.cs', 'AlundraMusicPlayer.cs': 'BgmPlayer.cs', 'AlundraRandom.cs': 'GameRandom.cs',
                'AlundraSoundBank.cs': 'SfxBank.cs', 'AlundraWorldProxy.cs': 'WorldProxy.cs',
                'AlundraEntityScriptProxy.cs': 'EntityScriptProxy.cs', 'AlundraEntityScriptProxy.HitPass.cs': 'EntityScriptProxy.HitPass.cs'}



# The main type of a file whose stem names no type.
MAIN = {'EventProgramRunner.cs': 'IEventProgramRunner', 'AlundraEntityScriptProxy.HitPass.cs': 'AlundraEntityScriptProxy'}
# Secondary types the tree keeps in their main type's file ("(+ X)" in section 2).
KEEP = {'NoOpEventProgramRunner', 'AlundraMapEvent'}
# Internal secondary types the tree lists as their own entry (own file), with the folder the tree gives them.
SPLIT_INTERNAL = {'AlundraSlotShadow': 'Probes', 'AlundraPortalScanner': 'Transitions'}

# The two files of the DLL root that are not under Alundra/Scripts. Plugin.cs keeps its place and its namespace; the other has no type.
ROOT_FILES = [
    {'old_file': 'Alundra/Plugin.cs', 'r2': 'Alundra/Plugin.cs', 'r3': 'Alundra/Plugin.cs', 'final': 'Alundra/Plugin.cs',
     'namespace': 'Alundra', 'types': ['Plugin']},
    {'old_file': 'Alundra/InternalsVisibleTo.Tests.cs', 'r2': 'Alundra/Properties/InternalsVisibleTo.cs',
     'r3': 'Alundra/Properties/InternalsVisibleTo.cs', 'final': 'Alundra/Properties/InternalsVisibleTo.cs', 'namespace': None, 'types': []},
]
CSPROJ = 'Alundra/Alundra.csproj'
SCRIPTS = 'Alundra/Scripts/'
STAGES = ('r2', 'r3', 'final')


def new_type_name(name):
    if name in SPECIAL:
        return SPECIAL[name]
    if re.match(r'^IAlundra[A-Z0-9]', name):
        return 'I' + name[len('IAlundra'):]
    if re.match(r'^Alundra[A-Z0-9]', name):
        return name[len('Alundra'):]
    return name


def new_file_name(f):
    if f in FILE_SPECIAL:
        return FILE_SPECIAL[f]
    if re.match(r'^IAlundra[A-Z0-9]', f):
        return 'I' + f[len('IAlundra'):]
    if re.match(r'^Alundra[A-Z0-9]', f):
        return f[len('Alundra'):]
    return f


class TableError(Exception):
    pass


def build(rev=BASE_REVISION):
    """The table as a dict, read at the git revision `rev`."""
    sha = scan_types.git(scan_types.REPO, 'rev-parse', '--verify', rev + '^{commit}').decode().strip()
    decls, paths = scan_types.dll_decls(rev=sha)
    scripts = [p for p in paths if p.startswith(SCRIPTS)]
    names = [p[len(SCRIPTS):] for p in scripts]
    problems = []
    if any('/' in n for n in names):
        problems.append('a file under Alundra/Scripts is in a subfolder: %s' % [n for n in names if '/' in n])
    missing = sorted(set(names) - set(FOLDER))
    extra = sorted(set(FOLDER) - set(names))
    if missing:
        problems.append('files of the base revision without a folder: %s' % missing)
    if extra:
        problems.append('mapped files missing at the base revision: %s' % extra)
    others = sorted(set(paths) - set(scripts) - {r['old_file'] for r in ROOT_FILES})
    if others:
        problems.append('unexpected .cs files outside Alundra/Scripts: %s' % others)
    for r in ROOT_FILES:
        if r['old_file'] not in paths:
            problems.append('root file missing at the base revision: ' + r['old_file'])
    if problems:
        raise TableError('\n'.join(problems))

    top = [d for d in decls if not d['nested'] and d['path'].startswith(SCRIPTS)]
    by_file = collections.defaultdict(list)
    for d in top:
        by_file[d['path']].append(d)
    rows = []
    for d in top:
        f = d['path'][len(SCRIPTS):]
        stem = f[:-3]
        main = MAIN.get(f, stem)
        t = d['name']
        if main not in {x['name'] for x in by_file[d['path']]}:
            raise TableError('%s: no type named %s (the main type of the file)' % (d['path'], main))
        is_main = t == main
        folder = FOLDER[f]
        file_cited = any(x['name'] in DATA_CITED for x in by_file[d['path']])
        split = (not is_main) and t not in KEEP and (d['access'] == 'public' or t in SPLIT_INTERNAL)
        tfolder = SPLIT_INTERNAL.get(t, folder)
        final_ns = 'Alundra.' + tfolder.replace('/', '.')
        new = new_type_name(t)
        if split:
            r2 = {'file': 'Alundra/%s/%s.cs' % (tfolder, t), 'namespace': final_ns, 'name': t}
            r3 = {'file': 'Alundra/%s/%s.cs' % (tfolder, new), 'namespace': final_ns, 'name': new}
            fin = r3
        elif file_cited:
            r2 = {'file': SCRIPTS + f, 'namespace': 'Alundra.Scripts', 'name': t}
            r3 = {'file': SCRIPTS + f, 'namespace': 'Alundra.Scripts', 'name': t if t in DATA_CITED else new}
            fin = {'file': 'Alundra/%s/%s' % (folder, new_file_name(f)), 'namespace': final_ns, 'name': new}
        else:
            r2 = {'file': 'Alundra/%s/%s' % (folder, f), 'namespace': final_ns, 'name': t}
            r3 = {'file': 'Alundra/%s/%s' % (folder, new_file_name(f)), 'namespace': final_ns, 'name': new}
            fin = r3
        rows.append({'old_file': d['path'], 'old_name': t, 'kind': d['kind'], 'access': d['access'], 'partial': d['partial'],
                     'main': is_main, 'split': split, 'data_cited': t in DATA_CITED, 'r2': r2, 'r3': r3, 'final': fin})
    rows.sort(key=lambda r: (r['old_file'], r['old_name']))

    # consistency of the table itself
    for st in STAGES:
        split_paths = collections.Counter(r[st]['file'].lower() for r in rows if r['split'])
        shared = {r[st]['file'].lower() for r in rows if not r['split']}
        for p, n in split_paths.items():
            if n > 1 or p in shared:
                problems.append('stage %s: a split type aims at the path %s which another type already holds' % (st, p))
        uses = collections.defaultdict(list)
        for r in rows:
            uses[(r[st]['namespace'], r[st]['name'])].append(r)
        for (ns, nm), rs in uses.items():
            if len(rs) > 1 and not all(x['partial'] for x in rs):
                problems.append('stage %s: the name %s.%s occurs %d times (not partial)' % (st, ns, nm, len(rs)))
    if problems:
        raise TableError('\n'.join(problems))
    return {
        'base_revision': sha,
        'note': 'Generated by tools/dll-structure/gen_table.py; do not edit. Stages: r2 = moved and split, no rename; r3 = renamed; final = '
                'reached when the nine data-cited types move (R4).',
        'counts': {'script_files': len(scripts), 'types': len(rows), 'renamed_types': sum(1 for r in rows if r['old_name'] != r['final']['name']),
                   'multi_type_files': sum(1 for v in by_file.values() if len(v) > 1), 'split_types': sum(1 for r in rows if r['split']),
                   'data_cited_types': sum(1 for r in rows if r['data_cited']),
                   'arrival_files': len({r['final']['file'].lower() for r in rows}),
                   'root_files': len(ROOT_FILES)},
        'root_files': ROOT_FILES,
        'types': rows,
    }


def render_md(t):
    rows = t['types']
    out = ['# Alundra DLL: name table', '',
           'Generated by `tools/dll-structure/gen_table.py` from the declarations of revision `%s`; do not edit.' % t['base_revision'][:12], '',
           '- %(script_files)d files under `Alundra/Scripts`, %(types)d top-level types (%(renamed_types)d renamed), %(multi_type_files)d files declare '
           'several types, %(split_types)d secondary types take their own file, %(arrival_files)d files at arrival.' % t['counts'],
           '- Stages: **r2** = file moved and split, namespace set, no rename; **r3** = renamed; **final** = after R4. The nine data-cited types '
           '(marked) stay in `Alundra/Scripts` under namespace `Alundra.Scripts` until R4.',
           '- Root files: `Alundra/Plugin.cs` (type `Plugin`, namespace `Alundra`) does not move; `Alundra/InternalsVisibleTo.Tests.cs` becomes '
           '`Alundra/Properties/InternalsVisibleTo.cs` from r2 (no type). `Alundra/Alundra.csproj` has no `<Compile>` and does not change.', '',
           '## By final file', '', '| Final file | Namespace | Types (old -> new) | Old file | Stays in Scripts until R4 |', '|---|---|---|---|---|']
    by_final = collections.defaultdict(list)
    for r in rows:
        by_final[r['final']['file']].append(r)
    for fin in sorted(by_final, key=str.lower):
        rs = by_final[fin]
        ts = ', '.join('`%s`' % r['old_name'] if r['old_name'] == r['final']['name'] else '`%s` -> `%s`' % (r['old_name'], r['final']['name']) for r in rs)
        olds = ', '.join(sorted({'`%s`' % r['old_file'][len(SCRIPTS):] for r in rs}))
        stay = ', '.join('`%s`' % r['old_name'] for r in rs if r['r3']['file'] == r['old_file'])
        out.append('| `%s` | `%s` | %s | %s | %s |' % (fin[len('Alundra/'):], rs[0]['final']['namespace'], ts, olds, stay))
    out += ['', '## Old path -> new path (for the frozen oracle annexes, D-R-6)', '',
            '| Old file | After R2 | After R3 | Final | Secondary types that leave (final file) |', '|---|---|---|---|---|']
    by_old = collections.defaultdict(list)
    for r in rows:
        by_old[r['old_file']].append(r)
    for old in sorted(by_old, key=str.lower):
        rs = by_old[old]
        main = [r for r in rs if r['main']][0]
        left = ', '.join('`%s` (`%s`)' % (r['old_name'], r['final']['file'][len('Alundra/'):]) for r in rs if r['split'])
        out.append('| `%s` | `%s` | `%s` | `%s` | %s |' % (old[len('Alundra/'):], main['r2']['file'][len('Alundra/'):],
                                                       main['r3']['file'][len('Alundra/'):], main['final']['file'][len('Alundra/'):], left))
    for r in t['root_files']:
        out.append('| `%s` | `%s` | `%s` | `%s` | |' % (r['old_file'][len('Alundra/'):], r['r2'][len('Alundra/'):], r['r3'][len('Alundra/'):], r['final'][len('Alundra/'):]))
    return '\n'.join(out) + '\n'


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    try:
        t = build()
    except (TableError, scan_types.ScanError) as e:
        print('ERROR: ' + str(e))
        return 1
    with open(os.path.join(HERE, 'table.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(t, f, indent=1, ensure_ascii=False)
        f.write('\n')
    with open(os.path.join(HERE, 'table.md'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(render_md(t))
    print('base', t['base_revision'][:12], json.dumps(t['counts']))
    return 0


if __name__ == '__main__':
    sys.exit(main())
