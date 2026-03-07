namespace Leda.Lang;

/// <summary>
/// An interface for lists of types, or of values that have types.
/// </summary>
public interface ITypeValueList
{
    /// <summary>
    /// A Type if it's a list of types, or Value of it's a list of expressions.<br/>
    /// Only one may be not null.
    /// </summary>
    public record struct TypeValue(Type? Type, Tree.Expression? Value, bool Rest)
    {
        public bool IsNone => Type == null && Value == null;
    }

    public TypeValue this[int index] { get; }
}