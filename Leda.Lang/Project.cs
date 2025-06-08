using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// A collection of Leda source files.
/// </summary>
public class Project
{
    public readonly List<Source> Sources = [];
    private readonly Dictionary<string, Source> sourcesByPath = [];

    public delegate void SourceCheckedHandler(Source source, List<Diagnostic> diagnostics);

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
    /// Parses, binds and checks this source, and returns the diagnostics from all the stages.
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public List<Diagnostic> Check(Source source)
    {
        List<Diagnostic> diagnostics = [];
        diagnostics.AddRange(source.Parse());
        diagnostics.AddRange(source.Bind());
        diagnostics.AddRange(source.Check());
        return diagnostics;
    }

    /// <summary>
    /// Parses, binds and checks all currently added sources.
    /// </summary>
    /// <param name="sourceCheckedHandler">Handler to run after a source has been checked, with all the diagnostics that were reported.</param>
    public void CheckAll(SourceCheckedHandler sourceCheckedHandler)
    {
        foreach (var source in Sources)
        {
            sourceCheckedHandler(source, Check(source));
        }
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