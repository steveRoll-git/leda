using System.Diagnostics.CodeAnalysis;
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
        private Func<Type, bool> AssignableFunc => assignableFunc;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            if (DefinitelyAssignable(other))
            {
                reason = null;
                return true;
            }

            if (!AssignableFunc(other))
            {
                reason = new TypeMismatch.Primitive(this, other);
                return false;
            }

            reason = null;
            return true;
        }

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
    public static readonly Type Nil = new PrimitiveType(other => other == Nil) { Name = "nil" };

    /// <summary>
    /// The `true` boolean literal.
    /// </summary>
    public static readonly Type True = new PrimitiveType(other => other == True) { Name = "true" };

    /// <summary>
    /// The `false` boolean literal.
    /// </summary>
    public static readonly Type False = new PrimitiveType(other => other == False) { Name = "false" };

    /// <summary>
    /// The primitive boolean type.
    /// </summary>
    public static readonly Type Boolean =
        new PrimitiveType(other => other == Boolean || other == True || other == False) { Name = "boolean" };

    /// <summary>
    /// The primitive number type.
    /// </summary>
    public static readonly Type NumberPrimitive =
        new PrimitiveType(other => other == NumberPrimitive || other is NumberLiteral) { Name = "number" };

    public class NumberLiteral(double literal) : Type
    {
        public double Literal => literal;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (DefinitelyAssignable(other) || other is NumberLiteral l && l.Literal == Literal)
            {
                reason = null;
                return true;
            }

            reason = new TypeMismatch.Primitive(this, other);
            return false;
        }

        public override string Display()
        {
            return Literal.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The primitive string type.
    /// </summary>
    public static readonly Type StringPrimitive =
        new PrimitiveType(other => other == StringPrimitive || other is StringLiteral) { Name = "string" };

    /// <summary>
    /// A string literal.
    /// </summary>
    public class StringLiteral(string literal) : Type
    {
        public string Literal => literal;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            if (DefinitelyAssignable(other) || other is StringLiteral l && l.Literal == Literal)
            {
                reason = null;
                return true;
            }

            reason = new TypeMismatch.Primitive(this, other);
            return false;
        }

        public override string Display()
        {
            return '"' + Literal + '"';
        }
    }

    /// <summary>
    /// Supertype of all function types.
    /// </summary>
    public static readonly Type FunctionPrimitive =
        new PrimitiveType(other => other == FunctionPrimitive || other is Function) { Name = "function" };

    public class Function(TypeList parameters, TypeList returns) : Type
    {
        public override bool UserNameable => true;

        /// <summary>
        /// The types of this function's parameters.
        /// </summary>
        public TypeList Parameters { get; } = parameters;

        /// <summary>
        /// This function's return types.
        /// </summary>
        public TypeList Return { get; } = returns;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            if (DefinitelyAssignable(other))
            {
                reason = null;
                return true;
            }

            // TODO accept other types that may be callable like tables with __call
            if (other is not Function function)
            {
                reason = new TypeMismatch.Primitive(this, other);
                return false;
            }

            List<TypeMismatch> reasons = [];
            if (!function.Parameters.IsAssignableFrom(Parameters, out var parameterReasons,
                    TypeList.TypeListKind.FunctionTypeParameter))
            {
                reasons.AddRange(parameterReasons);
            }

            if (!Return.IsAssignableFrom(function.Return, out var returnReasons,
                    TypeList.TypeListKind.FunctionTypeReturn))
            {
                reasons.AddRange(returnReasons);
            }

            if (reasons.Count > 0)
            {
                reason = new TypeMismatch.Primitive(this, other) { Children = reasons };
                return false;
            }

            reason = null;
            return true;
        }

        public override string Display()
        {
            return $"function({Parameters}){(Return.Empty ? "" : ": " + Return)}";
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

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            if (DefinitelyAssignable(other))
            {
                reason = null;
                return true;
            }

            if (other is not Table otherTable)
            {
                reason = new TypeMismatch.Primitive(this, other);
                return false;
            }

            List<TypeMismatch> reasons = [];

            foreach (var pair in Pairs)
            {
                // TODO use lookup
                var otherPair = otherTable.Pairs.FirstOrDefault(p => pair.Key.IsAssignableFrom(p.Key));
                if (otherPair.Key == null)
                {
                    reasons.Add(new TypeMismatch.SourceMissingKey(this, other, pair.Key));
                    continue;
                }

                if (!pair.Value.IsAssignableFrom(otherPair.Value, out var valueReason))
                {
                    reasons.Add(new TypeMismatch.TableKeyIncompatible(pair.Key) { Children = [valueReason] });
                }
            }

            if (reasons.Count > 0)
            {
                reason = new TypeMismatch.Primitive(this, other) { Children = reasons };
                return false;
            }

            reason = null;
            return true;
        }

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
    /// Returns whether a value of type `other` can be assigned to a variable of this type.
    /// </summary>
    public abstract bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason);

    public bool IsAssignableFrom(Type other)
    {
        return IsAssignableFrom(other, out _);
    }

    /// <summary>
    /// Returns whether the type `other` is definitely assignable to this type, without doing any specific checks.
    /// Returns `true` if `other` is the `unknown` type, or the target type itself.
    /// </summary>
    public bool DefinitelyAssignable(Type other)
    {
        return other == Unknown || other == this;
    }

    /// <summary>
    /// Returns a string representation of the type's contents.
    /// </summary>
    public abstract string Display();

    public override string ToString() => Name ?? Display();
}