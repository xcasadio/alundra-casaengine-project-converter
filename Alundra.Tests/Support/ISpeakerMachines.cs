#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>The two machines a raw sequence drives: the oracle's, or the DLL's (through an adapter). The strings of the passes are the digest cells of <c>gen_sequences.py</c>.</summary>
internal interface ISpeakerMachines
{
    int NameFlags { get; }

    bool NameSlot { get; }

    int PortraitState { get; }

    bool NameOpen(int id);

    void NameClose();

    bool PortraitOpen(RawEntity entity, (int X, int Y) cam, int height);

    void PortraitClose(RawEntity entity, (int X, int Y) cam);

    /// <summary>"frameX|textX", or null when nothing is drawn.</summary>
    string? NamePass();

    /// <summary>"x|y|w|h|rgb|phase", or null when nothing is drawn.</summary>
    string? PortraitPass();
}
