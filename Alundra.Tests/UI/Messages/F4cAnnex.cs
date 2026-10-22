#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Dialogues;
using Alundra.Dialogues.Yarn;
using Alundra.UI.Inventory;
using Alundra.UI.Messages;
using Alundra.UI.Shared;
using Alundra.Tests.Support;
using Alundra.Tests.Dialogues;

namespace Alundra.Tests.UI.Messages;

/// <summary>The folder <c>docs/plan-e19-f4c-annexe</c> (the prediction of E19.f4c2: states, classes, references, digests), searched upward from the test binaries.</summary>
internal static class F4cAnnex
{
    public static string Directory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f4c-annexe");
            if (File.Exists(Path.Combine(candidate, "classes.tsv")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("docs/plan-e19-f4c-annexe not found above " + AppContext.BaseDirectory);
    }

    public static string File_(string name) => Path.Combine(Directory(), name);
}
