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
    /// List of diagnostics reported by the Parser.
    /// </summary>
    internal List<Diagnostic> ParserDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Binder.
    /// </summary>
    internal List<Diagnostic> BinderDiagnostics { get; set; } = [];

    /// <summary>
    /// List of diagnostics reported by the Checker.
    /// </summary>
    internal List<Diagnostic> CheckerDiagnostics { get; set; } = [];

    /// <summary>
    /// The last <see cref="Project.EditVersion"/> that this source was checked at.
    /// </summary>
    internal int CheckedVersion { get; set; }

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
}