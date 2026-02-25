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

    public class TypeSymbol : Symbol { }

    public class IntrinsicType(Type type) : Symbol
    {
        public Type Type => type;
    }

    /// <summary>
    /// The built-in any type.
    /// </summary>
    public static readonly IntrinsicType AnyType = new(Type.Any);

    /// <summary>
    /// The built-in boolean type.
    /// </summary>
    public static readonly IntrinsicType BooleanType = new(Type.Boolean);

    /// <summary>
    /// The built-in number type.
    /// </summary>
    public static readonly IntrinsicType NumberType = new(Type.NumberPrimitive);

    /// <summary>
    /// The built-in string type.
    /// </summary>
    public static readonly IntrinsicType StringType = new(Type.StringPrimitive);

    /// <summary>
    /// The built-in function type.
    /// </summary>
    public static readonly IntrinsicType FunctionType = new(Type.FunctionPrimitive);
}