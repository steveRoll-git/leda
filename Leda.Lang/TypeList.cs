namespace Leda.Lang;

/// <summary>
/// Represents a list of values with an optional `rest` type.
/// </summary>
public class TypeList
{
    /// <summary>
    /// A TypeList that contains no values.
    /// </summary>
    public static readonly TypeList None = new();

    /// <summary>
    /// A TypeList with a repeating Any value.
    /// </summary>
    public static readonly TypeList Any = new() { Rest = Type.Any };

    /// <summary>
    /// The known list of types.
    /// </summary>
    public List<Type> List { get; init; } = [];

    /// <summary>
    /// A TypeList that continues this one. If this is not null, `Rest` must be null.
    /// </summary>
    public TypeList? Continued { get; init; }

    /// <summary>
    /// The type of any additional values that follow the List. If this is not null, `Continued` must be null.
    /// </summary>
    public Type? Rest { get; init; }

    /// <summary>
    /// Iterate over all of this list's types, including any types in `Continued` and `Rest`.<br/>
    /// If this list has a `Rest` type, it will be infinitely returned at the end of the list.
    /// </summary>
    /// <returns>An iterator for tuples with the current type, and whether this type is the final repeated `Rest` type.</returns>
    public IEnumerable<(Type Type, bool Repeated)> Types()
    {
        foreach (var type in List)
        {
            yield return (type, false);
        }

        if (Continued != null)
        {
            foreach (var pair in Continued.Types())
            {
                yield return pair;
            }
        }
        else if (Rest != null)
        {
            while (true)
            {
                yield return (Rest, true);
            }
        }
    }
}