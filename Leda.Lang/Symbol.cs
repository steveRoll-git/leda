namespace Leda.Lang;

/// <summary>
/// A value or type that has some origin in the source code, that may be referenced in multiple places.
/// </summary>
public abstract class Symbol(string name)
{
    /// <summary>
    /// The location where this symbol was defined.
    /// </summary>
    public Location Definition { get; internal set; }

    public string Name { get; } = name;

    /// <summary>
    /// A local or global variable.
    /// </summary>
    public class Variable(
        Tree.Statement.VariableDeclaration declaration,
        int index,
        bool uninitialized,
        Tree.Chunk chunk)
        : Symbol(declaration.Declarations[index].Name.Value)
    {
        public Tree.Statement.VariableDeclaration Declaration { get; } = declaration;
        public int Index { get; } = index;
        public bool Uninitialized { get; } = uninitialized;
        public Tree.Chunk Chunk { get; } = chunk;
    }

    /// <summary>
    /// A local variable.
    /// </summary>
    public class LocalVariable(
        Tree.Statement.LocalDeclaration localDeclaration,
        int index,
        bool uninitialized,
        Tree.Chunk chunk)
        : Variable(localDeclaration, index, uninitialized, chunk);

    /// <summary>
    /// A global variable.
    /// </summary>
    public class GlobalVariable(
        Tree.Statement.GlobalDeclaration globalDeclaration,
        int index,
        bool uninitialized,
        Tree.Chunk chunk)
        : Variable(globalDeclaration, index, uninitialized, chunk);

    /// <summary>
    /// A function defined with `local function`.
    /// </summary>
    public class LocalFunction(Tree.Statement.LocalFunctionDeclaration declaration) : Symbol(declaration.Name.Value)
    {
        public Tree.Statement.LocalFunctionDeclaration Declaration { get; } = declaration;
    }

    /// <summary>
    /// A parameter in a function.
    /// </summary>
    public class Parameter(Tree.Expression.Function function, int index)
        : Symbol(function.Type.Parameters[index].Name.Value)
    {
        public Tree.Expression.Function Function { get; } = function;
        public int Index { get; } = index;
    }

    /// <summary>
    /// The counter variable of a numeric `for` loop.
    /// </summary>
    public class NumericForCounter(Tree.Statement.NumericalFor forLoop) : Symbol(forLoop.Counter.Value);

    /// <summary>
    /// An iteration variable in a generic `for` loop.
    /// </summary>
    public class ForLoopVariable(Tree.Statement.IteratorFor forLoop, int index) : Symbol(forLoop.Variables[index].Value)
    {
        public Tree.Statement.IteratorFor ForLoop { get; } = forLoop;
        public int Index { get; } = index;
    }

    /// <summary>
    /// A language-defined type that is known ahead of time.
    /// </summary>
    public class IntrinsicType(string name, Type type) : Symbol(name)
    {
        public Type Type { get; } = type;
    }

    /// <summary>
    /// A type alias.
    /// </summary>
    public class TypeAlias(Tree.TypeAliasDeclaration declaration) : Symbol(declaration.Name.Value)
    {
        public Tree.TypeAliasDeclaration Declaration { get; } = declaration;
    }

    /// <summary>
    /// A generic type parameter.
    /// </summary>
    public class TypeParameter : Symbol
    {
        public TypeParameter(Tree.Type.Name name) : base(name.Value)
        {
            Type = new Type.TypeParameter(this);
        }

        /// <summary>
        /// The type of this type parameter.
        /// </summary>
        // It is created here and not by TypeEvaluator because it just needs to uniquely represent the type parameter.
        public Type.TypeParameter Type { get; }
    }

    /// <summary>
    /// A string field in a table.
    /// </summary>
    public class StringField(string key) : Symbol(key)
    {
        public string Key { get; } = key;
    }

    public class Label(Tree.LabelName name) : Symbol(name.Value);
}