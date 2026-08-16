using Leda.Lang;

namespace Leda.Test;

public class DiagnosticTest
{
    private const string ProjectPath = "../../../";
    private static readonly string TestsPath = Path.Join(ProjectPath, "tests");
    private static readonly string ResultsPath = Path.Join(ProjectPath, "results");

    [Theory]
    [ClassData(typeof(DiagnosticTestData))]
    public void TestDiagnostics(string path)
    {
        var filename = Path.GetFileNameWithoutExtension(path);
        var code = File.ReadAllText(path);
        var expectedDiagnostics = File.ReadAllText(Path.Join(ResultsPath, filename + ".diagnostics"));
        var expectedCode = File.ReadAllText(Path.Join(ResultsPath, filename + ".lua"));

        var project = new Project();
        var source = new Source(filename, code);
        project.AddSource(source);

        var diagnostics = project.GetDiagnostics(source);

        var actualDiagnostics = DiagnosticPrinter.DiagnosticsOutput(diagnostics);

        if (expectedDiagnostics != actualDiagnostics)
        {
            Assert.Fail(
                $"""
                 Diagnostics differ

                 Expected:
                 {expectedDiagnostics}
                 Actual:
                 {actualDiagnostics}
                 """);
        }

        var actualCode = Emitter.Emit(source.File);
        if (expectedCode != actualCode)
        {
            Assert.Fail($"""
                         Emitted code differs

                         {actualCode}
                         """);
        }
    }

    private sealed class DiagnosticTestData : TheoryData<string>
    {
        public DiagnosticTestData()
        {
            foreach (var path in Directory.EnumerateFiles(TestsPath))
            {
                Add(new TheoryDataRow<string>(path)
                {
                    TestDisplayName = Path.GetFileNameWithoutExtension(path),
                    Label = "",
                });
            }
        }
    }
}