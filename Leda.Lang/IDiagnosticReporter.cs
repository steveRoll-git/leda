namespace Leda.Lang;

/// <summary>
/// An interface for reporting code diagnostics.
/// </summary>
public interface IDiagnosticReporter
{
    public void Report(Diagnostic diagnostic);
}