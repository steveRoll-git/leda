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
    /// Creates a new source with the given path, and reads the file at that path into Code.
    /// </summary>
    public Source(string path)
    {
        Path = path;
        Code = File.ReadAllText(path);
    }

    /// <summary>
    /// Creates a new source with the given path and code.
    /// </summary>
    public Source(string path, string code)
    {
        Path = path;
        Code = code;
    }
}