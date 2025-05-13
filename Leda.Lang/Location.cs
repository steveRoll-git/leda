namespace Leda.Lang;

/// <summary>
/// A location in a source file.
/// </summary>
public struct Location
{
    public Source Source { get; set; }
    public Range Range { get; set; }

    public Location(Source source, Range range)
    {
        Source = source;
        Range = range;
    }
}