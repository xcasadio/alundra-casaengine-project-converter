namespace AlundraCasaEngineProjectConverter;

public sealed class CliOptions
{
    public required string InputDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public int Phase { get; init; }
    public IReadOnlyList<int>? MapFilter { get; init; }

    public static CliOptions? Parse(string[] args)
    {
        var positional = new List<string>();
        var phase = int.MaxValue;
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
                    mapFilter = args[++i]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(int.Parse)
                        .ToList();
                    break;

                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count < 2)
        {
            Console.Error.WriteLine(
                "Usage: alundra-casaengine-project-converter <inputDir> <outputDir> [--maps 0,4,10] [--phase N]");
            return null;
        }

        return new CliOptions
        {
            InputDirectory = positional[0],
            OutputDirectory = positional[1],
            Phase = phase,
            MapFilter = mapFilter,
        };
    }
}
