using Leda.Lang;

namespace Leda.Test;

public static class DiagnosticPrinter
{
    /// <summary>
    /// Returns a string representation of a list of diagnostics.
    /// </summary>
    public static string DiagnosticsOutput(List<Diagnostic> diagnostics)
    {
        var output = "";

        foreach (var diagnostic in diagnostics)
        {
            output += $"""
                       {diagnostic.GetType().Name}{diagnostic.Range}
                       {diagnostic.Severity}: {diagnostic.Message}

                       """;
        }

        return output;
    }
}