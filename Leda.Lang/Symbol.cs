namespace Leda.Lang;

public enum SymbolKind
{
    LocalVariable,
    Parameter,
    Type,
    TypeParameter
}

/// <summary>
/// A value or type that has some origin in the source code, that may be referenced in multiple places.
/// </summary>
public class Symbol(SymbolKind kind)
{
    /// <summary>
    /// The location where this symbol was defined.
    /// </summary>
    public Location Definition { get; internal set; }

    public SymbolKind Kind => kind;

    public class IntrinsicType(Type type) : Symbol(SymbolKind.Type)
    {
        public Type Type => type;
    }

    public class LocalVariable(Tree.Statement.LocalDeclaration declaration, int index)
        : Symbol(SymbolKind.LocalVariable)
    {
        public Tree.Statement.LocalDeclaration Declaration => declaration;
        public int Index => index;
    };

    public class Parameter(Tree.Expression.Function function, int index) : Symbol(SymbolKind.Parameter)
    {
        public Tree.Expression.Function Function => function;
        public int Index => index;
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