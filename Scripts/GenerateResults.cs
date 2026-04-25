#:project ../Leda.Lang/Leda.Lang.csproj
#:project ../Leda.Test/Leda.Test.csproj

using Leda.Lang;
using Leda.Test;

// Assumes the script's working directory is the solution's root.
const string ledaTestPath = "Leda.Test/";

var filename = args[0];

var testCodePath = Path.Join(ledaTestPath, "tests", filename);
var testResultPath = Path.Join(ledaTestPath, "results", filename + ".diagnostics");

var testCode = File.ReadAllText(testCodePath);
var source = new Source(filename, testCode);
var diagnostics = new List<Diagnostic>();
diagnostics.AddRange(source.Parse());
diagnostics.AddRange(source.Bind());
diagnostics.AddRange(source.Check());

var diagnosticsOutput = DiagnosticPrinter.DiagnosticsOutput(diagnostics);
File.WriteAllText(testResultPath, diagnosticsOutput);

Console.WriteLine("Generated results file at " + testResultPath);