using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// Represents a list of types with an optional `rest` type.
/// </summary>
public class TypeList : ITypeValueList
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
    /// A TypeList with a repeating Unknown value.
    /// </summary>
    public static readonly TypeList Unknown = new() { Rest = Type.Unknown };

    /// <summary>
    /// The known list of types.
    /// </summary>
    public List<Type> List { get; }

    /// <summary>
    /// An optional list of the names of each item in `List`. If this is present, it must have the same number of
    /// elements as `List`.
    /// </summary>
    public List<string>? NameList { get; init; }

    // TODO need to ensure a TypeList cannot continue itself.
    /// <summary>
    /// A TypeList that continues this one. If this is not null, `Rest` must be null.
    /// </summary>
    public TypeList? Continued { get; } = null;

    /// <summary>
    /// The type of any additional values that follow the List. If this is not null, `Continued` must be null.
    /// </summary>
    public Type? Rest { get; private init; } = null;

    public int MinimumValues { get; }

    public bool Empty { get; private init; }

    public TypeList()
    {
        List = [];
        MinimumValues = 0;
        Empty = true;
    }

    public TypeList(List<Type> list)
    {
        List = list;
        MinimumValues = CalculateMinimum();
    }

    public TypeList(List<Type> list, TypeList? continued)
    {
        List = list;
        Continued = continued;
        MinimumValues = CalculateMinimum();
    }

    private int CalculateMinimum()
    {
        // TODO consider Continued
        for (var i = List.Count - 1; i >= 0; i--)
        {
            // TODO consider nillable values
            if (true)
            {
                return i + 1;
                break;
            }
        }

        return 0;
    }

    public override string ToString()
    {
        string result;
        if (NameList != null)
        {
            result = string.Join(", ", List.Select((type, i) => NameList[i] + ": " + type));
        }
        else
        {
            result = string.Join(", ", List);
        }

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

    public ITypeValueList.TypeValue this[int index]
    {
        get
        {
            if (index < List.Count)
            {
                return new() { Type = List[index], Rest = false };
            }

            if (Rest != null)
            {
                return new() { Type = Rest, Rest = true };
            }

            if (Continued != null)
            {
                return Continued[index - List.Count];
            }

            return new();
        }
    }
}