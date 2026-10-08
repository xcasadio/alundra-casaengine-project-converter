# L-a (engine): streamed music loops back to a loop-start sample, read-only discovery, 2026-10-05

Nothing was built, tested, run (game, demo, editor, extractor, converter), committed or edited in a repository.
Repository `alundra-casaengine-project-converter` at `a046568` (branch `chantier/e19-opcodes`). Engine submodule at
`a6efd2a9` (`chantier/e19s2-ui-clip-view-space`). The author's uncommitted `CasaEngine.Launcher/Program.cs` was not
touched, and none of its lines is cited. Every engine line below is at `a6efd2a9` unless another ref is named.

The tools used were `rg`, `sed`, `git show`/`git diff` on other refs, `ilspycmd` on the MonoGame 3.8.5.1 DLL in the
NuGet cache (output in `mg/`), and Python scripts in this folder: `fillsim.py`, `warplog.py` and `wavinfo.py`
(section 7).

## Summary (answer first)

- **Design (minimal, above `IAudioBackend`, backend-agnostic).**
  1. `SoundAsset.LoopStartSample` is a `long` sample-frame index, where a frame is one sample per channel. It
     defaults to 0, and a negative value is clamped to 0. `Load` reads the optional key `loop_start_sample`.
     `EditorAssetJsonSerializer.SaveSoundAsset` writes the key only when it is not 0, following the
     `PsxSemiTransparency` precedent at `EditorAssetJsonSerializer.cs:383-386`. Every existing `.sound` therefore
     re-saves byte for byte.
  2. `WavStreamReader.SeekToSample(long sampleFrame)` seeks to `DataOffset + frame × BlockAlign` and sets
     `Position`. It throws `ArgumentOutOfRangeException` unless `0 ≤ frame < DataLength / BlockAlign`.
     `Rewind()` becomes `SeekToSample(0)` and keeps its behaviour.
  3. `MusicPlayer`:
     - `Play` copies the asset's loop start into the track.
     - It validates the loop start against the reader's format, at `Play` and never in `Update`. An out-of-range
       value logs a throttled warning and falls back to 0.
     - At the end of data, `FillQueue` calls `SeekToSample(track.LoopStartSample)` instead of `Rewind()`
       (`MusicPlayer.cs:287-289`).
     - With loop start 0, the bytes submitted are identical to today's.
  4. `FillQueue` submits at most `queuedBufferTarget` buffers per call. A correct backend never reaches this bound,
     because each submit increments its pending count. So nothing changes on MonoGame or on the engine's fake. The
     bound stops the infinite loop that a backend reporting 0 pending buffers causes today
     (`FakeAudioBackend.cs:213-228`; the DLL fake hard-codes 0 at `Alundra.Tests/FakeAudioBackend.cs:189`).
  5. Non-goals:
     - No change to `IAudioBackend`.
     - No loop end: with option B the file ends at J2.
     - No crossfade in the engine: the extractor bakes it.
     - No loop for resident (`PlaySound`) clips: MonoGame ignores loop points (`loop-disc/mg/SoundEffect.cs:542-554`).
     - No DLL change: that is L-b.
- **Files (engine only).**
  - Runtime:
    - `CasaEngine/Framework/Audio/SoundAsset.cs`
    - `CasaEngine/Framework/Audio/Streaming/WavStreamReader.cs`
    - `CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs`
    - optionally `Streaming/WavFormatInfo.cs` (`SampleFrameCount`).
  - Editor services: `CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:740-749`.
  - Editor: `CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs:273-285`. `CreatePreviewCopy` must copy the
    field, otherwise the preview silently loops from 0. The inspector row is optional.
  - Tests:
    - `CasaEngine.Tests/Audio/WavStreamReaderTests.cs`
    - `MusicPlayerTests.cs`, or a new `MusicPlayerLoopStartTests.cs`
    - `SoundAssetTests.cs`
    - `SoundAssetEditorSerializationTests.cs`
    - a ramp helper. Keep it out of `WavBuilder.cs`'s `Create`/`ExpectedData` region, which `chantier/audio-modern`
      also edits.
  - Docs:
    - `docs/engine/audio-system.md` §3, §5, §9 and §11
    - a new ADR (number **0056 or later**, because **0055 is taken** on `chantier/audio-modern`) plus its index
      line in `docs/decisions/README.md`
    - the engine plan file `ai-agent/tasks/<subject>-tasks.md` (engine AGENTS.md §3).
  - Demo: `CasaEngine.Demos/Demos/AudioDemo.cs` plus demo content (section 5).
- **Red-first tests with values worked out in advance** (section 3, computed by `fillsim.py`).
  - Synthetic WAV: a 1000-frame stereo ramp where frame f is `(L=f, R=10000+f)`, looped, loop start 600.
  - After `Play` (16 KiB buffers, target 3), the buffers are 4000 B `[0..999]`, 1600 B `[600..999]` and 1600 B
    `[600..999]`. After `ConsumeBuffers(3)` and `Update`, three more `[600..999]` follow.
  - The concatenated frames are i for i < 1000, then `600 + (i-1000) mod 400`.
  - Red today: frames `[0..999]` × 3.
  - Header with an 18-byte `fmt ` chunk and a 6-byte `LIST` chunk: the data offset is 60. A naive `44 + 600×4` seek
    would land on frame **596**.
  - Bound: a non-looped 65536-sample file, with the fake reporting 0 pending buffers.
    - Today 16 buffers (262144 B) go out in `Play`. The bound gives 3 (49152 B), then 6 (98304 B) after one
      `Update`.
    - The looped variant spins forever today. Run it under a watchdog, plus `--blame-hang-timeout`.
- **Existing tests that move.** None for the loop start or the bound. If the queue depth default changes (point
  below): `MusicPlayerTests.Play_StartsAStreamAndQueuesAhead` (`:38-53`),
  `Update_TopsTheQueueBackUpAsBuffersAreConsumed` (`:68-81`) and `SampleCount` (`:9-11`). The DLL and the converter
  are untouched: no DLL code uses `MusicPlayer` (`rg`), and the DLL fake's `OpenStream` returns null
  (`Alundra.Tests/FakeAudioBackend.cs:335`).
