namespace Leda.Lang;

/// <summary>
/// A range of text in a source file.
/// </summary>
public struct Range(Position start, Position end)
{
    /// <summary>
    /// Where this range starts (inclusive).
    /// </summary>
    public Position Start = start;

    /// <summary>
    /// Where this range ends (exclusive).
    /// </summary>
    public Position End = end;

    /// <summary>
    /// Returns whether the given position lies within this range.
    /// </summary>
    public bool Contains(Position position)
    {
        return position >= Start && position < End;
    }

    public override string ToString()
    {
        return $"({Start} {End})";
    }
}