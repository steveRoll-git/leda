using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// Represents a Leda source file.
/// </summary>
public class Source
{
    /// <summary>
    /// The file path for this source - relative to the workspace directory.
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

    public TypeEvaluator Evaluator { get; set; }

    /// <summary>
    /// A list of any symbols referenced in this Source that are defined in other Sources.
    /// </summary>
    private List<string> externalSymbols = [];

    /// <summary>
    /// List of diagnostics reported by the Parser.
    /// </summary>
    public List<Diagnostic> ParserDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Binder.
    /// </summary>
    public List<Diagnostic> BinderDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Checker.
    /// </summary>
    public List<Diagnostic> CheckerDiagnostics { get; set; } = [];

    /// <summary>
    /// Whether the source code needs to be parsed in order to get updated diagnostics.
    /// </summary>
    public bool NeedsParsing { get; set; } = true;

    /// <summary>
    /// Whether this source needs a Binder pass in order to get updated diagnostics.
    /// </summary>
    public bool NeedsBinding { get; set; } = true;

    /// <summary>
    /// Whether this source needs a Checker pass in order to get updated diagnostics.
    /// </summary>
    public bool NeedsChecking { get; set; } = true;

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
    /// Finds the symbol that this tree refers to if it exists.
    /// </summary>
    public Symbol? GetTreeSymbol(Tree tree)
    {
        TreeSymbolMap.TryGetValue(tree, out var symbol);
        return symbol;
    }
}