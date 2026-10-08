#!/usr/bin/env python3
"""Generates CasaEngine.Demos/Content/Audio/loop_point_demo.wav (E19.L-a, D-E19-107).

Specification (docs/plan-e19-loop-annexe/engine/notes.md, section 5 "Demo"):
  * 44100 Hz, stereo, 16 bit PCM, 132 300 frames (3.0 s), the same signal on both channels;
  * a 18-byte "fmt " chunk and a 6-byte "LIST" chunk before "data", so the audio data starts at offset 60
    (a header that is not 44 bytes, read through a real FileStream);
  * frames [0, 44100): a 440 Hz sine with a 10 ms fade in and a 10 ms fade out (the intro, heard once);
  * frames [44100, 132300): four 660 Hz beeps, each 0.25 s on (11 025 frames) then 0.25 s off (the loop region);
  * total size 529 260 bytes; the loop start is frame 44100 (loop_point_demo.sound, key loop_start_sample).

The beeps get the same 10 ms fades as the intro so the seam (silence at the end of the data, the first beep
at the loop start) is click free. The output is deterministic: integer arithmetic around math.sin, no
randomness, no timestamp.

Usage: python gen_loop_point_demo.py <output.wav>
"""
import math
import struct
import sys

SAMPLE_RATE = 44100
CHANNELS = 2
BITS = 16
INTRO_FRAMES = 44100
BEEP_ON = 11025
BEEP_OFF = 11025
BEEP_COUNT = 4
TOTAL_FRAMES = INTRO_FRAMES + BEEP_COUNT * (BEEP_ON + BEEP_OFF)
AMPLITUDE = 16000.0
FADE_FRAMES = 441  # 10 ms
INTRO_HZ = 440.0
BEEP_HZ = 660.0
FMT_SIZE = 18
LIST_PAYLOAD = b"INFO\x00\x00"  # 6 bytes
DATA_OFFSET = 12 + (8 + FMT_SIZE) + (8 + len(LIST_PAYLOAD)) + 8


def envelope(index, length):
    """Linear fade in and fade out over FADE_FRAMES at both ends of a segment."""
    fade_in = min(1.0, index / FADE_FRAMES)
    fade_out = min(1.0, (length - 1 - index) / FADE_FRAMES)
    return min(fade_in, fade_out)


def frame_value(frame):
    if frame < INTRO_FRAMES:
        phase = 2.0 * math.pi * INTRO_HZ * frame / SAMPLE_RATE
        gain = envelope(frame, INTRO_FRAMES)
        return int(round(AMPLITUDE * gain * math.sin(phase)))
    local = frame - INTRO_FRAMES
    period = BEEP_ON + BEEP_OFF
    inside = local % period
    if inside >= BEEP_ON:
        return 0
    phase = 2.0 * math.pi * BEEP_HZ * inside / SAMPLE_RATE
    gain = envelope(inside, BEEP_ON)
    return int(round(AMPLITUDE * gain * math.sin(phase)))


def build():
    data = bytearray()
    for frame in range(TOTAL_FRAMES):
        value = frame_value(frame)
        data += struct.pack("<hh", value, value)
    block_align = CHANNELS * BITS // 8
    byte_rate = SAMPLE_RATE * block_align
    fmt = struct.pack("<HHIIHHH", 1, CHANNELS, SAMPLE_RATE, byte_rate, block_align, BITS, 0)
    assert len(fmt) == FMT_SIZE
    body = b"WAVE"
    body += b"fmt " + struct.pack("<I", FMT_SIZE) + fmt
    body += b"LIST" + struct.pack("<I", len(LIST_PAYLOAD)) + LIST_PAYLOAD
    body += b"data" + struct.pack("<I", len(data)) + bytes(data)
    wav = b"RIFF" + struct.pack("<I", len(body)) + body
    assert wav.index(b"data") + 8 == DATA_OFFSET == 60
    assert len(wav) == 529260, len(wav)
    return wav


def main():
    if len(sys.argv) != 2:
        sys.exit("usage: gen_loop_point_demo.py <output.wav>")
    wav = build()
    with open(sys.argv[1], "wb") as handle:
        handle.write(wav)
    print("wrote %s: %d bytes, data at offset %d, %d frames" % (sys.argv[1], len(wav), DATA_OFFSET, TOTAL_FRAMES))


if __name__ == "__main__":
    main()
