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
    public Tree.Chunk Chunk { get; private set; }

    /// <summary>
    /// Maps Tree nodes to the symbol they refer to.
    /// </summary>
    private Dictionary<Tree, Symbol> treeSymbolMap = [];

    /// <summary>
    /// A dictionary of where symbols are references in this source. (Symbols from other sources may be referenced too?)
    /// </summary>
    public Dictionary<Symbol, List<Location>> SymbolReferences { get; private set; } = [];

    public TypeEvaluator Evaluator { get; private set; }

    /// <summary>
    /// A list of any symbols referenced in this Source that are defined in other Sources.
    /// </summary>
    private List<string> externalSymbols = [];

    /// <summary>
    /// Creates a new source with the given path, and reads the file at that path into Code.
    /// </summary>
    public static Source ReadFromFile(string path)
    {
        return new Source(path, File.ReadAllText(path));
    }

    /// <summary>
    /// Creates a new source with the given path and code.
    /// </summary>
    public Source(string path, string code)
    {
        Path = path;
        Code = code;
        Chunk = new Tree.Chunk();
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
    /// Parse the source's contents and store the syntax tree.
    /// </summary>
    public List<Diagnostic> Parse()
    {
        var (tree, diagnostics) = Parser.ParseFile(this);
        Chunk = tree;
        return diagnostics;
    }

    /// <summary>
    /// Associates all top level `Name` nodes with symbols.
    /// </summary>
    public List<Diagnostic> Bind()
    {
        treeSymbolMap = [];
        SymbolReferences = [];
        return Binder.Bind(this, Chunk);
    }

    /// <summary>
    /// Checks the types of all nodes.
    /// </summary>
    public List<Diagnostic> Check()
    {
        Evaluator = new TypeEvaluator(this);
        return Checker.Check(this, Evaluator);
    }

    /// <summary>
    /// Associates this tree node with the given symbol.
    /// </summary>
    internal void AttachSymbol(Tree tree, Symbol symbol, bool isDefinition = false)
    {
        treeSymbolMap.Add(tree, symbol);

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
        treeSymbolMap.TryGetValue(tree, out var symbol);
        return symbol;
    }
}