- **Starvation.**
  - Today's margin at 44.1 kHz, 16-bit stereo is 3 × 16384 B = **278.6 ms at best and 185.8 ms at worst**.
    MonoGame counts the playing buffer as pending until `UpdateQueue` unqueues it.
  - Subtract up to one frame: audio is refilled last in the frame, and the world load happens at the start of the
    next frame.
  - After a stall, the refill comes one frame late. The pending count read right after the stall is stale, because
    `FrameworkDispatcher.Update` ran before the stall.
  - Warps load synchronously on the game thread (`GameManager.cs:98-144`). A warp between maps with the same track
    keeps the voice playing (`AlundraMusicPlayer.cs:210-212`, for example 389↔390 on track 25).
  - The only timing evidence is the launcher's Debug log, with 1-second timestamps. 389/390 loads fall within
    0–2 s. The 478 load provably took **more than 1 s**, spanning 15:51:46 to 15:51:48.
  - Recommendation:
    - (a) Raise `MusicPlayer.DefaultQueuedBufferTarget` from 3 to **24**: 2.136–2.229 s at 44.1 kHz,
      4.27–4.46 s at 22.05 kHz, 384 KiB per track, with the per-call bound equal to the target. Measure the real
      warp stall first (section 4.6).
    - (b) If stalls go beyond ~2 s, the real fix is off-game-thread reads. That is already slice **S2 of
      `chantier/audio-modern`**: "Lecture disque et décodage hors du thread de jeu … points de boucle".
    - Preloading into memory does not help: starvation depends only on how much audio is queued, and MonoGame's
      `DynamicSoundEffectInstance` queue is not thread-safe (decompile `:350-401`).
- **Demo and device check.**
  - Engine AGENTS.md §6 (`AGENTS.md:44`) requires running the existing `AudioDemo`, plus a minimal demo for a
    non-trivial feature. Real playback is not covered by tests (`docs/engine/audio-system.md:256-257`); the
    precedent is a by-ear check (`ai-agent/tasks/audio-system-tasks.md:498-514`).
  - Proposal: a 3-second 44.1 kHz demo WAV (a 1-second 440 Hz intro, then 2 s of 660 Hz beeps), with
    `loop_start_sample` 44100 on the key `O`. The intro must be heard once, then beeps every 0.5 s with no gap.
  - Optional key `H`: a deliberate 0.5, 2.0 or 2.5 s game-thread hitch, with the queue count logged one frame later
    (0 means starved). This is a device check of the starvation choice.
- **Coordination blockers for the main session and the author** (section 6):
  - The ADR number.
  - The overlap with `chantier/audio-modern`'s S2: loop points and off-thread streaming are in its program envelope.
  - The base commit: stack on `a6efd2a9`, like the other E19 engine slices. Engine `main` `3e0770d6` (streaming
    sample-rate fix) is not in it.
  - Conflict files: `AudioDemo.cs`, `WavBuilder.cs`, `audio-system.md` §9 and `docs/decisions/README.md`.

## 1. Facts: the engine's audio stack for music (engine `a6efd2a9`)

### 1.1 AudioService (`CasaEngine/Framework/Audio/AudioService.cs`)
- The constructor builds `Music = new MusicPlayer(this)` with the defaults (`:32`). There is no setting for buffer
  size or queue depth. The stereo mixer is built at `:33`.
- `PlayStream(sampleRate, channelCount, bus, parameters, owner)` (`:150-183`):
  - It creates a backend streaming voice that is not started yet. `entry.IsStreaming = true` (`:179`).
  - Streaming voices are never recycled by `Update` (`:554-560`): "only its feeder knows" whether silence is the end
    or an underrun.
- `SubmitStreamBuffer` (`:259-265`) forwards only when the service still tracks the voice; otherwise it returns
  silently. `GetPendingBufferCount` (`:268-271`) returns the backend's count, or 0.
- `Update(elapsed)` (`:537-588`) runs the voice fades and bus gains, then `Music.Update` (`:586`), then
  `_stereoVoiceMixer.Update` (`:587`).
- `AudioSystemComponent.Update` calls `Service.Update(ElapsedGameTime)`
  (`Application/Components/AudioSystemComponent.cs:84-87`). The component's `UpdateOrder` is `Audio`
  (`AudioSystemComponent.cs:37`), which is **last** in the frame (`Application/ComponentOrder.cs:22-24`).

### 1.2 MusicPlayer (`CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs`)
- `DefaultBufferSizeInBytes = 16384` (`:23`; its doc assumes "185 ms of 22 kHz stereo") and
  `DefaultQueuedBufferTarget = 3` (`:26`; doc "roughly half a second of margin").
- The constructor (`:36-50`) accepts `bufferSizeInBytes` (minimum 1024) and `queuedBufferTarget` (> 0). One scratch
  buffer is shared by every track (`:48-49`): the backend copies on submit.
- `Play(asset, fadeIn, owner)` (`:60-118`):
  - It refuses a non-streaming asset (`:69-73`).
  - It opens the reader through `ClipProvider.OpenStream` and `new WavStreamReader` (`OpenReader`, `:302-328`).
    Every failure is logged and returns `None`, without throwing.
  - It calls `PlayStream` and takes a track slot. It copies **only** `IsLooped` from the asset (`:103`).
  - It calls `FillQueue` **before** `StartVoice` (`:108-110`), then the optional fade-in (`:112-115`).
- `Update` (`:230-260`):
  - Per track: if the voice is not alive, it releases the track (`:246-250`).
  - Then it calls `FillQueue` (`:252`).
  - Then, if `IsFinishing` and pending is 0, it stops and releases the track (`:254-258`).
  - So a non-looped track ends only when its queue has drained, never on a transient starvation.
- `FillQueue` (`:273-300`):
  - It loops **while** the track is not finishing and pending < target (`:275`). Each turn reads up to one buffer
    (`:277`).
  - At the end of data, a non-looped track becomes finishing (`:281-285`). A looped track calls `Reader.Rewind()`
    and reads again (`:287-289`). A second empty read makes it finishing (`:291-295`).
  - It submits with offset 0 (`:298`).
  - **There is no per-call cap.** When the backend's pending count does not grow, a looped track never leaves this
    loop. The last buffer before the end of data is partial: the read stops at the end of data (section 1.3).
- `GetPosition` (`:215-224`) is the reader's `Position`, in seconds. Its doc says "Loops back to zero on a rewind".
- The `Track` class (`:380-388`) holds Reader, Voice, IsLooped, IsFinishing, InUse and Generation. `ReleaseTrack`
  (`:363-378`) resets them.
- The class doc says "Looping is done by rewinding the reader: MonoGame refuses IsLooped on a dynamic voice" (`:17`).
  It also says the queue is "kept several hundred milliseconds deep so a long frame — shader compilation, world
  load — does not starve the voice" (`:12-14`).

### 1.3 WavStreamReader and WavFormatInfo (`Streaming/WavStreamReader.cs`, `Streaming/WavFormatInfo.cs`)
- The stream must be readable (`:34-37`) and **seekable** (`:39-42`, message "looping needs to rewind").
- The header parser (`:114-200`):
  - It requires RIFF/WAVE (`:121-124`).
  - It walks the chunks: `fmt ` of 16 bytes or more, with the extra bytes skipped (`:144-165`); unknown chunks are
    skipped with RIFF odd-size padding (`:196`, `:245-259`).
  - `DataOffset` = the stream position right after the `data` chunk header (`:174`). It is **not** assumed to be 44.
  - `DataLength` = the chunk size, or the file remainder when the size is 0 or too large (`:175-178`), rounded down
    to a whole block (`:193`).
  - It accepts only 16-bit PCM, mono or stereo (`:202-243`).
