import subprocess, sys, re
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
SCR = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad'
TARGETS = SCR + '/e20e0a3b-exec/skipcopy.targets'
SP = REPO + '/Alundra/Audio/SoundPlayer.cs'
RN = REPO + '/Alundra/Events/EventProgramRunner.cs'
PL = REPO + '/Alundra/Ai/Objects/PillarAi.cs'
classes = ['SoundPlayerStopTests', 'EventProgramRunnerTests', 'PillarAiStopSoundTests', 'PillarAiGoldenTests', 'PillarSoundWorldTests', 'ButtonStopSoundWorldTests', 'StopSoundSitesTests']
filt = '|'.join('FullyQualifiedName~' + c for c in classes)
sel = [int(x) for x in sys.argv[1].split(',')] if len(sys.argv) > 1 else None
SFX_HEAD = 'public void StopSfx(int sfxId)\n    {\n'
M = [
 ('0xBE still PlaySfx', RN, 'playSound2BisPlayer.StopSfx((v[2] << 8) | v[1]);', 'playSound2BisPlayer.PlaySfx((v[2] << 8) | v[1]);'),
 ('0xBE as StopAllSfx', RN, 'playSound2BisPlayer.StopSfx((v[2] << 8) | v[1]);', 'playSound2BisPlayer.StopAllSfx();'),
 ('StopSfx cuts the other ids too', SP, SFX_HEAD, SFX_HEAD + '        if (sfxId != int.MinValue)\n        {\n            StopAllSfx();\n            return;\n        }\n\n'),
 ('StopSfx does not free the cap (voices not forgotten)', SP, '            _audioService.Stop(voice.Handle);\n        }\n\n        voices.Clear();\n    }\n\n    /// <inheritdoc cref="ISoundPlayer.StopAllSfx"/>', '            _audioService.Stop(voice.Handle);\n        }\n    }\n\n    /// <inheritdoc cref="ISoundPlayer.StopAllSfx"/>'),
 ('StopSfx forgets the voices but does not stop them', SP, '        foreach (var voice in voices)\n        {\n            _audioService.Stop(voice.Handle);\n        }\n\n        voices.Clear();\n    }\n\n    /// <inheritdoc cref="ISoundPlayer.StopAllSfx"/>', '        voices.Clear();\n    }\n\n    /// <inheritdoc cref="ISoundPlayer.StopAllSfx"/>'),
 ('only one tone stopped', SP, '        foreach (var voice in voices)\n        {\n            _audioService.Stop(voice.Handle);\n        }\n\n        voices.Clear();\n    }\n\n    /// <inheritdoc cref="ISoundPlayer.StopAllSfx"/>', '        if (voices.Count > 0)\n        {\n            _audioService.Stop(voices[0].Handle);\n        }\n\n        voices.Clear();\n    }\n\n    /// <inheritdoc cref="ISoundPlayer.StopAllSfx"/>'),
 ('0xBE id on 8 bits', RN, 'playSound2BisPlayer.StopSfx((v[2] << 8) | v[1]);', 'playSound2BisPlayer.StopSfx(v[1]);'),
 ('StopSfx gated by the armed state', SP, SFX_HEAD, SFX_HEAD + '        if (BgmFadeDirector.Instance.IsArmed)\n        {\n            return;\n        }\n\n'),
 ('StopSfx registers the id in the anti-duplicate table', SP, SFX_HEAD, SFX_HEAD + '        TryRegisterForThisFrame(sfxId);\n'),
 ('0x75 forgotten (no call)', RN, 'playSoundEffectPlayer.StopSfx(v[1]);', '_ = playSoundEffectPlayer;'),
 ('0x75 still PlaySfx', RN, 'playSoundEffectPlayer.StopSfx(v[1]);', 'playSoundEffectPlayer.PlaySfx(v[1]);'),
 ('0x75 id on 16 bits', RN, 'playSoundEffectPlayer.StopSfx(v[1]);', 'playSoundEffectPlayer.StopSfx((v[2] << 8) | v[1]);'),
 ('C76 stops unconditionally (call site)', PL, 'if (step.StopSound)', 'if (step.StopSound || true)'),
 ('C76 never stops (call site)', PL, 'if (step.StopSound)', 'if (step.StopSound && step.Counter == int.MinValue)'),
 ('C76 stops another id', PL, 'context.SoundPlayer?.StopSfx(PushSfx);', 'context.SoundPlayer?.StopSfx(PushSfx + 1);'),
 ('C76 Step: stop whatever the animation', PL, 'animation == PushAnimationId, true)', 'true, true)'),
 ('C76 Step: stop only for animation 0', PL, 'animation == PushAnimationId, true)', 'animation == 0, true)'),
 ('C76 stops in the push too (contact branch)', PL, 'new CounterStep(counter, true, true, false, false)', 'new CounterStep(counter, true, true, true, false)'),
 ('StopSfx of an unknown id throws-free path removed (TryGetValue -> indexer)', SP, 'if (!_liveVoicesBySfxId.TryGetValue(sfxId, out var voices))\n        {\n            return;\n        }\n\n        foreach (var voice in voices)\n        {\n            _audioService.Stop', 'var voices = _liveVoicesBySfxId[sfxId];\n        foreach (var voice in voices)\n        {\n            _audioService.Stop'),
]
origs = {f: open(f, encoding='utf-8', newline='').read() for f in (SP, RN, PL)}
results = []
try:
    for i, (name, f, old, new) in enumerate(M):
        if sel is not None and i not in sel:
            continue
        text = origs[f]
        crlf = '\r\n' in text
        o, n = (old.replace('\n', '\r\n'), new.replace('\n', '\r\n')) if crlf else (old, new)
        if text.count(o) != 1:
            results.append((i, name, 'PATTERN COUNT %d' % text.count(o))); print(results[-1], flush=True); continue
        open(f, 'w', encoding='utf-8', newline='').write(text.replace(o, n, 1))
        r = subprocess.run(['dotnet', 'test', REPO + '/Alundra.Tests', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + TARGETS,
                            '--filter', filt, '--blame-hang-timeout', '60s', '--nologo'], capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=580)
        open(f, 'w', encoding='utf-8', newline='').write(text)
        out = r.stdout + r.stderr
        m = re.search(r'(?:chec|Failed)\w*\s*[!:]?\s*(?:-\s*)?.*?(?:Échec|Failed|chec)\s*:\s*(\d+)', out)
        fails = re.findall(r'^\s+(?:Échoué|Failed) (\S+)', out, re.M)
        mm = re.search(r'ch[^:\s]*\s*:\s*(\d+),\s*r[^:\s]*\s*:\s*(\d+)', out)
        if 'error CS' in out:
            res = 'BUILD ERROR'
        elif mm:
            res = ('KILLED (%s red of %d)' % (mm.group(1), int(mm.group(1)) + int(mm.group(2)))) if int(mm.group(1)) > 0 else 'SURVIVED'
        else:
            res = 'UNPARSED'
        results.append((i, name, res, fails[:6])); print(results[-1], flush=True)
finally:
    for f, t in origs.items():
        open(f, 'w', encoding='utf-8', newline='').write(t)
print('RESTORED', all(open(f, encoding='utf-8', newline='').read() == t for f, t in origs.items()))
