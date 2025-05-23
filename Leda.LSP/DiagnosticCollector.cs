using Leda.Lang;

namespace Leda.LSP;

public class DiagnosticCollector : IDiagnosticReporter
{
    public readonly List<Diagnostic> Diagnostics = [];

    public void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }
}