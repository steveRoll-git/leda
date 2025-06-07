using Leda.Lang;

namespace Leda;

/// <summary>
/// Pretty-prints diagnostics to the console.
/// </summary>
public static class ConsoleReporter
{
    private static readonly Dictionary<DiagnosticSeverity, ConsoleColor> SeverityColors = new()
    {
        { DiagnosticSeverity.Error, ConsoleColor.Red },
        { DiagnosticSeverity.Warning, ConsoleColor.Yellow },
        { DiagnosticSeverity.Information, ConsoleColor.Blue },
        { DiagnosticSeverity.Hint, ConsoleColor.Cyan }
    };

    public static void Report(Source source, Diagnostic diagnostic)
    {
        var prevColor = Console.ForegroundColor;

        var severityColor = SeverityColors[diagnostic.Severity];
        var numberColumnWidth = 4;
        var numberColumnSeparator = " | ";
        var start = diagnostic.Range.Start;
        var sourceLine = source.GetLine(diagnostic.Range.Start.Line);
        Console.ForegroundColor = severityColor;
        Console.Write($"{diagnostic.Severity}: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(diagnostic.Message);
        Console.WriteLine($"{source.Path}:{start}");
        Console.WriteLine("{0}{1}{2}",
            (start.Line + 1).ToString().PadLeft(numberColumnWidth),
            numberColumnSeparator,
            sourceLine);
        var endCharacter = diagnostic.Range.End.Line == start.Line ? diagnostic.Range.End.Character : sourceLine.Length;
        var highlightLine = new string(' ', numberColumnWidth + numberColumnSeparator.Length + start.Character) +
                            new string('^', Math.Max(endCharacter - start.Character, 1));
        Console.ForegroundColor = severityColor;
        Console.WriteLine(highlightLine);

        Console.ForegroundColor = prevColor;
    }
}