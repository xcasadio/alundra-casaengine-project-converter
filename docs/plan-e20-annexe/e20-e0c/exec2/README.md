# E20.e0c2 (fanfare B): T0 measurements (2026-10-23)

Plan: `docs/plan-e20-heros.md` section 1.26.1 (revision 0, READY, D-E20-65). HEAD of departure `6fa6bf81`, branch `chantier/theme1-porter-plateforme`. No DLL code is written by T0.

## 1. The sites, reread at the HEAD

- `Alundra/Ai/Objects/PickupAi.cs`: `FanfareStart` (line 94, a journaled stub), `FanfareIsBusy` (line 97, always false), `FanfareEnd` (line 100, a journaled stub). Calls: `FanfareEnd()` at the state 2 (line 165, after `SetCloseMask(DefaultCloseMask)`), `FanfareStart()` at the pickup (line 217, after `world.Block(entity)` and before `OpenMessage`). The plan's order (Block, music, message) already holds in the code: only the body of the stubs changes (D-E0C2-3).
- `Alundra/Audio/BgmFadeDirector.cs`: `StopAllSound` (returns at once when `BgmPlayer.Instance.CurrentMapSoundIndex < 0`; else stops the SFX if armed, restores the master volume, `BgmPlayer.PlaySequence`) and `LoadBgm(0)` (clears the reset flag, `BgmPlayer.StopSequence`, returns). Both read as the plan says.
- `Alundra/Audio/BgmPlayer.cs`: `StopSequence` stops the voice, the track stays loaded; `PlaySequence` starts a fresh voice from the top if no live voice and a track is loaded.
- `Alundra/Maps/IEntityWorldContext.cs:189`: `IBgmFadeDirector? BgmFadeDirector => null;` (default interface member). `PickupHost` (test support) does not override it.

## 2. Prediction of the existing tests that move

Predicted: none. Hosts without a director do not change (`BgmFadeDirector` is null), and a real world without a loaded track plays nothing (`StopAllSound` returns on a negative index, or `PlaySequence` finds no loaded track).

Measured by a temporary wiring, undone afterwards (`git diff` empty): `FanfareStart(world)` calling `world.BgmFadeDirector?.LoadBgm(0)` and `FanfareEnd(world)` calling `world.BgmFadeDirector?.StopAllSound()`, journals kept. Release build with the neutralised copy (`skipcopy.targets`), `--blame-hang-timeout 60s`, filter
`FullyQualifiedName~PickupAi | ~BgmFadeDirector | ~MusicPlayer | ~LootDrop | ~PickupObjects` (the `PickupAi*` classes, `BgmFadeDirector*`, `MusicPlayer*`, plus `LootDrop*` and the export guard that name `PickupAi`):

- 161 tests, 161 passed, 0 failed. **No existing test moves.** The prediction holds.

## 3. The montage of (m2)

Decision: (m2) is observable without the audio engine. It reuses the montage of `MusicPlayerTests.T1bis_...` (`Alundra.Tests/Audio/MusicPlayerTests.cs`):

- a fixture project with `Maps/music-index.json` and `Musics/bgm-manifest.json` (track 25), a `FakeAudioBackend` and a `FakeAudioClipProvider` with the stream registered, an `AudioService`;
- `BgmPlayer.Instance.AttachToWorld(service, project)` and `BgmFadeDirector.Instance.AttachToWorld(service, null)`; `BgmPlayer.Instance.PlayMapMusic(389)` loads track 25 and the frame close (`WorldProxy.ConsumeMusicResetSoundFlagOnFrameClose`) starts the voice;
- the observables: `backend.StreamVoices.Count` (a fresh voice = a new entry), `BgmPlayer.Instance.IsCurrentVoiceAlive`, `BgmPlayer.Instance.CurrentVoiceForTests` (the handle changes on restart);
- the pickup host is a `PickupHost` whose `BgmFadeDirector` is the real `BgmFadeDirector.Instance` (a new property of the support class, `PickupHost`);
- the class is in the collection `MusicPlayerSingletonCollection` (the singletons), with `ResetForTests` of both singletons in the constructor and in `Dispose`.

Therefore the stop condition "(m2) cannot observe the restart" does not hold; T1 goes on.

(m1) uses a recording `IBgmFadeDirector` (a new class in `Alundra.Tests/Support/`), with a shared call log that also records the `Block` of the host, the opening of the message and the close mask, to prove the order Block, LoadBgm(0), message. That needs the host and the dialogue fake to write into one log; `PickupHost.Block` and `FakeDialogueDirector.OpenComposed` are subclass-free, so the log is built by comparing the counts at each call of the recorder (it snapshots `host.Blocked.Count` and `dialogue.Opens.Count` when it is called).