- `Read(buffer, offset, count)` (`:60-88`) returns at most the remaining data, rounded down to whole blocks. It
  returns 0 at the end. It allocates nothing.
- `Rewind()` (`:91-97`) seeks to `DataOffset` and sets `_dataBytesRead = 0`. `Position` = data bytes read (`:54`).
  `IsEndOfStream` (`:51`).
- Fact from the repository: the demo music `racing_game_music_1.wav` has an 18-byte `fmt ` chunk and a `fact` chunk,
  so its **data offset is 58** (`wavinfo.out`). The Alundra BGM WAVs have the plain 44-byte header (`bgm_045.wav`,
  `wavinfo.out`).

### 1.4 SoundAsset, loader, editor serializer and inspector (the silent-loss pitfalls)
- `SoundAsset` (`CasaEngine/Framework/Audio/SoundAsset.cs`):
  - Fields: `AudioFileAssetId`, `Volume` (sanitized, `:31-35`), `Pitch` (`:38-42`), `IsLooped` (`:44`), `BusName`
    (`:47-51`) and `IsStreaming` (`:58`).
  - `CreateVoiceParameters` (`:61-64`).
  - `Load(JObject)` (`:66-79`) reads every key optionally, with a default: `is_looped` at `:76`, `is_streaming` at
    `:78`.
  - Invalid values are sanitized silently (`:81-99`). There is no logging in this class.
  - `JsonHelper` has `GetInt32`/`GetUInt32` but no `GetInt64` (`CasaEngine/Core/Serialization/JsonHelper.cs:34-47`).
    A long would be read with `Value<long>()`.
- `SoundAssetLoader` (`Assets/Loaders/SoundAssetLoader.cs:13-27`) is `new SoundAsset()` followed by `Load`.
- **Writing happens elsewhere.** `EditorAssetJsonSerializer.TrySerialize` dispatches `case SoundAsset` → `SaveSoundAsset`
  (`CasaEngine.EditorServices/EditorAssetJsonSerializer.cs:98-100`). `SaveSoundAsset` (`:740-749`) writes the six
  keys explicitly.
  - A new property not written there is lost at the first editor save, while `Load` still reads it.
  - This is the same class of pitfall as the memory note on `EntityComponent`'s serializer: the defaults written
    match the defaults read, so behaviour hides it and only a key diff shows it.
  - Precedent for "omitted at default": `if (spriteData.PsxSemiTransparency != None) node.Add(...)` (`:383-386`).
    `:426-428` and `:488-491` do the same.
- **Second silent-drop site.** `SoundAssetInspectorPanel.CreatePreviewCopy` (`CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs:273-285`)
  copies the fields one by one into a preview asset on the Editor bus. Streaming previews go through it
  (`:231-237`), so a new field must be copied there too. The field rows are at `:318-334`. `CasaEngine.Editor` has
  no `InternalsVisibleTo` for the tests, and the method is private static: it cannot be unit-tested without
  widening.
- Other `new SoundAsset { … }` sites in production code are `GameEditor.cs:3751` (Create Sound) and
  `SoundAssetInspectorPanel.cs:74`. The other sites are tests (`rg`).
- Tests today: `SoundAssetTests.cs:11-60` (load, defaults) and `SoundAssetEditorSerializationTests.cs:10-69`
  (round trip and key names). The key test asserts the presence of the six keys only.

### 1.5 Clip provider (`CasaEngine/Framework/Audio/AssetContentManagerAudioClipProvider.cs`)
- `OpenStream(id)` (`:155-185`):
  - It resolves the catalogue entry (`:187-193`: the runtime resolver, or `AssetCatalog`).
  - It returns `new FileStream(fullPath, Open, Read, FileShare.Read)` (`:178`), which is seekable.
  - Failures log and return null.
- The test fake `FakeAudioClipProvider.OpenStream` returns `new MemoryStream(content, writable: false)`
  (`CasaEngine.Tests/Audio/FakeAudioClipProvider.cs:30-35`).
- The resident path `GetClip` keeps every acquired clip for the provider's lifetime (`:119-153`). This matters for
  L-b: after option B, a resident track 14 would be 76.3 MiB and track 7 59.4 MiB (`fillsim.out`).

### 1.6 StereoVoiceMixer and ADR-0039's bound
- `QueuedBufferTarget = 3` and `MaxBuffersPerUpdate = 3` (`Streaming/StereoVoiceMixer.cs:35-42`). `FillQueue`
  stops at `submitted < MaxBuffersPerUpdate` (`:154-178`).
- ADR-0039 says "at most 3 buffers submitted per voice per `Update`. The bound also protects against a backend that
  never reports queued buffers" (`docs/decisions/0039-software-stereo-voices.md:21`). It also says "Not done: loop
  points (the whole clip loops)" (`:31`).
- The existing test `AudioServiceStereoVoiceTests.Update_NeverSubmitsMoreThanThreeBuffersPerVoice_EvenIfTheBackendNeverReportsItsQueue`
  (`CasaEngine.Tests/Audio/AudioServiceStereoVoiceTests.cs:206-224`) gives 3, 6, then 9 buffers. That is the model
  for the music bound test.

### 1.7 IAudioBackend, the MonoGame backend, and MonoGame internals
- `IAudioBackend` streaming members (`IAudioBackend.cs:55-79`): `SupportsStreaming`, `CreateStreamingVoice`,
  `SubmitBuffer` ("data is copied"), `GetPendingBufferCount` ("Zero means the voice is starving") and `Start`.
  **L-a does not change it.**
- `MonoGameAudioBackend`:
  - It creates a `DynamicSoundEffectInstance` per streaming voice (`:211-251`, `:426`).
  - It never sets `IsLooped` on a dynamic instance (`:436-441`).
  - `SubmitBuffer` returns silently on a stale slot and **catches `InstancePlayLimitException`** (`:253-276`). If
    that path ever fires, the pending count does not grow, and a looped `FillQueue` would spin. **This is a
    hypothesis, not observed:** the decompiled `PlatformSubmitBuffer` does not obviously throw it.
  - `GetPendingBufferCount` returns `dynamicInstance.PendingBufferCount` (`:278-286`).
