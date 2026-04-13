namespace Leda.Lang;

/// <summary>
/// A value or type that has some origin in the source code, that may be referenced in multiple places.
/// </summary>
public abstract class Symbol
{
    /// <summary>
    /// The location where this symbol was defined.
    /// </summary>
    public Location Definition { get; internal set; }

    /// <summary>
    /// A local variable.
    /// </summary>
    public class LocalVariable(Tree.Statement.LocalDeclaration declaration, int index)
        : Symbol
    {
        public Tree.Statement.LocalDeclaration Declaration => declaration;
        public int Index => index;
    }

    /// <summary>
    /// A function defined with `local function`.
    /// </summary>
    public class LocalFunction(Tree.Statement.LocalFunctionDeclaration declaration) : Symbol
    {
        public Tree.Statement.LocalFunctionDeclaration Declaration => declaration;
    }

    /// <summary>
    /// A parameter in a function.
    /// </summary>
    public class Parameter(Tree.Expression.Function function, int index) : Symbol
    {
        public Tree.Expression.Function Function => function;
        public int Index => index;
    }

    /// <summary>
    /// The counter variable of a numeric `for` loop.
    /// </summary>
    public class NumericForCounter : Symbol;

    /// <summary>
    /// An iteration variable in a generic `for` loop.
    /// </summary>
    public class ForVariable(Tree.Statement.IteratorFor forLoop, int index) : Symbol
    {
        public Tree.Statement.IteratorFor ForLoop => forLoop;
        public int Index => index;
    }

    /// <summary>
    /// A language-defined type that is known ahead of time.
    /// </summary>
    public class IntrinsicType(Type type) : Symbol
    {
        public Type Type => type;
    }

    /// <summary>
    /// A type alias.
    /// </summary>
    public class TypeAlias(Tree.TypeAliasDeclaration declaration) : Symbol
    {
        public Tree.TypeAliasDeclaration Declaration => declaration;
    }

    /// <summary>
    /// A generic type parameter.
    /// </summary>
    public class TypeParameter : Symbol;

    /// <summary>
    /// A string key in a table.
    /// </summary>
    public class StringKey(Type.Table table, string key) : Symbol
    {
        // For this symbol it's okay to store type information, since it's recreated in the typecheck phase.
        public Type.Table Table => table;
        public string Key => key;
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