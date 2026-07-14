namespace Leda.Lang;

/// <summary>
/// Represents a Leda source file.
/// </summary>
public class Source
{
    /// <summary>
    /// The file path of this source.
    /// </summary>
    public readonly string Path;

    /// <summary>
    /// The code in this source file as a string.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// A dictionary where the key is a 0-based line number, and the value is the index in `Code` where that line begins.
    /// </summary>
    private Dictionary<int, int> newlines = new() { { 0, 0 } };

    /// <summary>
    /// The syntax tree for this file.
    /// </summary>
    public Tree.File File { get; set; }

    /// <summary>
    /// Maps Tree nodes to the symbol they refer to.
    /// </summary>
    public Dictionary<Tree, Symbol> TreeSymbolMap { get; set; } = [];

    /// <summary>
    /// A dictionary of where symbols are references in this source. (Symbols from other sources may be referenced too?)
    /// </summary>
    public Dictionary<Symbol, List<Location>> SymbolReferences { get; set; } = [];

    /// <summary>
    /// Names that possibly refer to a global value defined in another source.
    /// </summary>
    public Dictionary<string, List<Tree.Expression.Name>> GlobalNames { get; } = [];

    public TypeEvaluator Evaluator { get; set; }

    /// <summary>
    /// A set of other sources that this source depends on.
    /// </summary>
    public HashSet<Source> Dependencies { get; } = [];

    /// <summary>
    /// A set of sources that depend on this source.
    /// </summary>
    public HashSet<Source> Dependents { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Parser.
    /// </summary>
    internal List<Diagnostic> ParserDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by `Project.BindGlobals`.
    /// </summary>
    internal List<Diagnostic.NameNotFound> NameNotFoundDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Binder.
    /// </summary>
    internal List<Diagnostic> BinderDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Checker.
    /// </summary>
    internal List<Diagnostic> CheckerDiagnostics { get; set; } = [];

    /// <summary>
    /// Whether the bindings of the global references in this source need to be rechecked.
    /// </summary>
    internal bool NeedsBindingGlobals { get; set; } = true;

    /// <summary>
    /// Whether this source needs a Checker pass in order to get updated diagnostics.
    /// </summary>
    internal bool NeedsChecking { get; set; } = true;

    /// <summary>
    /// Creates a new source with the given path, and reads the file at that path into Code.
    /// </summary>
    public static Source ReadFromFile(string path)
    {
        return new Source(path, System.IO.File.ReadAllText(path));
    }

    /// <summary>
    /// Creates a new source with the given path and code.
    /// </summary>
    public Source(string path, string code)
    {
        Path = path;
        Code = code;
        File = new Tree.File();
        Evaluator = new TypeEvaluator(this);

        // Map all newline numbers to the indices they appear at.
        // TODO the newline map is currently only used by ConsoleReporter. generating it should be done only in that case
        var currentLine = 1;
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == '\n')
            {
                newlines.Add(currentLine, i + 1);
                currentLine++;
            }
        }
    }

    /// <summary>
    /// Returns the contents of the line at the given (0-based) index.
    /// </summary>
    public string GetLine(int index)
    {
        if (newlines.TryGetValue(index, out var lineStart))
        {
            if (newlines.TryGetValue(index + 1, out var lineEnd))
            {
                return Code.Substring(lineStart, lineEnd - lineStart - 1);
            }

            return Code.Substring(lineStart);
        }

        return "";
    }

    /// <summary>
    /// Associates this tree node with the given symbol.
    /// </summary>
    internal void AttachSymbol(Tree tree, Symbol symbol, bool isDefinition = false)
    {
        TreeSymbolMap.Add(tree, symbol);

        if (isDefinition)
        {
            symbol.Definition = new(this, tree.Range);
        }
        else
        {
            if (!SymbolReferences.TryGetValue(symbol, out var references))
            {
                references = [];
                SymbolReferences.Add(symbol, references);
            }

            references.Add(new Location(this, tree.Range));
        }
    }

    /// <summary>
    /// Removes the association of this tree node with a symbol.
    /// </summary>
    internal void DetachSymbol(Tree tree)
    {
        TreeSymbolMap.Remove(tree);
    }

    /// <summary>
    /// Finds the symbol that this tree refers to if it exists.
    /// </summary>
    public Symbol? GetTreeSymbol(Tree tree)
    {
        TreeSymbolMap.TryGetValue(tree, out var symbol);
        return symbol;
    }
}