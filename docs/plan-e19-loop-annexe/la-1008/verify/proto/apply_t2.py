"""Applies T2 (depth 24) and the discovery's MusicPlayerTests edits to the PHYSICAL COPY given as argv[1]."""
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
assert "scratchpad" in str(root), root


def patch(rel, old, new):
    p = root / rel
    t = p.read_text(encoding="utf-8")
    n = t.count(old)
    if n != 1:
        sys.exit(f"{rel}: expected 1 match, got {n}: {old[:80]!r}")
    p.write_text(t.replace(old, new), encoding="utf-8")


patch("CasaEngine/Framework/Audio/Streaming/MusicPlayer.cs",
      "public const int DefaultQueuedBufferTarget = 3;", "public const int DefaultQueuedBufferTarget = 24;")
tests = "CasaEngine.Tests/Audio/MusicPlayerTests.cs"
patch(tests, "private const int SampleCount = 65536;", "private const int SampleCount = 106496;")
patch(tests, "        Assert.Equal(3, service.Music.GetPendingBufferCount(track));\n    }\n\n    [Fact]\n    public void Play_UsesTheFormatOfTheFile()",
      "        Assert.Equal(24, service.Music.GetPendingBufferCount(track));\n    }\n\n    [Fact]\n    public void Play_UsesTheFormatOfTheFile()")
patch(tests, "        Assert.Equal(1, service.Music.GetPendingBufferCount(track));\n\n        service.Update(0.016f);\n\n        Assert.Equal(3, service.Music.GetPendingBufferCount(track));",
      "        Assert.Equal(22, service.Music.GetPendingBufferCount(track));\n\n        service.Update(0.016f);\n\n        Assert.Equal(24, service.Music.GetPendingBufferCount(track));")
print("applied T2")
