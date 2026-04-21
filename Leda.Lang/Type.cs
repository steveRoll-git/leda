using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// Represents a type in the type system.
/// </summary>
public abstract class Type
{
    public string? Name { get; set; }

    /// <summary>
    /// A type that doesn't require much checking logic other than checking that the source
    /// type is equal to one or more existing types.
    /// </summary>
    public class PrimitiveType(Func<Type, bool> assignableFunc) : Type
    {
        public Func<Type, bool> AssignableFunc => assignableFunc;
    }

    /// <summary>
    /// The top type - can hold any value.
    /// </summary>
    public static readonly Type Any = new PrimitiveType(_ => true) { Name = "any" };

    /// <summary>
    /// The "unknown" type, used as a placeholder before named references are resolved, or left there in case of errors.
    /// </summary>
    public static readonly Type Unknown = new PrimitiveType(_ => true) { Name = "unknown" };

    /// <summary>
    /// The `nil` unit type.
    /// </summary>
    public static readonly Type Nil = new PrimitiveType(_ => false) { Name = "nil" };

    /// <summary>
    /// The `true` boolean literal.
    /// </summary>
    public static readonly Type True = new PrimitiveType(_ => false) { Name = "true" };

    /// <summary>
    /// The `false` boolean literal.
    /// </summary>
    public static readonly Type False = new PrimitiveType(_ => false) { Name = "false" };

    /// <summary>
    /// The primitive boolean type.
    /// </summary>
    public static readonly Type Boolean =
        new PrimitiveType(other => other == True || other == False) { Name = "boolean" };

    /// <summary>
    /// The primitive number type.
    /// </summary>
    public static readonly Type NumberPrimitive =
        new PrimitiveType(other => other is NumberLiteral) { Name = "number" };

    public class NumberLiteral(double literal) : Type
    {
        public double Literal => literal;
    }

    /// <summary>
    /// The primitive string type.
    /// </summary>
    public static readonly Type StringPrimitive =
        new PrimitiveType(other => other is StringLiteral) { Name = "string" };

    /// <summary>
    /// A string literal.
    /// </summary>
    public class StringLiteral(string literal) : Type
    {
        public string Literal => literal;
    }

    /// <summary>
    /// Supertype of all function types.
    /// </summary>
    public static readonly Type FunctionPrimitive =
        new PrimitiveType(other => other is Function) { Name = "function" };

    public class Function(TypeList parameters, TypeList returns, List<TypeParameter> typeParameters) : Type
    {
        /// <summary>
        /// The types of this function's parameters.
        /// </summary>
        public TypeList Parameters => parameters;

        /// <summary>
        /// This function's return types.
        /// </summary>
        public TypeList Return => returns;

        public List<TypeParameter> TypeParameters => typeParameters;
    }

    /// <summary>
    /// Supertype of all table types.
    /// </summary>
    public static readonly Type
        TablePrimitive = new PrimitiveType(other => other == TablePrimitive || other is Table) { Name = "table" };

    /// <summary>
    /// A table type, which can originate either from a Tree.Type.Table, or inferred from a Tree.Expression.Table.
    /// </summary>
    public class Table : Type
    {
        /// <summary>
        /// The symbol and type of a string key in a table.
        /// </summary>
        public record StringKey(Symbol Symbol, Type Type);

        /// <summary>
        /// Cached values of fields whose keys are string literals.
        /// </summary>
        public Dictionary<string, StringKey?> StringLiterals { get; } = [];

        /// <summary>
        /// Cached values of fields whose keys are number literals.
        /// </summary>
        public Dictionary<double, Type> NumberLiterals { get; } = [];

        // TODO also store `true` and `false` literals

        public readonly record struct Field(Type Key, Type Value);

        /// <summary>
        /// Cached values of fields whose key is not a string or number literal.
        /// </summary>
        public List<Field> Indexers { get; } = [];

        /// <summary>
        /// The table value that this table type should be inferred from.
        /// </summary>
        public Tree.Expression.Table? InferTree { get; }

        /// <summary>
        /// The table type definition that defines this table.
        /// </summary>
        public Tree.Type.Table? TypeTree { get; }

        public Table(Tree.Expression.Table inferTree)
        {
            InferTree = inferTree;
        }

        public Table(Tree.Type.Table typeTree)
        {
            TypeTree = typeTree;
        }

        /// <summary>
        /// Returns whether this table type is inferred from a table value, or defined by a table type annotation.
        /// </summary>
        public bool IsInferred([NotNullWhen(true)] out Tree.Expression.Table? inferTree,
            [NotNullWhen(false)] out Tree.Type.Table? typeTree)
        {
            inferTree = InferTree;
            typeTree = TypeTree;
            return inferTree != null;
        }
    }

    /// <summary>
    /// A generic type parameter.
    /// </summary>
    public class TypeParameter : Type
    {
        /// <summary>
        /// A generic type parameter.
        /// </summary>
        public TypeParameter(string name)
        {
            Name = name;
        }
    }
}