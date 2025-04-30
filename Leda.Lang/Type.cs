namespace Leda.Lang;

public class Type
{
    public string Name { get; init; }

    public Type(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The "unknown" type, used as a placeholder before named references are resolved, or left there in case of errors.
    /// </summary>
    public static readonly Type Unknown = new("unknown");

    /// <summary>
    /// The primitive number type.
    /// </summary>
    public static readonly Type Number = new("number");

    /// <summary>
    /// The primitive boolean type.
    /// </summary>
    public static readonly Type Boolean = new("boolean");

    /// <summary>
    /// The primitive string type.
    /// </summary>
    public static readonly Type String = new("string");

    /// <summary>
    /// A union of one or more types.
    /// </summary>
    public class Union(string name, List<Type> types) : Type(name)
    {
        public List<Type> Types => types;
    }
}