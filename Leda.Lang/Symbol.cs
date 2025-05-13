namespace Leda.Lang;

/// <summary>
/// A value or type that has some origin in the source code, that may be referenced in multiple places.
/// </summary>
public class Symbol
{
    /// <summary>
    /// The location where this symbol was defined.
    /// </summary>
    public Location Definition { get; internal set; }

    public class LocalVariable : Symbol { }

    public class Parameter : Symbol { }

    public class TypeSymbol(Type type) : Symbol
    {
        public Type Type => type;
    }
}