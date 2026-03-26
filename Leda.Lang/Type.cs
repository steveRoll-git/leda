using System.Globalization;

namespace Leda.Lang;

public abstract class Type
{
    public string? Name { get; set; }

    /// <summary>
    /// Whether the user can give a custom name to this type.
    /// </summary>
    public virtual bool UserNameable => false;

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
        public override bool UserNameable => true;

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

    public class Table(List<Table.Pair> pairs) : Type
    {
        // TODO use a more efficient lookup structure for this

        public override bool UserNameable => true;

        public struct Pair(Type key, Type value)
        {
            public Type Key => key;
            public Type Value => value;
        }

        public List<Pair> Pairs => pairs;

        public override string Display()
        {
            var s = "{";

            if (Pairs.Count > 0)
            {
                s += "\n";
            }

            foreach (var pair in Pairs)
            {
                string keyString;
                // TODO show key without quotes only if it's also a valid identifier
                if (pair.Key is StringLiteral c)
                {
                    keyString = c.Literal;
                }
                else
                {
                    keyString = $"[{pair.Key}]";
                }

                s += $"  {keyString}: {pair.Value},\n";
            }

            return s + "}";
        }
    }

    /// <summary>
    /// A placeholder for a type that will be inferred from assignment.
    /// </summary>
    public class Infer(Action<Type> onInferred) : Type
    {
        /// <summary>
        /// The action that will be performed once the type is inferred.
        /// </summary>
        public Action<Type> OnInferred { get; } = onInferred;

        public override string Display()
        {
            return "unknown";
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