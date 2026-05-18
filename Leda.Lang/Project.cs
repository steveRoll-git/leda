using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// A collection of Leda source files.
/// </summary>
public class Project
{
    public readonly List<Source> Sources = [];
    private readonly Dictionary<string, Source> sourcesByPath = [];

    /// <summary>
    /// Adds a source file to this project.
    /// </summary>
    public void AddSource(Source source)
    {
        if (sourcesByPath.ContainsKey(source.Path))
        {
            throw new Exception("A source with this path has already been added");
        }

        Sources.Add(source);
        sourcesByPath.Add(source.Path, source);
    }

    /// <summary>
    /// Removes a previously added source from this project.
    /// </summary>
    public void RemoveSource(Source source)
    {
        if (!sourcesByPath.ContainsKey(source.Path))
        {
            throw new Exception("This source hasn't been added");
        }

        Sources.Remove(source);
        sourcesByPath.Remove(source.Path);
    }

    public bool TryGetSourceByPath(string path, [NotNullWhen(true)] out Source? source)
    {
        return sourcesByPath.TryGetValue(path, out source);
    }

    /// <summary>
    /// Returns the list of current diagnostics of a source.
    /// </summary>
    public List<Diagnostic> GetDiagnostics(Source source)
    {
        if (source.NeedsParsing)
        {
            source.ParserDiagnostics = Parse(source);
            source.NeedsParsing = false;
        }

        if (source.NeedsBinding)
        {
            source.BinderDiagnostics = Bind(source);
            source.NeedsBinding = false;
        }

        if (source.NeedsChecking)
        {
            source.CheckerDiagnostics = Check(source);
            source.NeedsChecking = false;
        }

        return [..source.ParserDiagnostics, ..source.BinderDiagnostics, ..source.CheckerDiagnostics];
    }

    /// <summary>
    /// Parse the source's contents and store the syntax tree.
    /// </summary>
    private List<Diagnostic> Parse(Source source)
    {
        var (tree, diagnostics) = Parser.ParseFile(source);
        source.File = tree;
        return diagnostics;
    }

    /// <summary>
    /// Associates all top level `Name` nodes with symbols.
    /// </summary>
    private List<Diagnostic> Bind(Source source)
    {
        source.TreeSymbolMap = [];
        source.SymbolReferences = [];
        return Binder.Bind(source, source.File);
    }

    /// <summary>
    /// Checks the types of all nodes.
    /// </summary>
    private List<Diagnostic> Check(Source source)
    {
        source.Evaluator = new TypeEvaluator(source);
        return Checker.Check(source, source.Evaluator);
    }

    /// <summary>
    /// Creates a new project and adds all leda files in the given path.
    /// </summary>
    public static Project FromFilesInDirectory(string path)
    {
        var project = new Project();

        foreach (var filePath in Directory.EnumerateFiles(path, "*.leda"))
        {
            project.AddSource(Source.ReadFromFile(filePath));
        }

        return project;
    }
}