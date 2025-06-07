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
    /// Parses, binds and checks all currently added sources.
    /// </summary>
    public void CheckAll(IDiagnosticReporter reporter)
    {
        foreach (var source in Sources)
        {
            source.Parse(reporter);
            source.Bind(reporter);
            source.Check(reporter);
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