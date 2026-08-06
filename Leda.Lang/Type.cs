global using TypeMap = System.Collections.Generic.Dictionary<Leda.Lang.Type.TypeParameter, Leda.Lang.Type>;

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
    /// A type that can hold any value.
    /// </summary>
    public static readonly Type Any = new PrimitiveType(_ => true) { Name = "any" };

    /// <summary>
    /// A type that is returned in case of errors. Currently, it behaves similarly to `any`.
    /// </summary>
    public static readonly Type Unknown = new PrimitiveType(_ => true) { Name = "unknown" };

    /// <summary>
    /// The `nil` literal.
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
    public static readonly Type FunctionPrimitive = new PrimitiveType(other => other is Function) { Name = "function" };

    public class Function(TypeList parameters, TypeList returns, List<TypeParameter> typeParameters) : Type
    {
        /// <summary>
        /// The types of this function's parameters.
        /// </summary>
        public TypeList Parameters => parameters;

        /// <summary>
        /// This function's return types.
        /// </summary>
        public TypeList Returns => returns;

        public List<TypeParameter> TypeParameters => typeParameters;

        public bool IsGeneric => TypeParameters.Count > 0;
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
        /// The symbol and type of a string field in a table.
        /// </summary>
        public abstract class StringField(Symbol? symbol)
        {
            public Symbol? Symbol => symbol;
            public Type? CachedType = null;
        }

        /// <summary>
        /// A string field in a table type that's inferred from a value.
        /// </summary>
        public class ValueStringField(Symbol? symbol, Tree.Expression.Table.Field tableField) : StringField(symbol)
        {
            public Tree.Expression.Table.Field Field => tableField;
        }

        /// <summary>
        /// A string field in a table type that's defined by a type annotation.
        /// </summary>
        public class TypeStringField(Symbol? symbol, Tree.Type.Table.Field tableField) : StringField(symbol)
        {
            public Tree.Type.Table.Field Field => tableField;
        }

        /// <summary>
        /// Cached values of fields whose keys are string literals.
        /// </summary>
        public Dictionary<string, StringField> StringLiterals { get; }

        /// <summary>
        /// Cached values of fields whose keys are number literals.
        /// </summary>
        public Dictionary<double, Type> NumberLiterals { get; }

        // TODO also store `true` and `false` literals

        public readonly record struct Field(Type Key, Type Value);

        /// <summary>
        /// Cached values of fields whose key is not a string or number literal.
        /// </summary>
        public List<Field> Indexers { get; }

        /// <summary>
        /// The type arguments that this table was instantiated with, if this table is an instantiation of a generic
        /// table.
        /// </summary>
        public TypeMap? TypeMap { get; init; }

        public Table()
        {
            StringLiterals = [];
            NumberLiterals = [];
            Indexers = [];
        }

        public Table(Table other)
        {
            StringLiterals = other.StringLiterals;
            NumberLiterals = other.NumberLiterals;
            Indexers = other.Indexers;
        }
    }

    /// <summary>
    /// A wrapper around another type that also accepts `nil`.
    /// </summary>
    public class Nillable(Type inner) : Type
    {
        public Type Inner => inner;
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