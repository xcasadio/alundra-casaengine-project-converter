using AlundraCasaEngineProjectConverter;
using AlundraCasaEngineProjectConverter.Writers;

var options = CliOptions.Parse(args);
if (options is null)
{
    return 1;
}

if (!Directory.Exists(options.InputDirectory))
{
    Console.Error.WriteLine($"Input directory not found: {options.InputDirectory}");
    return 1;
}

Console.WriteLine("Start conversion...");
Console.WriteLine($"Input : {options.InputDirectory}");
Console.WriteLine($"Output: {options.OutputDirectory}");

var report = new ConversionReport();

// Phase 0: bootstrap an empty CasaEngine project.
if (options.Phase >= 0)
{
    ProjectWriter.CreateEmptyProject(options.OutputDirectory, report);
}

report.Save(Path.Combine(options.OutputDirectory, "report.json"));
report.PrintSummary();

return report.Errors.Count > 0 ? 1 : 0;


