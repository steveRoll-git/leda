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
    public Tree.Block Tree { get; private set; }

    /// <summary>
    /// Maps Tree nodes to the symbol they refer to.
    /// </summary>
    private Dictionary<Tree, Symbol> treeSymbolMap = [];

    /// <summary>
    /// Maps Symbols to their types.
    /// </summary>
    private readonly Dictionary<Symbol, Type> symbolTypeMap = [];

    /// <summary>
    /// A dictionary of where symbols are references in this source. (Symbols from other sources may be referenced too?)
    /// </summary>
    public Dictionary<Symbol, List<Location>> SymbolReferences { get; private set; } = [];

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

        // Map all newline numbers to the indices they appear at.
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
        Tree = tree;
        return diagnostics;
    }

    /// <summary>
    /// Associates all top level `Name` nodes with symbols.
    /// </summary>
    public List<Diagnostic> Bind()
    {
        treeSymbolMap = [];
        SymbolReferences = [];
        return Binder.Bind(this, Tree);
    }

    /// <summary>
    /// Checks the types of all nodes.
    /// </summary>
    public List<Diagnostic> Check()
    {
        return Checker.Check(this);
    }

    /// <summary>
    /// Associates this tree node with the given symbol.
    /// </summary>
    internal void AttachSymbol(Tree tree, Symbol symbol, bool isDefinition = false)
    {
        treeSymbolMap.Add(tree, symbol);

        if (!isDefinition)
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
    /// Finds the symbol that this tree refers to.
    /// </summary>
    /// <returns>True if this tree has a corresponding value symbol, false otherwise.</returns>
    public bool TryGetTreeSymbol(Tree tree, [NotNullWhen(true)] out Symbol? symbol)
    {
        return treeSymbolMap.TryGetValue(tree, out symbol);
    }

    /// <summary>
    /// Sets this value symbol's type.
    /// </summary>
    internal void SetSymbolType(Symbol symbol, Type type)
    {
        symbolTypeMap[symbol] = type;
    }

    /// <summary>
    /// Tries getting the type of this value symbol, if it exists.
    /// </summary>
    public bool TryGetSymbolType(Symbol symbol, [NotNullWhen(true)] out Type? type)
    {
        return symbolTypeMap.TryGetValue(symbol, out type);
    }
}