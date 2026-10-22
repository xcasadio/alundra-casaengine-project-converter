#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests.Support;

internal sealed class OracleSpeakerMachines : ISpeakerMachines
{
    private readonly OracleNameBox _name;
    private readonly OraclePortrait _portrait = new();

    public OracleSpeakerMachines(Func<int, int?> widthOf) => _name = new OracleNameBox(widthOf);

    public int NameFlags => _name.Flags;

    public bool NameSlot => _name.Slot;

    public int PortraitState => _portrait.State;

    public bool NameOpen(int id) => _name.Open(id);

    public void NameClose() => _name.Close();

    public bool PortraitOpen(RawEntity entity, (int X, int Y) cam, int height) => _portrait.Open(entity, cam, height);

    public void PortraitClose(RawEntity entity, (int X, int Y) cam) => _portrait.Close(entity, cam);

    public string? NamePass() => _name.Pass() is { } d ? $"{d.FrameX}|{d.TextX}" : null;

    public string? PortraitPass() => _portrait.Pass() is { } d ? $"{d.X}|{d.Y}|{d.W}|{d.H}|{d.Rgb}|{d.Phase}" : null;
}
