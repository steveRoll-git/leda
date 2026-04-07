namespace Leda.Lang;

/// <summary>
/// Represents a list of types.
/// </summary>
public abstract class TypeList
{
    /// <summary>
    /// The number of elements in this TypeList, excluding the `rest` part.
    /// </summary>
    public abstract int Count { get; }

    /// <summary>
    /// A TypeList that doesn't depend on any information from the source code.
    /// </summary>
    public class Builtin : TypeList
    {
        public override int Count => 0;
    }

    /// <summary>
    /// A TypeList that contains no values.
    /// </summary>
    public static readonly Builtin Empty = new();

    /// <summary>
    /// A TypeList with a repeating Any value.
    /// </summary>
    public static readonly Builtin Any = new();

    /// <summary>
    /// A TypeList with a repeating Unknown value.
    /// </summary>
    public static readonly Builtin Unknown = new();

    /// <summary>
    /// The parameters of a function.
    /// </summary>
    public class Parameters(Tree.Expression.Function function) : TypeList
    {
        public Tree.Expression.Function Function => function;
        public override int Count => function.Type.Parameters.Count;
    }

    /// <summary>
    /// A TypeList that derives its types from a list of type annotations.
    /// </summary>
    public class FromTypes(List<Tree.Type> types) : TypeList
    {
        public List<Tree.Type> Types => types;
        public override int Count => types.Count;
    }

    /// <summary>
    /// A TypeList that derives its types from a list of values.
    /// </summary>
    public class FromValues(List<Tree.Expression> values) : TypeList
    {
        public List<Tree.Expression> Values => values;
        public override int Count => values.Count;
    }

    /// <summary>
    /// Returns a string that represents an item in this kind of TypeList.
    /// </summary>
    public static string ItemNoun(TypeListKind kind) => kind switch
    {
        TypeListKind.Parameter or TypeListKind.FunctionTypeParameter => "parameter",
        TypeListKind.Return or TypeListKind.FunctionTypeReturn => "return value",
        _ => "value"
    };
}

/// <summary>
/// What context a TypeList is used in.
/// </summary>
public enum TypeListKind
{
    /// <summary>
    /// The values given as parameters to a function call.
    /// </summary>
    Parameter,

    /// <summary>
    /// The values returned by a function.
    /// </summary>
    Return,

    /// <summary>
    /// The parameters of a function type being assigned to another function type.
    /// </summary>
    FunctionTypeParameter,

    /// <summary>
    /// The return type of a function type being assigned to another function type.
    /// </summary>
    FunctionTypeReturn,

    /// <summary>
    /// Any other use of TypeLists.
    /// </summary>
    Value
}