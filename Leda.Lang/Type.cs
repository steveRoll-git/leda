using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

public class Type
{
    public string Name { get; } = "<unnamed type>";

    /// <summary>
    /// Function that will be called with another type to check if it's assignable to this one.
    /// </summary>
    private readonly Func<Type, bool> assignableFunc = _ => true;

    public Type(string name, Func<Type, bool> assignableFunc)
    {
        Name = name;
        this.assignableFunc = assignableFunc;
    }

    public Type(string name)
    {
        Name = name;
    }

    public Type() { }

    /// <summary>
    /// The top type - can hold any value.
    /// </summary>
    public static readonly Type Any = new("any");

    /// <summary>
    /// The "unknown" type, used as a placeholder before named references are resolved, or left there in case of errors.
    /// </summary>
    public static readonly Type Unknown = new("unknown");

    /// <summary>
    /// The `nil` unit type.
    /// </summary>
    public static readonly Type Nil = new("nil", other => other == Nil);

    /// <summary>
    /// The primitive number type.
    /// </summary>
    public static readonly Type Number = new("number", other => other == Number);

    /// <summary>
    /// The `true` boolean literal.
    /// </summary>
    public static readonly Type True = new("true", other => other == True);

    /// <summary>
    /// The `false` boolean literal.
    /// </summary>
    public static readonly Type False = new("false", other => other == False);

    /// <summary>
    /// The primitive boolean type.
    /// </summary>
    public static readonly Type Boolean = new("boolean", other => other == Boolean || other == True || other == False);

    /// <summary>
    /// The primitive string type.
    /// </summary>
    public static readonly Type StringPrimitive =
        new("string", other => other == StringPrimitive || other is StringConstant);

    /// <summary>
    /// Supertype of all table types.
    /// </summary>
    public static readonly Type
        TablePrimitive = new("table", other => other == TablePrimitive || other is Table);

    /// <summary>
    /// Supertype of all function types.
    /// </summary>
    public static readonly Type FunctionPrimitive =
        new("function", other => other == FunctionPrimitive || other is Function);

    /// <summary>
    /// A string constant.
    /// </summary>
    public class StringConstant(string constant) : Type
    {
        public string Constant => constant;

        public override string ToString()
        {
            return '"' + Constant + '"';
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
            reason = null;

            // TODO accept other types that may be callable like tables with __call
            if (other is Function function)
            {
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

                return true;
            }

            reason = new TypeMismatch.Primitive(this, other);
            return false;
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
                if (pair.Key is StringConstant c)
                {
                    keyString = c.Constant;
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
    /// A union of two or more types.
    /// </summary>
    public class Union(string name, List<Type> types) : Type(name)
    {
        public List<Type> Types => types;

        // public override bool IsAssignableFrom(Type other)
        // {
        //     foreach (var type in types)
        //     {
        //         if (type.IsAssignableFrom(other))
        //         {
        //             return true;
        //         }
        //     }
        //
        //     return false;
        // }
    }

    /// <summary>
    /// Returns whether a value of type `other` can be assigned to a variable of this type.
    /// </summary>
    public virtual bool IsAssignableFrom(Type other, [NotNullWhen(false)] out TypeMismatch? reason)
    {
        if (!assignableFunc(other))
        {
            reason = new TypeMismatch.Primitive(this, other);
            return false;
        }

        reason = null;
        return true;
    }

    public bool IsAssignableFrom(Type other)
    {
        return IsAssignableFrom(other, out _);
    }

    public override string ToString() => Name;
}