- MonoGame DesktopGL 3.8.5.1, decompiled by `ilspycmd` into `mg/` (`Directory.Packages.props:18` = 3.8.5.1):
  - `PendingBufferCount` → `PlatformGetPendingBufferCount` → `_queuedBuffers.Count` (`mg/DynamicSoundEffectInstance.cs:59-65`, `:315-318`).
    The playing buffer stays counted until it is processed.
  - Processed buffers are unqueued **only** in `PlatformUpdateQueue` (`:385-401`). It is called from
    `DynamicSoundEffectInstanceManager.UpdatePlayingInstances` (`mg/DynamicSoundEffectInstanceManager.cs:42-58`),
    which `FrameworkDispatcher.DoUpdate` calls (`mg/FrameworkDispatcher.cs:30-35`). `Game.DoUpdate` calls
    `FrameworkDispatcher.Update()` **before** `Update(gameTime)` (`mg/Game.cs:999-1007`).
  - `PlatformSubmitBuffer` (`:350-370`):
    - It allocates an `OALSoundBuffer` per submit, and copies again when `offset != 0`.
    - It queues the buffer.
    - It restarts the source (`AL.SourcePlay`) when the instance is Playing but the OpenAL source has stopped
      (`:364-369`). That is the recovery after an underrun.
  - Variable time step: one `DoUpdate` with the accumulated elapsed time, clamped to `MaxElapsedTime` = 500 ms
    (`mg/Game.cs:315`, `:783-786`, `:816-821`). The Alundra project runs with `"IsFixedTimeStep": false`
    (`alundra-project/AlundraGame.json:6`) and `"VSyncEnabled": true` (`:19`).
- The engine's frame order (`CasaEngine/Framework/Application/CasaEngineGame.cs:555-573`):
  `GameManager.UpdateWorld(gameTime)` (`:555`), **then** the components through `base.Update` (`:572`), with
  audio last.

### 1.8 ADRs and docs
- ADR-0001 (`docs/decisions/0001-audio-runtime-architecture-v1.md:14`): "Music streaming is written in the engine
  from V1: `DynamicSoundEffectInstance` fed by an in-engine RIFF PCM reader". Its consequences note "disk reads
  happen on the game thread inside `Update`" (`:23`).
- ADR-0002 (`0002-audio-asset-format-and-editor-scope-v1.md:14`) gives the V1 `.sound` metadata list: file, volume,
  pitch, loop, bus and streaming mode. The new field amends that list.
- `docs/engine/audio-system.md`:
  - §3 holds the `.sound` JSON and its field table (`:60-92`).
  - §5 says "La boucle rembobine le lecteur" (`:133-135`).
  - §9 says "Lecture disque sur le thread de jeu … environ une demi-seconde de file d'avance" (`:250-252`). That
    already overstates the margin at 44.1 kHz.
  - §9 also says "La lecture réelle n'est pas couverte par les tests … le reste passe par la démo" (`:256-257`).
  - §11 lists the demo keys (`:273-289`).
- Engine `AGENTS.md`:
  - §3 requires a plan when there is more than one commit (`:22`).
  - §6 covers tests and demos: "un sample ou une démo minimal est obligatoire pour toute feature visible non
    triviale. Si un sample existe pour la zone touchée, le lancer au moins une fois" (`:40-44`).
  - §9.2 allows interfaces only at real backend boundaries (`:79`).
  - §9.3 lists the hot paths, including streaming (`:82-84`).
  - §9.7 forbids silent changes to asset formats (`:99-101`).
  - §9.8 prefers additive API changes (`:103-105`).
  - §10 requires an ADR for a format, public API or backend decision (`:123-126`).

### 1.9 Fakes
- Engine `FakeAudioBackend`:
  - `SubmitBuffer` increments `PendingBuffers` and, with `RecordSubmittedBuffers`, keeps a copy of each buffer
    (`CasaEngine.Tests/Audio/FakeAudioBackend.cs:188-211`).
  - `ReportsZeroPendingBuffers` (`:213-228`).
  - `ConsumeBuffers` (`:249-256`), `GetSubmittedBytes`/`GetSubmittedBuffers` (`:258-268`).
- DLL fake: `GetPendingBufferCount => 0` (`Alundra.Tests/FakeAudioBackend.cs:189`) and `OpenStream => null`
  (`:335`). L-b will need a real `OpenStream`, and L-a's bound is what keeps a looped L-b test from freezing
  (`docs/plan-e11c-musique.md:125-127`: "Un test de musique en route A gèlerait la suite").

### 1.10 Concurrent engine work (decision-relevant)
- **Engine `main` = `3e0770d6`** (2026-10-05 16:53): "refuse out-of-range streaming sample rates without losing a
  voice" (`6921e8d0`).
  - It is **not** in `a6efd2a9`: 2 commits on `main` are not in HEAD, and 31 commits of the E19 stack are not in
    `main`.
  - It touches `AudioService.cs:169` (the log text), the `IAudioBackend.cs` doc, `MonoGameAudioBackend.cs` and
    `audio-system.md` §9: a new bullet right before "Lecture disque sur le thread de jeu".
- **`chantier/audio-modern`** (worktree `CasaEngineMonogame/.claude/worktrees/audio-modern`, head `c4e003e9`,
  approved by the author on 2026-10-05, AUTO mode until T1.8):
  - It adds a software mixer and a `SoftwareAudioBackend` behind the unchanged `IAudioBackend`.
  - It claims **ADR-0055** ("Engine-owned software audio mixer with thin native outputs").
  - Its program envelope lists **S2**: "Lecture disque et décodage hors du thread de jeu ; … points de boucle et
    plage de pitch élargie (API additive)". X3/X4 cover a real-time SEQ sequencer for Alundra's music
    (`audio-modern-tasks.md:202-212`, a copy is in this folder).
  - Its diff against `main` does **not** touch `MusicPlayer.cs`, `WavStreamReader.cs`, `SoundAsset.cs` or the
    editor serializer.
  - It does touch `AudioDemo.cs` (19 lines), `WavBuilder.cs` (+14, right after `Create`), `FakeAudioBackend.cs`
    (conformance fixes), `docs/engine/audio-system.md`, `docs/decisions/README.md` and ADR-0001's status line.
  - In its `SoftwareAudioBackend`, `GetPendingBufferCount` drains the mixer's events on every call, so the count is
    fresh rather than one frame stale (`SoftwareAudioBackend.cs:374-377`, `:471-517`, copy in this folder).

## 2. The minimal change (design for L-a)

### 2.1 `SoundAsset.LoopStartSample`
- `public long LoopStartSample { get; set; }`:
  - The setter clamps a negative value to 0, like `SanitizeVolume`.
  - Doc: "Sample frame (one sample per channel, counted from the first audio byte) where a **looped, streamed**
    sound restarts once it reaches the end of its data. 0 rewinds to the start (default). Ignored by resident
    sounds, because MonoGame DesktopGL has no loop points."
- `Load`: `LoopStartSample = element.ContainsKey("loop_start_sample") ? element["loop_start_sample"].Value<long>() : 0;`
  - The value is not validated against the file here: the length is known only when the WAV is opened.
- Why `long`: it matches `WavFormatInfo.DataLength` and `WavStreamReader.Position`. Alundra's largest value is
  27204 × 735 = 19 994 940 (track 14, J2), which would also fit an `int`.
