import sys
sys.path.insert(0, 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm34')
import linemap
T='Alundra.Tests/'
for rev in ('9502f75','7d776b3'):
    print('######', rev)
    linemap.batch(rev, [(T+'AlundraEventProgramRunnerTests.cs','323'),(T+'AlundraEventProgramRunnerTests.cs','331'),(T+'IntroTraceHarnessTests.cs','322'),(T+'IntroTraceHarnessTests.cs','332'),(T+'AlundraStoryChainOpcodeAudit.cs','128-338')] ) if False else None
