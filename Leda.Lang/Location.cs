namespace Leda.Lang;

/// <summary>
/// A location in a source file.
/// </summary>
public struct Location(Source source, Range range)
{
    public Source? Source { get; set; } = source;
    public Range Range { get; set; } = range;
}