- Values for L-b: track 25 gives 5 344 920, track 14 (the extractor's J1) 10 695 720, tracks 45 and 46 279 300
  (`fillsim.out`).

### 2.2 Serializer and editor
- `SaveSoundAsset` gets `if (soundAsset.LoopStartSample != 0) { node.Add("loop_start_sample", soundAsset.LoopStartSample); }`
  after `is_streaming`. Every existing `.sound` is unchanged on re-save: the two demo files under
  `CasaEngine.Demos/Content/Audio/` and any authored one.
- `CreatePreviewCopy` gets `LoopStartSample = source.LoopStartSample`.
- An inspector row (an integer field) is optional. Without it, the editor still preserves a hand-written value,
  because Load and Save both carry it.

### 2.3 `WavStreamReader.SeekToSample(long sampleFrame)`
- It throws `ObjectDisposedException` when the reader is disposed.
- It throws `ArgumentOutOfRangeException` unless `0 ≤ sampleFrame < Format.DataLength / Format.BlockAlign`. A value
  equal to the frame count would make every later read return 0.
- `var offset = sampleFrame * Format.BlockAlign; _stream.Seek(Format.DataOffset + offset, Begin); _dataBytesRead = offset;`
- `Rewind()` becomes `SeekToSample(0)`; its observable behaviour is identical.
- Optional: `WavFormatInfo.SampleFrameCount => DataLength / BlockAlign`.
- Seekability is already guaranteed by the constructor (`:39-42`).
- It must not allocate. The existing allocation test is on a `MemoryStream`. That `FileStream.Seek` does not
  allocate is assumed, not verified here.

### 2.4 `MusicPlayer`
- `Track` gains `public long LoopStartSample;`, which `ReleaseTrack` resets to 0.
- In `Play`, after `OpenReader`:
  - `var loopStart = asset.LoopStartSample;`
  - When `loopStart >= reader.Format.DataLength / reader.Format.BlockAlign`, write a throttled warning
    ("loop start N is past the end of 'name' (M sample frames); it loops from the start") and set `loopStart = 0`.
  - Then `track.LoopStartSample = loopStart;`.
  - The warning string is built in `Play`, not in `Update` (hot-path rule §9.3).
- In `FillQueue`: replace `track.Reader.Rewind()` with `track.Reader.SeekToSample(track.LoopStartSample)`. Keep the
  "second read 0 → finishing" guard.
- The seam needs no special handling. The last read before the end is partial (it stops at the end of data) and is
  submitted. The next read returns 0, then the reader seeks, and the following buffer starts exactly at the loop
  start. Buffers queued back to back are gapless, as today's whole-file loop already relies on.
  - Side effect, documented as a limit: a loop region shorter than one buffer produces short buffers. Alundra's
    shortest loop region is 338 frames × 735 = 248 430 sample frames (5.6 s), so it does not matter there.
- Docs to update:
  - The class remark (`:17`): "Looping seeks the reader back to the asset's loop start".
  - `GetPosition` (`:215`): "Goes back to the loop start when the track loops".
  - The constant docs (`:22-26`), which assume 22 kHz.

### 2.5 Per-call bound
- In `FillQueue`, `var submitted = 0;` with the loop condition `&& submitted < _queuedBufferTarget`, and
  `submitted++` after each submit. This is the `StereoVoiceMixer.cs:156-170` pattern, with the bound equal to the
  target.
- The bound is equal to the target, not a separate constant 3, for two reasons:
  - With a correct backend, the loop already stops at `target - pending ≤ target` submissions. The bound is never
    reached, so behaviour is unchanged on MonoGame, on the software backend and on the engine fake.
  - If the target is raised (section 4), a full refill after a stall still completes in one call.
- It also makes `Play`'s initial fill bounded under a 0-reporting backend.

### 2.6 Unchanged, and non-goals
- `IAudioBackend`, `AudioService`'s public API, `SoundPlaybackOverrides` (there is no per-call loop-start override),
  `SoundEmitterComponent` and the cutscene `PlayMusic`. They inherit the loop start through the asset.
- Out of scope:
  - a loop end;
  - an engine-side crossfade at the seam: option B bakes it in the WAV (D-E19-73);
  - resident-clip loop points;
  - any DLL, converter or extractor change: that is L-b and the extractor/converter slices.

### 2.7 ADR and docs
- A new engine ADR, "Streamed music loops back to an authored loop-start sample frame", with these decisions:
  - the field, its unit and its default;
  - the key omitted at 0;
  - streaming only;
  - the out-of-range fallback;
  - the per-call bound;
  - the queue depth, if decided.
- It amends ADR-0002's metadata list and ADR-0001's "the loop rewinds the reader".
- Number: **0055 is already used** by `chantier/audio-modern`. A local branch scan finds no 0056-0059, so propose
  0056 and re-scan every branch and worktree at execution.
- `docs/engine/audio-system.md`:
  - §3: the JSON example and a table row `loop_start_sample` | "sample frame where a looped streamed sound restarts;
    omitted means 0".
  - §5: the loop behaviour.
  - §9: the queue margin. It is on the line next to engine `main`'s new bullet, so a textual conflict is likely.
  - §11: the new demo key.

### 2.8 Base commit and conflicts
- Like the other E19 engine slices, L-a should start from the parent's engine pointer `a6efd2a9`. Engine `main`
  (`3e0770d6`) is not in that stack.
- Expected textual conflicts at a later integration:
  - with `main`: `audio-system.md` §9;
  - with `chantier/audio-modern`: `AudioDemo.cs`, `WavBuilder.cs` (if a helper is added after `Create`),
    `audio-system.md` and `docs/decisions/README.md`.
- No overlap on `MusicPlayer.cs`, `WavStreamReader.cs`, `SoundAsset.cs` or the serializer.

## 3. Tests: red first, values computed in advance (`fillsim.py`, `fillsim.out`)

### 3.0 Synthetic data
- **Ramp R1000**: 1000 stereo frames at 44100 Hz, 16-bit, where frame f is `L = f`, `R = 10000 + f`.
  - Built with `WavBuilder.Create(PcmFormatTag, 44100, 2, 16, data)`, the default 16-byte `fmt `, so the data
    offset is 44.
  - Variant **R1000h**: `formatChunkSize: 18` plus `extraChunk: new byte[6]` with id `LIST`. Its data offset is
    12 + (8+18) + (8+6) + 8 = **60**. A naive `44 + 600×4 = 2444` lands at data byte 2384, which is **frame 596**.
- `WavBuilder.CreatePcm16` fills `i % 251` bytes (`WavBuilder.cs:18-22`), not a ramp. The ramp must be built in the
  test, or by a new helper placed away from `audio-modern`'s insertion point.
- Decoding helper for the tests: concatenate `backend.GetSubmittedBuffers(voice)`
  (`RecordSubmittedBuffers = true`, `FakeAudioBackend.cs:193`), then read int16 pairs.

### 3.1 WavStreamReader
| test | input | expected | red today |
|---|---|---|---|
| W1 `SeekToSample_LandsOnTheFrame_WithANon44ByteHeader` | R1000h, `SeekToSample(600)`, `Read(buf,0,16)` | `Format.DataOffset == 60`; `Position == 2400` after the seek; 16 bytes = (600,10600) (601,10601) (602,10602) (603,10603); `Position == 2416` | does not compile (no method) |
| W2 `SeekToSample_LastFrame` | R1000, `SeekToSample(999)` | `Read` returns 4 bytes (999,10999), then 0; `IsEndOfStream` | compile |
| W3 `SeekToSample_RejectsOutOfRange` | R1000, -1 and 1000 | `ArgumentOutOfRangeException` both | compile |
| W4 `SeekToSampleZero_EqualsRewind` | R1000 read to the end, then `SeekToSample(0)` vs `Rewind()` | same 4000 bytes, `Position == 0` | compile |
| W5 mono | 1000 mono frames, value f, `SeekToSample(600)` | `Position == 1200`, the next 2 bytes = 600 | compile |
| W6 allocation | the `Read_DoesNotAllocate` pattern (`WavStreamReaderTests.cs:144-163`) with `SeekToSample(k)` instead of `Rewind` | 0 bytes | compile |

### 3.2 MusicPlayer loop start (through `AudioService.Music`: 16384-B buffers, target 3, voice `(0,0)`)
- **L1** looped R1000, `LoopStartSample = 600`:
  - After `Play`: 3 buffers of **4000, 1600 and 1600 B** = frames `[0..999]`, `[600..999]`, `[600..999]`.
  - After `ConsumeBuffers(voice,3)` and `service.Update(0.016f)`: 6 buffers, the last three 1600 B `[600..999]`.
  - Concatenation over 3000 frames: `frame(i) = i` for i < 1000, else `600 + (i-1000) mod 400`, on both channels
    (R = 10000 + that).
  - Behavioural red: the property exists but is ignored, which gives `[0..999]` × 3 (4000 B each). The first failure
    is at index 1000: expected 600, got 0.
- **L2**, a buffer straddling the seam: `new MusicPlayer(service, bufferSizeInBytes: 1024, queuedBufferTarget: 3)`
  (256 frames), same asset, driven by `player.Update`.
  - After `Play`: `[0..255] [256..511] [512..767]`.
  - After two rounds of `ConsumeBuffers(3)` and `Update`: 9 buffers, sizes
    `1024,1024,1024,928,1024,576,1024,576,1024` = `[768..999] [600..855] [856..999] [600..855] [856..999] [600..855]`.
  - Concatenation over 2056 frames matches the same formula.
- **L3**, default unchanged: R1000 looped with `LoopStartSample` unset (0) gives 3 × 4000 B `[0..999]`, then 3 more.
  The existing `ALoopingTrack_RewindsInsteadOfEnding` (`MusicPlayerTests.cs:100-118`) stays.
- **L4**, a non-looped track ignores the loop start: R1000, `IsLooped = false`, `LoopStartSample = 600`. `Play`
  submits 1 buffer of 4000 B `[0..999]` and the track is finishing. After `ConsumeBuffers(1)` and `Update`,
  `IsAlive` is false and `ActiveTrackCount == 0`.
- **L5**, the out-of-range fallback (design choice to confirm): `LoopStartSample = 1000`, and also 5 000 000.
  `Play` is valid and the frames are `[0..999]` × 3. The warning is logged (not asserted, as with the existing
  throttled logs).
- **L6**, the non-44-byte header through the player: R1000h, looped, 600. Same expected frames as L1. A naive
  offset gives 596 at index 1000.
- **L7**, allocation: the `Update_DoesNotAllocateWhileStreaming` pattern (`MusicPlayerTests.cs:300-321`) with a
  65536-sample looped file and `LoopStartSample = 32768`. 200 updates × 2 buffers cross the seam many times, and
  0 bytes are allocated.

### 3.3 Per-call bound (`backend.ReportsZeroPendingBuffers = true`)
- **B1**, not looped, so there is no hang today. `CreateStreamingAsset(provider)` (65536 samples, 22050 Hz, 16
  buffers).
  - After `Play`: `GetSubmittedBytes(voice)` must be **49152** (3 buffers). Today it is **262144** (16 buffers), so
    the test is red.
  - After one `service.Update`: **98304**.
- **B2**, looped R1000 with loop start 600. Today `Play` never returns. Expected: 3 buffers `[0..999] [600..999]
  [600..999]`, then 6 after one `Update`.
  - Run the red with a watchdog: `var t = Task.Run(() => service.Music.Play(asset)); Assert.True(t.Wait(TimeSpan.FromSeconds(10)))`.
    Today's spinning thread is a thread-pool thread and does not block the process from exiting.
  - Also run `dotnet test --blame-hang-timeout 60s` (memory "Pièges des exécuteurs").

### 3.4 SoundAsset and serializer
- **S1** `Load` with `"loop_start_sample": 5344920` gives `5344920L`. **S2**: a missing key gives 0. **S3**: −5
  gives 0. All in `SoundAssetTests.cs`.
- **S4** round trip: extend `SaveThenLoad_KeepsEveryField` (`SoundAssetEditorSerializationTests.cs:10-38`) with
  `LoopStartSample = 5344920`, which is a non-default value.
- **S5**: `TrySerialize(new SoundAsset())` has no `loop_start_sample` key. With 5344920, the key is present with
  that value.
- Red: compile, then behaviour. If `SaveSoundAsset` is not changed, S4 fails with 0 instead of 5344920. That is the
  pitfall the test exists for.

### 3.5 Mutations (each must turn a test red)
- `SeekToSample` ignoring `DataOffset` (`44 + …`): W1 and L6, which see 596.
- `FillQueue` back to `Rewind()`: L1, L2, L6 and B2.
- Bound removed: B1 (262144) and B2 (timeout).
- Serializer key not written: S4.
- Key written even at 0: S5.
- Off-by-one in the range check (`<=`): W3.
- Loop start honoured on non-looped tracks: L4.

### 3.6 Existing tests that move
- **With the loop start and the bound: none.**
  - `MusicPlayerTests` (19 facts): the fake increments pending, so the bound is never reached. Loop start 0 is the
    same bytes.
  - `WavStreamReaderTests`: `Rewind` is kept.
  - `SoundAssetTests` and `SoundAssetEditorSerializationTests`: the new key is absent by default; the existing key
    test checks presence only.
  - `AudioServiceStereoVoiceTests`, `AudioServiceStreamingTests` and the cutscene and emitter tests: no
    `RegisterStream` outside `MusicPlayerTests` (`rg`).
- **If the queue default changes (section 4):**
  - `MusicPlayerTests.Play_StartsAStreamAndQueuesAhead` (`:38-53`): asserts 3. With 24, the current 65536-sample
    file holds only 16 buffers, so the test would see 16.
  - `Update_TopsTheQueueBackUpAsBuffersAreConsumed` (`:68-81`): 1 then 3, which becomes 22 then 24.
  - The `SampleCount` constant and its comment (`:9-11`).
  - Write them against `MusicPlayer.DefaultQueuedBufferTarget`, with `SampleCount ≥ (target + 2) × 4096`.
- Parent repository:
  - `Alundra.Tests` (2570) does not move: no DLL code calls `MusicPlayer` or `IsStreaming` (`rg` over
    `Alundra/`), and the DLL plays music through `PlayClip` (`AlundraMusicPlayer.cs:384-385`).
  - The converter tests do not move.

## 4. Streaming starvation

### 4.1 Today's margin (`fillsim.out`)
| stream | per 16 KiB buffer | target 3, best (3 buffers) | target 3, worst (just over 2) |
|---|---|---|---|
| 44.1 kHz stereo 16-bit (Alundra BGM) | 92.88 ms | **278.6 ms** | **185.8 ms** |
| 22.05 kHz stereo 16-bit (engine demo music) | 185.76 ms | 557.3 ms | 371.5 ms |

- Worst case: the refill sees 3 pending buffers while the playing one is almost finished, because MonoGame counts it
  until it is processed (`mg/DynamicSoundEffectInstance.cs:315-318`, `:385-401`). The class doc's "roughly half a
  second" (`MusicPlayer.cs:25`) holds only at 22 kHz.

### 4.2 What happens in a stall (order of operations, MonoGame backend)
1. Frame N-1: audio is updated last, so `FillQueue` tops up to 3 (`ComponentOrder.cs:22-24`). Then Draw and present
   follow (vsync on, `AlundraGame.json:19`).
2. Frame N: `FrameworkDispatcher.Update` unqueues the processed buffers (`mg/Game.cs:1004`). Then
   `GameManager.UpdateWorld` loads the world **synchronously**: `LoadCopy<World>`, `LoadContent` and `BeginPlay`
   (`GameManager.cs:98-144`, called from `CasaEngineGame.cs:555`).
3. Once the queue has run dry, the OpenAL source stops on its own. The pending count is **stale** inside frame N:
   nothing unqueues it until the next `FrameworkDispatcher.Update`. So the audio `FillQueue` that runs after the
   stall in frame N still sees 3 and submits nothing.
4. Frame N+1: `UpdateQueue` drops the 3 processed buffers, `FillQueue` submits 3, and `PlatformSubmitBuffer`
   restarts the source (`:364-369`).
5. Audible gap ≈ stall − margin + about one frame. With `IsFixedTimeStep` false, the next frame's elapsed time is
   clamped to 500 ms (`mg/Game.cs:315`, `:783-786`); this does not affect audio.
- With `chantier/audio-modern`'s software backend, step 3 does not apply: the count drains on every call. The stall
  itself still starves the stream, because the stream is fed only by the game thread until its S2.

### 4.3 Evidence of stall durations (all that exists in the repository)
- No frame-time log or profiler output was found (`rg` over `Alundra/`, `docs/` and the engine). The only timing
  source is the launcher log `CasaEngineMonogame/CasaEngine.Launcher/bin/Debug/net9.0-windows/log.txt`, from a
  2026-10-05 session.
  - It is a Debug build at Trace verbosity: every asset load writes a line through `FileLogger` with `AutoFlush`
    (`CasaEngine/Core/Logging/FileLogger.cs:15-16`).
  - Its timestamps have **1-second resolution** (`DateTime.Now:T`, `:29`).
- `warplog.py` and `warplog.out`, read with the timestamp-transition listings, give these spans (the burst is the
  run of lines right after `World change:`):
  - 389→390 (160 loads): 4 times, 0–2 s each.
  - 390→389 (51 loads): 4 times, 0–2 s.
  - 476→478 (`0x53`): **15:51:46 → 15:51:48, so more than 1 s**. It holds 1245 loads plus the first-frame wiring and
    the resident `Musics\bgm_030.wav` load (log line 4887). The track changes here, so this is a starvation example
    only if a stream is playing.
  - 478→476 (12 loads): 0–1 s.
  - 391 (267 loads): 0–2 s.
  - 162 (654 loads): 0–2 s.
  - The startup load of 389 took more than 2 s (15:48:10 → 15:48:13), but music starts only after it (B10), so it
    is harmless.
  - The heuristic's 14-second and 22-second "spans" are mixed bursts that include in-game lazy loads
    (`sfx_*.wav`, entities), checked by eye. They are not load stalls.
- `docs/plan-e11c-musique.md:120-121` gives the resident route's measured load: 46 ms for a 20.4 MiB BGM. It
  doubles under option B, and it is a stall of its own on a track change.
- **Not established:** the duration of a single frame in Release, and the split of the 478 burst between
  `UpdateWorld` and the first frames.

### 4.4 When the music keeps playing through a load
- `AlundraWarpDirector.Advance` requests the world only after the screen fade has settled
  (`AlundraWarpDirector.cs:613-623`).
- The arrival's `PlayMapMusic` returns early when the raw index is unchanged: "B10: total short-circuit on 0, or the
  raw index already current" (`AlundraMusicPlayer.cs:210-212`). The voice plays on through the load.
- So every warp between maps that share a track runs a synchronous load under live music:
  - 389↔390 (track 25);
  - track 7 (20 maps);
  - track 16 (137 maps);
  - track 21 (56 maps);
  - track 30 (48 maps).

  The counts come from `loop-disc/notes.md` §2.
- The master fade machine acts only when it is armed by `0xA6` (`AlundraBgmFadeDirector.cs` doc, `:7-60`).
- Today's resident looped clip cannot starve. **L-b moving to streaming introduces the risk.** L-a alone changes
  nothing audible in Alundra.

### 4.5 Options and recommendation
- **(a) Deeper default queue: recommended for L-a, or as its own task with its own decision.**
  `DefaultQueuedBufferTarget` goes from 3 to **24**, the buffer size stays 16 KiB, and the per-call bound equals the
  target. The margins are:
  - 44.1 kHz: best **2229.1 ms**, worst **2136.2 ms**;
  - 22.05 kHz: 4.46 s / 4.27 s.

  Costs:
  - 384 KiB of OpenAL buffers per track (768 KiB during a crossfade);
  - a 384 KiB synchronous read at `Play`;
  - no fade latency, because fades act on the voice volume, not on the queued data.

  Variants:
  - 23 buffers keeps the worst case just above 2 s (2043 ms).
  - A duration-based target per track, `ceil(seconds × BytesPerSecond / bufferSize) + 1`, keeps the same seconds
    at every rate. It is more code.
  - 64 KiB × 7 gives best 2.60 s / worst 2.23 s, with 4 times fewer submits (MonoGame allocates one
    `OALSoundBuffer` per submit).
- **(b) Top-up before a world load**, if measured stalls exceed about 2 s. `GameManager.UpdateWorld` calls an
  `AudioService` hook before `Clear()` (`GameManager.cs:98-103`). Every music track is then topped up to a
  "stall depth" (for example 5 s, 860 KiB). The count is fresh at that point, because `FrameworkDispatcher.Update`
  ran just before. This is new public API and a coupling from GameManager to audio, so it is an author decision.
- **(c) Off-game-thread reads.** This is the structural fix. It belongs to `chantier/audio-modern` S2. It cannot be
  done on MonoGame's `DynamicSoundEffectInstance`: its `_queuedBuffers` is a plain `Queue<>` mutated by
  `UpdateQueue` on the game thread (`mg/DynamicSoundEffectInstance.cs:363`, `:385-401`).
- **Preloading the file into memory does not help.** Starvation depends only on the audio already submitted. It
  would also cost up to 76 MiB per track after option B.
- **Recommendation:**
  - Take (a) at 24, but only after the measurement of 4.6 confirms that stalls of maps sharing a track stay below
    about 2 s in Debug.
  - Keep (b) as the fallback.
  - Leave (c) to S2.
  - Record the margin in the ADR and in `audio-system.md` §9.

### 4.6 Measurement (premise check, main session, later)
- An out-of-repo harness on the exported project, following the precedent of memory "Capture d'écran" and
  `archive/audio-stereo-voices-mute-tasks.md:115-118`. It times each `Game.Tick` with a `Stopwatch` and logs the maximum
  frame time per world change, in Debug and in Release. Routes:
  - 389→390→389 (track 25 kept);
  - two track-7 overworld maps;
  - two track-16 maps.
- Pass criterion for (a): the maximum warp frame time plus one frame stays below 2136 ms.

## 5. Demo and device check (engine rules)

- **Rules.** Engine `AGENTS.md:44` (§6): a minimal demo is required for a non-trivial visible feature. When a sample
  exists for the area, it must be run at least once. `audio-system.md:256-257` says real playback is not covered by
  tests and goes through the demo. Precedents:
  - `audio-system-tasks.md` M6.4 (`:498-514`): "validation à l'oreille du streaming … boucle propre au bout de
    4 min 41", left 🧪 for the author.
  - The B key of ADR-0039: the demo is run from `CasaEngine.Demos/`, with a back-buffer capture and a check of the
    log, and listening is left to the author (`archive/audio-stereo-voices-mute-tasks.md:111-114`).
- **Proposal.** The existing demo music cannot show the seam quickly: it runs 280.75 s at 22.05 kHz (`wavinfo.out`).
  1. A demo WAV `Content/Audio/loop_point_demo.wav`, generated deterministically by a script at execution time:
     - 44100 Hz stereo 16-bit, 132 300 frames (3.0 s), written with an 18-byte `fmt ` and a `LIST` chunk, so its
       data offset is 60. That exercises a non-44-byte header on a real `FileStream`.
     - Frames [0, 44100) hold a 440 Hz sine with a 10 ms fade in and out.
     - Frames [44100, 132300) hold four 660 Hz beeps: 0.25 s on (11 025 frames), then 0.25 s off.
     - Size 529 260 B.
  2. `loop_point_demo.sound`:
     - `is_streaming` true, `is_looped` true, `bus_name` Music, `volume` 0.8, `loop_start_sample` 44100;
     - plus its entries in `AssetInfos.json` and `Content.mgcb` (`/copy`), like the existing `racing_game_music*.sound`
       (`Content.mgcb:25-32`, `AssetInfos.json:1161-1184`).
  3. Key `O` in `AudioDemo`: play or stop it. The HUD line shows its `GetPosition` and its queue. Expected sound:
     the 440 Hz tone **once**, then beeps every 0.5 s forever, with no gap or click. The position jumps back from
     about 3.0 to 1.0 s. With the old behaviour, the 440 Hz tone would come back every 3 s. The existing `P` and `C`
     keys give the default-unchanged check: racing music at loop start 0.
  4. Optional key `H`: a deliberate game-thread hitch with `Thread.Sleep` of 0.5, 2.0 or 2.5 s, cycling. One frame
     later it logs the music queue count, where 0 means starved. This gives the agent objective starvation
     evidence on the device, without listening:
     - at target 3, the 44.1 kHz demo track starves at 0.5 s;
     - at target 24, it should survive 2.0 s and starve at 2.5 s.
- **Run procedure:** working directory `CasaEngine.Demos/` (memory "Lancer les démos CasaEngine"), a back-buffer
  capture, and `log.txt` checked. Listening stays 🧪 for the author.
- **The in-game recipe is L-b's.** Map 389: the intro (22.6 s) once, then the loop every 98.6 s, with no intro
  replay. It also includes a 389↔390 warp with no gap.
- `chantier/audio-modern` also edits `AudioDemo.cs`: integrate it late or rebase.

## 6. Open questions (direction-changing; for the main session and the author)
1. **Overlap with `chantier/audio-modern` S2**, which already lists "points de boucle" and off-thread reads. Options:
   - Do L-a now on the E19 stack. The ADR then names `SoundAsset.LoopStartSample` and `SeekToSample` as the
     loop-point API that S2 keeps.
   - Fold L-a into S2.

   Recommendation: do it now. It is small, sits above `IAudioBackend`, and the core files do not overlap.
2. **Queue depth**: 3 → 24 in L-a, before or after the measurement of 4.6? Or option (b)?
3. **An out-of-range loop start**: fall back to 0 with a warning (proposed), or refuse the track?
4. **An inspector row for the field**: add it now, or defer (ADR-0002 scope)?
5. **Demo content**: is a new 529 KB generated WAV acceptable in `CasaEngine.Demos/Content/Audio/`? The alternative
   is an in-memory provider decorator in the demo, which skips the `FileStream` path.
6. **Base and ADR number**: stack on `a6efd2a9`, and take ADR-0056 after a re-scan. Integration with `main`
   `3e0770d6` and with `audio-modern` comes later.

## 7. Scripts and outputs (this folder)
| file | role |
|---|---|
| `fillsim.py` / `fillsim.out` | Model of `FillQueue` and `WavStreamReader.Read`, today and as proposed, against the fake: the expected buffers and frames for L1-L6 and B1-B2, the header offsets, the queue margins, the option-B sizes and the loop-start values. |
| `warplog.py` / `warplog.out` | Scan of the launcher's Debug log for world-change bursts (1-second bounds). |
| `wavinfo.py` / `wavinfo.out` | Chunk layout and envelope of the demo WAVs and of `bgm_045.wav`. |
| `mg/DynamicSoundEffectInstance.cs`, `mg/FrameworkDispatcher.cs`, `mg/DynamicSoundEffectInstanceManager.cs`, `mg/Game.cs` | `ilspycmd` decompile of MonoGame.Framework.DesktopGL 3.8.5.1 (NuGet cache). |
| `audio-modern-tasks.md`, `SoftwareAudioBackend.cs` | Read-only copies (`git show`) from `chantier/audio-modern` `c4e003e9`. |
