#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.e0c T1: reads the vectors of the oracle that T0 versioned under <c>docs/plan-e20-annexe/e20-e0c/exec/vectors/</c> (produced by the real code of the binary, see that folder's README). The folder is found above the
/// test binaries, the way <see cref="ProjectRootFinder"/> finds the exported project.
/// </summary>
internal static class AnnexFiles
{
    private const string VectorsFolder = "docs/plan-e20-annexe/e20-e0c/exec/vectors";

    public static string Vector(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, VectorsFolder, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no '{VectorsFolder}/{name}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.e T1: a file of the annex <c>docs/plan-e20-annexe/e20-e/rev3/</c> (the vectors and the outputs of the oracle of the cut of the grass), found above the test binaries.</summary>
    public static string CutAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-e/rev3", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-e/rev3/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.g5a T1: a file of the annex <c>docs/plan-e20-annexe/e20-g5/disc/</c> (the goldens of C76 that the discovery made from the real code of the binary), found above the test binaries.</summary>
    public static string PillarAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-g5/disc", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-g5/disc/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.g5b T1: a file of the annex <c>docs/plan-e20-annexe/e20-g5/g5b/</c> (the value vectors of the opcodes 0x72, 0xA9 and 0xAA taken from the annexes of E19.sm3 and E19.sm4), found above the test binaries.</summary>
    public static string ButtonAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-g5/g5b", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-g5/g5b/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.g2c1 T1: a file of the annex <c>docs/plan-e20-annexe/e20-g2c/disc/</c> (the 1 244 goldens of C4 that the discovery of 2026-10-22 made from the real code of the binary), found above the test binaries.</summary>
    public static string C4Annex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-g2c/disc", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-g2c/disc/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.e0d T2: a file of the annex <c>docs/plan-e20-annexe/e20-e0d-disc/</c> (the 244 vectors of F255 and the outputs of the oracles of the chest that the discovery of 2026-10-24 made from the real code of the binary), found above the test binaries.</summary>
    public static string ChestAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-e0d-disc", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-e0d-disc/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.h3a T1: a file under <c>docs/plan-e20-annexe/</c> given by its path from that folder (the goldens of D30 and E12 of the critic of h3, <c>e20-h3/critic/results/goldens_critic.json</c>, and those of E19, <c>e20-h3a/goldens_e19.json</c>), found above the test binaries.</summary>
    public static string BombAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.g2c2 T1: a file of the annex <c>docs/plan-e20-annexe/e20-g2c2/</c> (the 90 vectors of C70, E20 and D34 that T0 made from the real code of the binary, <c>vectors/goldens_g2c2.json</c>), found above the test binaries.</summary>
    public static string TorchAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-g2c2", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-g2c2/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.h4c1 T1: a file of the annex <c>docs/plan-e20-annexe/e20-h4c1/</c> (the keys, the vectors and the table of the earth tome 1), found above the test binaries.</summary>
    public static string EarthTome1Annex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-h4c1", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-h4c1/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.h4c2 T1: a file of the annex <c>docs/plan-e20-annexe/e20-h4c2/</c> (the keys, the cases of the driver and the table of the earth tome 2), found above the test binaries.</summary>
    public static string EarthTome2Annex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe/e20-h4c2", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/e20-h4c2/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.h4d1 T1: a file of the annex <c>docs/plan-e20-annexe/e20-h4d1/</c> (the faithful keys of the fire tome 2 in <c>critic/keys_faithful/</c>, the vectors of E28 replayed by T0 in <c>t0/vec/</c>), found above the test binaries.</summary>
    public static string FireTome2Annex(string relative) => WaterDropAnnex("e20-h4d1/" + relative);

    /// <summary>E20.h4c2 T1: a vector file of the h4b bricks (<c>docs/plan-e20-annexe/e20-h4b/bin/vec/</c>: E13, the camera step), the real code of the binary replayed from an empty memory.</summary>
    public static string H4bVector(string name) => WaterDropAnnex("e20-h4b/bin/vec/" + name);

    /// <summary>The parsed vector file; the caller disposes it.</summary>
    public static JsonDocument ReadVector(string name) => JsonDocument.Parse(File.ReadAllText(Vector(name)));

    /// <summary>E20.g2c3 T1: a file of the annex <c>docs/plan-e20-annexe/</c> (the goldens of A19 and C71 in <c>e20-g2c3-disc/g2c3/replay/</c>, the four vectors of the state 2 in <c>e20-g2c3/vectors/</c>), found above the test binaries.</summary>
    public static string WaterDropAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs/plan-e20-annexe", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AnnexFiles: no 'docs/plan-e20-annexe/{relative}' found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>E20.g1d T1: a file of the annex <c>docs/plan-e20-annexe/e20-g1d/keys/</c> (the keys of the sort driver of the binary, replayed twice in T0), found above the test binaries.</summary>
    public static string DrawOrderKeys(string name) => WaterDropAnnex("e20-g1d/keys/" + name);
}
