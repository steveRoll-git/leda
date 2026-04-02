using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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

        public override string Display()
        {
            return Name!;
        }
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

        public override string Display()
        {
            return Literal.ToString(CultureInfo.InvariantCulture);
        }
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

        public override string Display()
        {
            return '"' + Literal + '"';
        }
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
        public TypeList Parameters { get; } = parameters;

        /// <summary>
        /// This function's return types.
        /// </summary>
        public TypeList Return { get; set; } = returns;

        public List<TypeParameter> TypeParameters { get; } = typeParameters;

        public override string Display()
        {
            return
                $"function{(TypeParameters.Count > 0 ? $"<{string.Join(", ", TypeParameters)}>" : "")}({Parameters}){(Return.Empty ? "" : ": " + Return)}";
        }
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
        /// Cached values of pairs whose keys are string literals.
        /// </summary>
        public Dictionary<string, Type?> StringLiterals { get; } = [];

        /// <summary>
        /// Cached values of pairs whose keys are number literals.
        /// </summary>
        public Dictionary<double, Type> NumberLiterals { get; } = [];

        // TODO also store `true` and `false` literals

        public readonly record struct Pair(Type Key, Type Value);

        /// <summary>
        /// Cached values of pairs whose key is not a string or number literal.
        /// </summary>
        public List<Pair> Indexers { get; } = [];

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

        public override string Display()
        {
            // TODO the checker should be the one doing displays
            return "TODO";
        }
    }

    /// <summary>
    /// A reference to a symbol.
    /// </summary>
    public class Reference(Symbol symbol, string name) : Type
    {
        public Symbol Symbol => symbol;

        public override string Display()
        {
            return name;
        }
    }

    /// <summary>
    /// A generic type parameter.
    /// </summary>
    public class TypeParameter(string name) : Type
    {
        public override string Display()
        {
            return name;
        }
    }

    /// <summary>
    /// Returns a string representation of the type's contents.
    /// </summary>
    public abstract string Display();

    public override string ToString() => Name ?? Display();
}