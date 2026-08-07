namespace Leda.Lang;

/// <summary>
/// A range of text in a source file.
/// </summary>
/// <param name="Start">Where this range starts (inclusive).</param>
/// <param name="End">Where this range ends (exclusive).</param>
public readonly record struct Range(Position Start, Position End)
{
    /// <summary>
    /// Returns whether the given position lies within this range.
    /// </summary>
    public bool Contains(Position position)
    {
        return position >= Start && position <= End;
    }

    /// <summary>
    /// Returns a range that covers both this and the other range.
    /// </summary>
    public Range Union(Range other)
    {
        return new(Start < other.Start ? Start : other.Start, End > other.End ? End : other.End);
    }

    public override string ToString()
    {
        return $"({Start} {End})";
    }
}