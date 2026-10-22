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

    /// <summary>The parsed vector file; the caller disposes it.</summary>
    public static JsonDocument ReadVector(string name) => JsonDocument.Parse(File.ReadAllText(Vector(name)));
}
