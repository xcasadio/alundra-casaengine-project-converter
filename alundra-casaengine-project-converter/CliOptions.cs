namespace AlundraCasaEngineProjectConverter;

public sealed class CliOptions
{
    public required string InputDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public int Phase { get; init; }
    public IReadOnlyList<int>? MapFilter { get; init; }

    /// <summary>
    /// Whether the run ends by loading every generated asset back through its engine class. On by
    /// default: it is the safety net that catches assets which would otherwise only fail in the
    /// editor. --no-verify turns it off for fast iteration.
    /// </summary>
    public bool Verify { get; init; } = true;

    public static CliOptions? Parse(string[] args)
    {
        var positional = new List<string>();
        var phase = int.MaxValue;
        var verify = true;
        List<int>? mapFilter = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--phase":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out phase))
                    {
                        Console.Error.WriteLine("--phase requires an integer value.");
                        return null;
                    }
                    break;

                case "--maps":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--maps requires a comma-separated list of map indices.");
                        return null;
                    }
                    mapFilter = new List<int>();
                    foreach (var token in args[++i]
                                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        // Same courtesy --phase already extends: a typo gets a message, not a stack trace.
                        if (!int.TryParse(token, out var mapIndex))
                        {
                            Console.Error.WriteLine($"--maps expects integer map indices; '{token}' is not one.");
                            return null;
                        }

                        mapFilter.Add(mapIndex);
                    }

                    break;

                case "--no-verify":
                    verify = false;
                    break;

                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count < 2)
        {
            Console.Error.WriteLine(
                "Usage: alundra-casaengine-project-converter <inputDir> <outputDir> [--maps 0,4,10] [--phase N] [--no-verify]");
            return null;
        }

        return new CliOptions
        {
            InputDirectory = positional[0],
            OutputDirectory = positional[1],
            Phase = phase,
            MapFilter = mapFilter,
            Verify = verify,
        };
    }
}
