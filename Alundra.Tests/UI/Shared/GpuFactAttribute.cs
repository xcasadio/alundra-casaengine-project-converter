#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using CasaEngine.Framework.UI.Backend.MonoGame;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.FontStashSharp;
using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Alundra.UI.Choice;
using Alundra.UI.Inventory;
using Alundra.UI.Messages;
using Alundra.Tests.Support;

namespace Alundra.Tests.UI.Shared;

/// <summary>Like <see cref="FactAttribute"/>, but skipped (not failed) when no real <see cref="GraphicsDevice"/> can be created in this process (a headless agent): the pattern of the
/// engine's own <c>GpuFactAttribute</c>.</summary>
public sealed class GpuFactAttribute : FactAttribute
{
    public GpuFactAttribute()
    {
        if (!TextBoxGpu.IsAvailable)
        {
            Skip = TextBoxGpu.UnavailableReason;
        }
    }
}
