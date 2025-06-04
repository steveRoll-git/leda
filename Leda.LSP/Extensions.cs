using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic;
using Range = Leda.Lang.Range;

namespace Leda.LSP;

public static class Extensions
{
    public static Position ToLs(this Lang.Position p) => new(p.Line, p.Character);
    public static Lang.Position ToLeda(this Position p) => new(p.Line, p.Character);

    public static DocumentRange ToLs(this Range r) => new(r.Start.ToLs(), r.End.ToLs());

    public static DiagnosticSeverity ToLs(this Lang.DiagnosticSeverity s) => s switch
    {
        Lang.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
        Lang.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
        Lang.DiagnosticSeverity.Information => DiagnosticSeverity.Information,
        Lang.DiagnosticSeverity.Hint => DiagnosticSeverity.Hint,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };

    public static Diagnostic ToLs(this Leda.Lang.Diagnostic d) => new()
    {
        Message = d.Message,
        Range = d.Range.ToLs(),
        Severity = d.Severity.ToLs(),
        Source = "leda"
    };
}