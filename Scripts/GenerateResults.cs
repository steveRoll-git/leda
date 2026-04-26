#:project ../Leda.Lang/Leda.Lang.csproj
#:project ../Leda.Test/Leda.Test.csproj

using Leda.Lang;
using Leda.Test;

// Assumes the script's working directory is the solution's root.
const string ledaTestPath = "Leda.Test/";

var filename = args[0];
var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

var testCodePath = Path.Join(ledaTestPath, "tests", filename);
var testDiagnosticsPath = Path.Join(ledaTestPath, "results", filenameWithoutExtension + ".diagnostics");
var testEmittedPath = Path.Join(ledaTestPath, "results", filenameWithoutExtension + ".lua");

var testCode = File.ReadAllText(testCodePath);
var source = new Source(filename, testCode);
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