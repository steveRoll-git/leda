global using TypeMap = System.Collections.Generic.Dictionary<Leda.Lang.Type.TypeParameter, Leda.Lang.Type>;

namespace Leda.Lang;

/// <summary>
/// Represents a type in the type system.
/// </summary>
public abstract class Type
{
    /// <summary>
    /// The symbol that corresponds to this type.
    /// It is non-null for intrinsic types, and for types that have a named definition (type aliases and parameters).
    /// </summary>
    public Symbol? Symbol { get; internal set; }

    /// <summary>
    /// A type that doesn't require much checking logic other than checking that the source
    /// type is equal to one or more existing types.
    /// </summary>
    public class PrimitiveType : Type
    {
        public new Symbol Symbol => base.Symbol!;

        /// <summary>
        /// A function that returns whether the given type is assignable to this type.
        /// </summary>
        public Func<Type, bool> AssignableFunc { get; }

        public PrimitiveType(string name, Func<Type, bool> assignableFunc)
        {
            AssignableFunc = assignableFunc;
            base.Symbol = new Symbol.IntrinsicType(name, this);
        }
    }

    /// <summary>
    /// A type that can hold any value.
    /// </summary>
    public static readonly PrimitiveType Any = new("any", _ => true);

    /// <summary>
    /// A type that is returned in case of errors. Currently, it behaves similarly to `any`.
    /// </summary>
    public static readonly PrimitiveType Unknown = new("unknown", _ => true);

    /// <summary>
    /// The `nil` literal.
    /// </summary>
    public static readonly PrimitiveType Nil = new("nil", _ => false);

    /// <summary>
    /// The `true` boolean literal.
    /// </summary>
    public static readonly PrimitiveType True = new("true", _ => false);

    /// <summary>
    /// The `false` boolean literal.
    /// </summary>
    public static readonly PrimitiveType False = new("false", _ => false);

    /// <summary>
    /// The primitive boolean type.
    /// </summary>
    public static readonly PrimitiveType Boolean = new("boolean", other => other == True || other == False);

    /// <summary>
    /// The primitive number type.
    /// </summary>
    public static readonly PrimitiveType NumberPrimitive = new("number", other => other is NumberLiteral);

    public class NumberLiteral(double literal) : Type
    {
        public double Literal { get; } = literal;
    }

    /// <summary>
    /// The primitive string type.
    /// </summary>
    public static readonly PrimitiveType StringPrimitive = new("string", other => other is StringLiteral);

    /// <summary>
    /// A string literal.
    /// </summary>
    public class StringLiteral(string literal) : Type
    {
        public string Literal { get; } = literal;
    }

    /// <summary>
    /// Supertype of all function types.
    /// </summary>
    public static readonly PrimitiveType FunctionPrimitive = new("function", other => other is Function);

    public class Function(TypeList parameters, TypeList returns, List<TypeParameter> typeParameters) : Type
    {
        /// <summary>
        /// The types of this function's parameters.
        /// </summary>
        public TypeList Parameters { get; } = parameters;

        /// <summary>
        /// This function's return types.
        /// </summary>
        public TypeList Returns { get; } = returns;

        public List<TypeParameter> TypeParameters { get; } = typeParameters;

        public bool IsGeneric => TypeParameters.Count > 0;
    }

    /// <summary>
    /// Supertype of all table types.
    /// </summary>
    public static readonly PrimitiveType TablePrimitive = new("table", other => other == TablePrimitive || other is Table or Array);

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
            public Symbol? Symbol { get; } = symbol;
            public Type? CachedType = null;
        }

        /// <summary>
        /// A string field in a table type that's inferred from a value.
        /// </summary>
        public class ValueStringField(Symbol? symbol, Tree.Expression.Table.Field tableField) : StringField(symbol)
        {
            public Tree.Expression.Table.Field Field { get; } = tableField;
        }

        /// <summary>
        /// A string field in a table type that's defined by a type annotation.
        /// </summary>
        public class TypeStringField(Symbol? symbol, Tree.Type.Table.Field tableField) : StringField(symbol)
        {
            public Tree.Type.Table.Field Field { get; } = tableField;
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
            Symbol = other.Symbol;
            StringLiterals = other.StringLiterals;
            NumberLiterals = other.NumberLiterals;
            Indexers = other.Indexers;
        }
    }

    public class Array(Type elementType) : Type
    {
        public Type ElementType { get; } = elementType;
    }

    /// <summary>
    /// A wrapper around another type that also accepts `nil`.
    /// </summary>
    public class Nillable(Type inner) : Type
    {
        public Type Inner { get; } = inner;
    }

    /// <summary>
    /// A generic type parameter.
    /// </summary>
    public class TypeParameter : Type
    {
        public new Symbol Symbol => base.Symbol!;

        /// <summary>
        /// A generic type parameter.
        /// </summary>
        public TypeParameter(Symbol symbol)
        {
            base.Symbol = symbol;
        }
    }
}