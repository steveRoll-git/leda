using Leda.Lang;

namespace Leda;

internal class Program
{
    private const string UsageText = """
                                     Usage:
                                     leda gen <path>
                                     """;

    private static void PrintUsageAndExit()
    {
        Console.WriteLine(UsageText);
        Environment.Exit(1);
    }

    private static int _errorCount = 0;
    private static readonly HashSet<Source> ErrorSources = [];

    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No subcommand specified.");
            PrintUsageAndExit();
        }

        var verb = args[0];
        CliVerb action;
        if (verb == "gen")
        {
            action = CliVerb.Gen;
        }
        else
        {
            Console.WriteLine($"Invalid subcommand '{verb}'.");
            PrintUsageAndExit();
            return;
        }

        if (args.Length < 2)
        {
            Console.WriteLine("A path to a project directory must be given.");
            Environment.Exit(1);
        }

        var path = args[1];
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"There is no directory at '{path}'.");
            Environment.Exit(1);
        }

        var project = Project.FromFilesInDirectory(path);
        project.CheckAll((source, diagnostics) =>
        {
            foreach (var diagnostic in diagnostics)
            {
                ConsoleReporter.Report(source, diagnostic);
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    _errorCount += 1;
                    ErrorSources.Add(source);
                }
            }
        });

        if (_errorCount > 0)
        {
            // TODO add option to generate files despite errors?
            Console.WriteLine($"{_errorCount} errors were found in {ErrorSources.Count} files. No code generated.");
            Environment.Exit(1);
        }

        foreach (var source in project.Sources)
        {
            var outPath = Path.ChangeExtension(source.Path, ".lua");
            Console.WriteLine("Emitting " + outPath);

            var outCode = Emitter.Emit(source.Tree);
            File.WriteAllText(outPath, outCode);
        }

        Console.WriteLine($"{project.Sources.Count} files emitted.");
    }

    private enum CliVerb
    {
        Gen
    }
}