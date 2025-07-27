using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

public abstract class Type
{
    public virtual string Name => "<unnamed type>";

    /// <summary>
    /// Base class for all primitive types, that don't require much checking logic other than checking that the target
    /// type is equal to one or more existing types.
    /// </summary>
    private class PrimitiveType(string name, Func<Type, bool> assignableFunc) : Type
    {
        public override string Name => name;
        private Func<Type, bool> AssignableFunc => assignableFunc;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            // 'unknown' is assignable to all types.
            if (other == Unknown)
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
    }

    /// <summary>
    /// The top type - can hold any value.
    /// </summary>
    public static readonly Type Any = new PrimitiveType("any", _ => true);

    /// <summary>
    /// The "unknown" type, used as a placeholder before named references are resolved, or left there in case of errors.
    /// </summary>
    public static readonly Type Unknown = new PrimitiveType("unknown", _ => true);

    /// <summary>
    /// The `nil` unit type.
    /// </summary>
    public static readonly Type Nil = new PrimitiveType("nil", other => other == Nil);

    /// <summary>
    /// The primitive number type.
    /// </summary>
    public static readonly Type Number = new PrimitiveType("number", other => other == Number);

    /// <summary>
    /// The `true` boolean literal.
    /// </summary>
    public static readonly Type True = new PrimitiveType("true", other => other == True);

    /// <summary>
    /// The `false` boolean literal.
    /// </summary>
    public static readonly Type False = new PrimitiveType("false", other => other == False);

    /// <summary>
    /// The primitive boolean type.
    /// </summary>
    public static readonly Type Boolean =
        new PrimitiveType("boolean", other => other == Boolean || other == True || other == False);

    /// <summary>
    /// The primitive string type.
    /// </summary>
    public static readonly Type StringPrimitive =
        new PrimitiveType("string", other => other == StringPrimitive || other is StringLiteral);

    /// <summary>
    /// Supertype of all table types.
    /// </summary>
    public static readonly Type
        TablePrimitive = new PrimitiveType("table", other => other == TablePrimitive || other is Table);

    /// <summary>
    /// Supertype of all function types.
    /// </summary>
    public static readonly Type FunctionPrimitive =
        new PrimitiveType("function", other => other == FunctionPrimitive || other is Function);

    /// <summary>
    /// A string literal.
    /// </summary>
    public class StringLiteral(string literal) : Type
    {
        public string Literal => literal;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            // 'unknown' is assignable to all types.
            if (other == Unknown)
            {
                reason = null;
                return true;
            }

            if (other is StringLiteral c && c.Literal == Literal)
            {
                reason = null;
                return true;
            }

            reason = new TypeMismatch.Primitive(this, other);
            return false;
        }

        public override string ToString()
        {
            return '"' + Literal + '"';
        }
    }

    public class Function(TypeList parameters, TypeList returns) : Type
    {
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
            // 'unknown' is assignable to all types.
            if (other == Unknown)
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

        public override string ToString()
        {
            return $"function({Parameters}){(Return.Empty ? "" : ": " + Return)}";
        }
    }

    public class Table(List<Table.Pair> pairs) : Type
    {
        // TODO use a more efficient lookup structure for this

        public struct Pair(Type key, Type value)
        {
            public Type Key => key;
            public Type Value => value;
        }

        public List<Pair> Pairs => pairs;

        public override bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
        {
            // 'unknown' is assignable to all types.
            if (other == Unknown)
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

        public override string ToString()
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

    public override string ToString() => Name;
}