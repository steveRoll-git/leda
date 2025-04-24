namespace Leda.Lang;

/// <summary>
/// A range of text in a source file.
/// </summary>
public struct Range
{
    /// <summary>
    /// Where this range starts (inclusive).
    /// </summary>
    public Position Start;

    /// <summary>
    /// Where this range ends (exclusive).
    /// </summary>
    public Position End;

    public Range(Position start, Position end)
    {
        Start = start;
        End = end;
    }

    public override string ToString()
    {
        return $"({Start} {End})";
    }
}