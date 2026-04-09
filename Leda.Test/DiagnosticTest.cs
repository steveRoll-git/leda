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

        Assert.Equal(diagnostics.Count, testScenario.ExpectedDiagnostics.Count);
        for (var i = 0; i < diagnostics.Count; i++)
        {
            var diagnostic = diagnostics[i];
            Assert.Equal(diagnostic.Range, testScenario.ExpectedDiagnostics[i].Range);
            Assert.Equal(diagnostic.Message, testScenario.ExpectedDiagnostics[i].Message);
        }
    }
}

public record DiagnosticResult(Range Range, string Message);

public record TestScenario(string Filename, string Code, List<DiagnosticResult> ExpectedDiagnostics)
{
    public override string ToString()
    {
        return Filename;
    }
}

public class DiagnosticTestData : TheoryData<TestScenario>
{
    private static readonly Regex DiagnosticRegex = new(@"^\s*(\^+) (.+)");

    public DiagnosticTestData()
    {
        foreach (var file in Directory.EnumerateFiles("../../../diagnosticTests"))
        {
            using var reader = new StreamReader(file);
            var code = "";
            var diagnostics = new List<DiagnosticResult>();
            var lineNumber = -1;
            while (reader.ReadLine() is { } line)
            {
                if (DiagnosticRegex.Match(line) is { Success: true, Groups: var groups })
                {
                    var rangeGroup = groups[1];
                    var range = new Range(
                        new(lineNumber, rangeGroup.Index),
                        new(lineNumber, rangeGroup.Index + rangeGroup.Length));
                    diagnostics.Add(new(range, groups[2].Value));
                }
                else
                {
                    code += line + "\n";
                    lineNumber++;
                }
            }

            Add(new(Path.GetFileNameWithoutExtension(file), code, diagnostics));
        }
    }
}