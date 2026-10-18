import sys
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm34')
import linemap, difflib

S = 'Alundra/Scripts/'
pairs = [
    (S + 'AlundraEventProgramRunner.cs', 'Alundra/Events/EventProgramRunner.cs'),
    (S + 'EventOpcodeSizeTable.cs', 'Alundra/Events/EventOpcodeSizeTable.cs'),
    (S + 'EntitySearchService.cs', 'Alundra/Entities/EntitySearchService.cs'),
    (S + 'AlundraEntityScriptProxy.cs', 'Alundra/Entities/EntityScriptProxy.cs'),
    (S + 'AlundraWorldProxy.cs', 'Alundra/Maps/WorldProxy.cs'),
    (S + 'AlundraPlayerManager.cs', 'Alundra/Hero/PlayerManager.cs'),
    (S + 'AlundraTickPad.cs', 'Alundra/Hero/TickPad.cs'),
    (S + 'AlundraGameState.cs', 'Alundra/Session/GameState.cs'),
    (S + 'EventProgramState.cs', 'Alundra/Events/EventProgramState.cs'),
    (S + 'EntitySupport.cs', 'Alundra/Physics/EntitySupport.cs'),
    (S + 'AlundraEntitySpawnFactory.cs', 'Alundra/Entities/Spawning/EntitySpawnFactory.cs'),
]
for old_path, new_path in pairs:
    old = linemap.git_show('9502f75', old_path)
    new = linemap.read_head(new_path)
    oc = [linemap.canon(x) for x in old]
    nc = [linemap.canon(x) for x in new]
    sm = difflib.SequenceMatcher(None, oc, nc, autojunk=False)
    removed = []
    added = []
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag in ('replace', 'delete'):
            removed += [(i + 1, old[i].strip()) for i in range(i1, i2) if old[i].strip()]
        if tag in ('replace', 'insert'):
            added += [(j + 1, new[j].strip()) for j in range(j1, j2) if new[j].strip()]
    print(f'## {old_path} ({len(old)}) -> {new_path} ({len(new)}): old lines not matched {len(removed)}, new lines not matched {len(added)}')
    for ln, t in removed[:12]:
        print('   - old', ln, t[:120])
    for ln, t in added[:12]:
        print('   + new', ln, t[:120])
