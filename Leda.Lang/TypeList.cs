using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// Represents a list of values with an optional `rest` type.
/// </summary>
public class TypeList
{
    /// <summary>
    /// A TypeList that contains no values.
    /// </summary>
    public static readonly TypeList None = new() { Empty = true };

    /// <summary>
    /// A TypeList with a repeating Any value.
    /// </summary>
    public static readonly TypeList Any = new() { Rest = Type.Any };

    /// <summary>
    /// The known list of types.
    /// </summary>
    public List<Type> List { get; }

    // TODO optional names for list items

    /// <summary>
    /// A TypeList that continues this one. If this is not null, `Rest` must be null.
    /// </summary>
    public TypeList? Continued { get; } = null;

    /// <summary>
    /// The type of any additional values that follow the List. If this is not null, `Continued` must be null.
    /// </summary>
    public Type? Rest { get; private init; } = null;

    public int MinimumValues { get; private set; }

    public bool Empty { get; private init; }

    public TypeList()
    {
        List = [];
        MinimumValues = 0;
    }

    public TypeList(List<Type> list)
    {
        List = list;
        CalculateMinimum();
    }

    public TypeList(List<Type> list, TypeList? continued)
    {
        List = list;
        Continued = continued;
        CalculateMinimum();
    }

    private void CalculateMinimum()
    {
        // TODO consider Continued
        for (var i = List.Count - 1; i >= 0; i--)
        {
            // TODO consider nillable values
            if (true)
            {
                MinimumValues = i + 1;
                break;
            }
        }
    }

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

    public bool IsAssignableFrom(TypeList other, [NotNullWhen(false)] out List<TypeMismatch>? reasons,
        TypeListKind kind)
    {
        if (other.MinimumValues < MinimumValues)
        {
            reasons = [new TypeMismatch.NotEnoughValues(MinimumValues, other.MinimumValues, kind)];
            return false;
        }

        reasons = [];

        using var sourceEnumerator = other.Types().GetEnumerator();
        var sourceIndex = 1;

        // TODO handle Continued and Rest
        foreach (var target in List)
        {
            var sourceType = Type.Nil;
            var gotSource = false;
            if (sourceEnumerator.MoveNext())
            {
                // TODO handle the source being a `Rest` value
                sourceType = sourceEnumerator.Current.Type;
                gotSource = true;
            }

            if (!target.IsAssignableFrom(sourceType, out var subReason))
            {
                reasons.Add(new TypeMismatch.ValueInListIncompatible(sourceIndex, kind) { Children = [subReason] });
            }

            if (gotSource)
            {
                sourceIndex++;
            }
        }

        if (reasons.Count > 0)
        {
            return false;
        }

        reasons = null;
        return true;
    }

    public override string ToString()
    {
        var result = string.Join(", ", List);

        if (Continued != null)
        {
            result += (result.Length > 0 ? ", " : "") + Continued;
        }

        if (Rest != null)
        {
            result += (result.Length > 0 ? ", " : "") + Rest + "...";
        }

        return result;
    }

    /// <summary>
    /// What context a TypeList is used in.
    /// </summary>
    public enum TypeListKind
    {
        /// <summary>
        /// The values given as parameters to a function call.
        /// </summary>
        Parameter,

        /// <summary>
        /// The values returned by a function.
        /// </summary>
        Return,

        /// <summary>
        /// The parameters of a function type being assigned to another function type.
        /// </summary>
        FunctionTypeParameter,

        /// <summary>
        /// The return type of a function type being assigned to another function type.
        /// </summary>
        FunctionTypeReturn,

        /// <summary>
        /// Any other use of TypeLists.
        /// </summary>
        Value
    }
}