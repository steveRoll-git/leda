namespace Leda.Lang;

/// <summary>
/// Represents a list of types.
/// </summary>
public abstract class TypeList
{
    /// <summary>
    /// A TypeList that doesn't depend on any information from the source code.
    /// </summary>
    public class Builtin : TypeList;

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
    }

    /// <summary>
    /// The return types of a function.
    /// </summary>
    public class Returns(Tree.Expression.Function function) : TypeList
    {
        public Tree.Expression.Function Function => function;
    }
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