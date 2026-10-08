"""Applies a minimal T1 prototype (rules R1-R6 of the discovery) to the PHYSICAL COPY given as argv[1].

Never run against the repository. Each replacement must match exactly once, else the script stops.
A mutation name may be given as argv[2] to apply one mutation on top (see MUTATIONS).
"""
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
mutation = sys.argv[2] if len(sys.argv) > 2 else None
assert (root / "CasaEngine.Tests").is_dir() and "scratchpad" in str(root), root


def patch(rel, old, new):
    p = root / rel
    t = p.read_text(encoding="utf-8")
    n = t.count(old)
    if n != 1:
        sys.exit(f"{rel}: expected 1 match, got {n}: {old[:80]!r}")
    p.write_text(t.replace(old, new), encoding="utf-8")


# --- WavFormatInfo -----------------------------------------------------------
patch("CasaEngine/Framework/Audio/Streaming/WavFormatInfo.cs",
      "    public int BytesPerSecond => SampleRate * BlockAlign;",
      "    public int BytesPerSecond => SampleRate * BlockAlign;\n\n"
      "    public long SampleFrameCount => BlockAlign > 0 ? DataLength / BlockAlign : 0;")

# --- WavStreamReader.SeekToSample --------------------------------------------
seek_target = "Format.DataOffset + byteOffset"
if mutation == "no_data_offset":
    seek_target = "44 + byteOffset"
range_check = "frame < 0 || frame >= Format.SampleFrameCount"
if mutation == "range_off_by_one":
    range_check = "frame < 0 || frame > Format.SampleFrameCount"
patch("CasaEngine/Framework/Audio/Streaming/WavStreamReader.cs",
      """    public void Rewind()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _stream.Seek(Format.DataOffset, SeekOrigin.Begin);
        _dataBytesRead = 0;
    }""",
      f"""    public void Rewind() => SeekToSample(0);

    public void SeekToSample(long frame)
    {{
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if ({range_check})
        {{
            throw new ArgumentOutOfRangeException(nameof(frame));
        }}

        var byteOffset = frame * Format.BlockAlign;
        _stream.Seek({seek_target}, SeekOrigin.Begin);
        _dataBytesRead = byteOffset;
    }}""")

# --- SoundAsset ----------------------------------------------------------------
patch("CasaEngine/Framework/Audio/SoundAsset.cs",
      "    public bool IsStreaming { get; set; }\n",
      "    public bool IsStreaming { get; set; }\n\n"
      "    private long _loopStartSample;\n\n"
      "    public long LoopStartSample\n    {\n        get => _loopStartSample;\n"
      "        set => _loopStartSample = Math.Max(0L, value);\n    }\n")
if mutation == "float_read":
    load = '        LoopStartSample = (long)ReadNumber(element, "loop_start_sample", 0f);\n'
else:
    load = '        LoopStartSample = ReadInteger(element, "loop_start_sample", 0L);\n'
patch("CasaEngine/Framework/Audio/SoundAsset.cs",
      '        IsStreaming = element.ContainsKey("is_streaming") && element["is_streaming"].GetBoolean();\n',
      '        IsStreaming = element.ContainsKey("is_streaming") && element["is_streaming"].GetBoolean();\n' + load)
patch("CasaEngine/Framework/Audio/SoundAsset.cs",
      "    private void LoadVariationAudioFileAssetIds(JObject element)\n",
      """    private long ReadInteger(JObject element, string key, long defaultValue)
    {
        if (!element.TryGetValue(key, out var token))
        {
            return defaultValue;
        }

        if (token.Type == JTokenType.Integer)
        {
            try
            {
                return (long)token;
            }
            catch (OverflowException)
            {
            }
        }

        Logs.WriteWarning($"Sound asset '{Name}': key '{key}' is not an integer, the default is used.");
        return defaultValue;
    }

    private void LoadVariationAudioFileAssetIds(JObject element)
""")

# --- MusicPlayer ---------------------------------------------------------------
patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "        track.IsLooped = asset.IsLooped;\n",
      "        track.IsLooped = asset.IsLooped;\n"
      "        track.LoopStartSample = ResolveLoopStart(asset, reader);\n")
patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "            track.Channel = _worker.Acquire(reader, track.IsLooped, asset.Name);",
      "            track.Channel = _worker.Acquire(reader, track.IsLooped, track.LoopStartSample, asset.Name);")
fill_seek = "track.Reader.SeekToSample(track.LoopStartSample);"
if mutation == "fillqueue_rewind":
    fill_seek = "track.Reader.Rewind();"
patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "                track.Reader.Rewind();\n",
      f"                {fill_seek}\n")
patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "        track.IsLooped = false;\n        track.Generation++;",
      "        track.IsLooped = false;\n        track.LoopStartSample = 0;\n        track.Generation++;")
patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "        public bool IsLooped;\n        public bool IsFinishing;",
      "        public bool IsLooped;\n        public long LoopStartSample;\n        public bool IsFinishing;")
play_check = "asset.LoopStartSample < frameCount"
if mutation == "range_off_by_one":
    play_check = "asset.LoopStartSample <= frameCount"
patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "    private WavStreamReader OpenReader(SoundAsset asset)\n",
      f"""    private long ResolveLoopStart(SoundAsset asset, WavStreamReader reader)
    {{
        if (!asset.IsLooped || asset.LoopStartSample == 0)
        {{
            return 0;
        }}

        var frameCount = reader.Format.SampleFrameCount;
        if ({play_check})
        {{
            return asset.LoopStartSample;
        }}

        _log.WriteWarning($"Audio: music '{{asset.Name}}' has loop start {{asset.LoopStartSample}}, at or past its {{frameCount}} frames; it loops from the start.");
        return 0;
    }}

    private WavStreamReader OpenReader(SoundAsset asset)
""")

# --- StreamingWorker -----------------------------------------------------------
patch("CasaEngine/Framework/Audio/Streaming/StreamingWorker.cs",
      "    public Channel Acquire(WavStreamReader reader, bool isLooped, string name)",
      "    public Channel Acquire(WavStreamReader reader, bool isLooped, long loopStartSample, string name)")
patch("CasaEngine/Framework/Audio/Streaming/StreamingWorker.cs",
      "            channel.Reset(reader, isLooped, name);",
      "            channel.Reset(reader, isLooped, loopStartSample, name);")
patch("CasaEngine/Framework/Audio/Streaming/StreamingWorker.cs",
      "        internal void Reset(WavStreamReader reader, bool isLooped, string name)\n        {\n            _reader = reader;\n            _isLooped = isLooped;",
      "        internal void Reset(WavStreamReader reader, bool isLooped, long loopStartSample, string name)\n        {\n            _reader = reader;\n            _isLooped = isLooped;\n            _loopStartSample = loopStartSample;")
patch("CasaEngine/Framework/Audio/Streaming/StreamingWorker.cs",
      "        private bool _isLooped;\n",
      "        private bool _isLooped;\n        private long _loopStartSample;\n")
worker_seek = "_reader.SeekToSample(_loopStartSample);"
if mutation == "worker_rewind":
    worker_seek = "_reader.Rewind();"
patch("CasaEngine/Framework/Audio/Streaming/StreamingWorker.cs",
      "                        _reader.Rewind();\n",
      f"                        {worker_seek}\n")

# --- Editor serializer and preview copy ------------------------------------------
if mutation == "key_not_written":
    save = ""
elif mutation == "key_written_at_zero":
    save = '        node.Add("loop_start_sample", soundAsset.LoopStartSample);\n'
else:
    save = ('        if (soundAsset.LoopStartSample != 0)\n        {\n'
            '            node.Add("loop_start_sample", soundAsset.LoopStartSample);\n        }\n')
patch("CasaEngine.EditorServices/EditorAssetJsonSerializer.cs",
      '        node.Add("is_streaming", soundAsset.IsStreaming);\n',
      '        node.Add("is_streaming", soundAsset.IsStreaming);\n' + save)
patch("CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs",
      "    private static SoundAsset CreatePreviewCopy(SoundAsset source)",
      "    internal static SoundAsset CreatePreviewCopy(SoundAsset source)")
copy = "" if mutation == "no_preview_copy" else "            LoopStartSample = source.LoopStartSample,\n"
patch("CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs",
      "            IsStreaming = source.IsStreaming,\n            BusName = AudioBusNames.Editor,",
      "            IsStreaming = source.IsStreaming,\n" + copy + "            BusName = AudioBusNames.Editor,")

# --- remove the per-call cap (mutation only) ------------------------------------
if mutation == "no_cap":
    patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
          "             submitted < _queuedBufferTarget && !track.IsFinishing && _service.GetPendingBufferCount(track.Voice) < _queuedBufferTarget;",
          "             !track.IsFinishing && _service.GetPendingBufferCount(track.Voice) < _queuedBufferTarget;")

print("applied", mutation or "T1")
