namespace Leda.Lang;

/// <summary>
/// A position in source code.
/// </summary>
public struct Position(int line, int character) : IComparable<Position>
{
    /// <summary>
    /// Zero-based line number.
    /// </summary>
    public int Line = line;

    /// <summary>
    /// Zero-based character number.
    /// </summary>
    public int Character = character;

    public int CompareTo(Position other)
    {
        if (Line == other.Line)
        {
            return Character - other.Character;
        }

        return Line - other.Line;
    }

    public static bool operator <(Position a, Position b)
    {
        return a.CompareTo(b) < 0;
    }

    public static bool operator <=(Position a, Position b)
    {
        return a.CompareTo(b) <= 0;
    }

    public static bool operator >(Position a, Position b)
    {
        return a.CompareTo(b) > 0;
    }

    public static bool operator >=(Position a, Position b)
    {
        return a.CompareTo(b) >= 0;
    }

    public override string ToString()
    {
        return $"{Line + 1}:{Character + 1}";
    }
}