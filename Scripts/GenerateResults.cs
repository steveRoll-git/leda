#:project ../Leda.Lang/Leda.Lang.csproj
#:project ../Leda.Test/Leda.Test.csproj
#:package Microsoft.Extensions.FileSystemGlobbing@10.0.7

using Leda.Lang;
using Leda.Test;
using Microsoft.Extensions.FileSystemGlobbing;

// Assumes the script's working directory is the solution's root.
const string projectPath = "Leda.Test/";

var testFilesPath = Path.Join(projectPath, "tests");
var resultFilesPath = Path.Join(projectPath, "results");

var pattern = args[0];

Matcher matcher = new();
matcher.AddInclude(pattern);

foreach (var testCodePath in matcher.GetResultsInFullPath(testFilesPath))
{
    var filenameWithoutExtension = Path.GetFileNameWithoutExtension(testCodePath);
    var testDiagnosticsPath = Path.Join(resultFilesPath, filenameWithoutExtension + ".diagnostics");
    var testEmittedPath = Path.Join(resultFilesPath, filenameWithoutExtension + ".lua");

    var testCode = File.ReadAllText(testCodePath);
    var source = new Source(filenameWithoutExtension, testCode);
    var diagnostics = new List<Diagnostic>();
    diagnostics.AddRange(source.Parse());
    diagnostics.AddRange(source.Bind());
    diagnostics.AddRange(source.Check());

    var diagnosticsOutput = DiagnosticPrinter.DiagnosticsOutput(diagnostics);
    File.WriteAllText(testDiagnosticsPath, diagnosticsOutput);
    Console.WriteLine("Generated results file at " + testDiagnosticsPath);

    var emittedCode = Emitter.Emit(source.Chunk);
    File.WriteAllText(testEmittedPath, emittedCode);
    Console.WriteLine("Generated emitted file at " + testEmittedPath);
}