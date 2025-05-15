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
    public string Code { get; }

    /// <summary>
    /// A dictionary where the key is a 0-based line number, and the value is the index in `Code` where that line begins.
    /// </summary>
    private Dictionary<int, int> newlines = new() { { 0, 0 } };

    /// <summary>
    /// The syntax tree for this file.
    /// </summary>
    public Tree.Block Tree { get; private set; }

    /// <summary>
    /// Maps Tree nodes to the Symbol they refer to.
    /// </summary>
    private Dictionary<Tree, Symbol> symbolMap = [];

    /// <summary>
    /// A list of any symbols referenced in this Source that are defined in other Sources.
    /// </summary>
    private List<string> externalSymbols = [];

    /// <summary>
    /// Creates a new source with the given path, and reads the file at that path into Code.
    /// </summary>
    public Source(string path) : this(path, File.ReadAllText(path)) { }

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
    public void Parse(IDiagnosticReporter reporter)
    {
        Tree = Parser.ParseFile(this, reporter);
    }

    /// <summary>
    /// Associates all top level `Name` nodes with symbols.
    /// </summary>
    public void Bind(IDiagnosticReporter reporter)
    {
        symbolMap = [];
        Binder.Bind(this, Tree, reporter);
    }

    /// <summary>
    /// Checks the types of all nodes.
    /// </summary>
    public void Check(IDiagnosticReporter reporter)
    {
        Checker.Check(this, reporter);
    }

    /// <summary>
    /// Associates this tree node with the given symbol.
    /// </summary>
    internal void AttachSymbol(Tree tree, Symbol symbol)
    {
        symbolMap.Add(tree, symbol);
    }

    /// <summary>
    /// Finds the symbol that this tree refers to.
    /// </summary>
    /// <returns>True if this tree has a corresponding symbol, false otherwise.</returns>
    internal bool TryGetSymbol(Tree tree, [NotNullWhen(true)] out Symbol? symbol)
    {
        return symbolMap.TryGetValue(tree, out symbol);
    }
}