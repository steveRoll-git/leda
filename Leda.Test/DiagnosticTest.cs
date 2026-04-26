using System.Text.RegularExpressions;
using Leda.Lang;
using Range = Leda.Lang.Range;

namespace Leda.Test;

public class DiagnosticTest
{
    [Theory]
    [ClassData(typeof(DiagnosticTestData))]
    public void TestDiagnostics(TestScenario testScenario)
    {
        var source = new Source("testCode", testScenario.Code);

        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(source.Parse());
        diagnostics.AddRange(source.Bind());
        diagnostics.AddRange(source.Check());

        var actualDiagnostics = DiagnosticPrinter.DiagnosticsOutput(diagnostics);

        if (testScenario.ExpectedDiagnostics != actualDiagnostics)
        {
            Assert.Fail(
                $"""
                 Diagnostics differ

                 Expected:
                 {testScenario.ExpectedDiagnostics}
                 Actual:
                 {actualDiagnostics}
                 """);
        }

        var actualCode = Emitter.Emit(source.Chunk);
        if (testScenario.ExpectedCode != actualCode)
        {
            Assert.Fail($"""
                         Emitted code differs

                         {actualCode}
                         """);
        }
    }
}

public record TestScenario(string Filename, string Code, string ExpectedDiagnostics, string ExpectedCode)
{
    public override string ToString()
    {
        return Filename;
    }
}

public class DiagnosticTestData : TheoryData<TestScenario>
{
    private const string ProjectPath = "../../../";

    public DiagnosticTestData()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Join(ProjectPath, "tests")))
        {
            var filename = Path.GetFileNameWithoutExtension(file);
            var code = File.ReadAllText(file);
            var expectedDiagnostics = File.ReadAllText(Path.Join(ProjectPath, "results", filename + ".diagnostics"));
            var expectedCode = File.ReadAllText(Path.Join(ProjectPath, "results", filename + ".lua"));

            Add(new(filename, code, expectedDiagnostics, expectedCode));
        }
    }
}