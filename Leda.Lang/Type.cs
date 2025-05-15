namespace Leda.Lang;

public class Type
{
    public string Name { get; }

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
    public static readonly Type String = new("string", other => other == String);

    /// <summary>
    /// Supertype of all table types.
    /// </summary>
    public static readonly Type Table = new("table", other => other == Table); // TODO include other table types

    /// <summary>
    /// Supertype of all function types.
    /// </summary>
    public static readonly Type
        Function = new("function", other => other == Function); // TODO include other function types

    /// <summary>
    /// A union of two or more types.
    /// </summary>
    public class Union(string name, List<Type> types) : Type(name)
    {
        public List<Type> Types => types;

        public override bool IsAssignableFrom(Type other)
        {
            foreach (var type in types)
            {
                if (type.IsAssignableFrom(other))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Returns whether a value of type `other` can be assigned to a variable of this type.
    /// </summary>
    public virtual bool IsAssignableFrom(Type other)
    {
        return assignableFunc(other);
    }

    public override string ToString() => Name;
}