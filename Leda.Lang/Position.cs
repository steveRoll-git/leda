namespace Leda.Lang;

/// <summary>
/// A position in source code.
/// </summary>
public struct Position(int line, int character)
{
    /// <summary>
    /// Zero-based line number.
    /// </summary>
    public int Line = line;

    /// <summary>
    /// Zero-based character number.
    /// </summary>
    public int Character = character;

    public override string ToString()
    {
        return $"{Line + 1}:{Character + 1}";
    }
}