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

    public bool IsAssignableFrom(TypeList other, [NotNullWhen(false)] out List<TypeMismatch>? reasons,
        TypeListKind kind)
    {
        if (other.MinimumValues < MinimumValues)
        {
            reasons = [new TypeMismatch.NotEnoughValues(MinimumValues, other.MinimumValues, kind)];
            return false;
        }

        reasons = [];

        var targetIterator = GetIterator();
        var sourceIterator = other.GetIterator();
        var sourceIndex = 0;

        while (targetIterator.Next(out var targetType))
        {
            var gotSource = sourceIterator.Next(out var sourceType);
            if (targetIterator.IsRest && !gotSource)
            {
                // If the target type list has a rest type, we only need to check them as long as the source is
                // providing unique types.
                break;
            }

            sourceType ??= Type.Nil;

            if (!targetType.IsAssignableFrom(sourceType, out var subReason))
            {
                reasons.Add(new TypeMismatch.ValueInListIncompatible(sourceIndex, kind) { Children = [subReason] });
            }

            if (gotSource)
            {
                sourceIndex++;
                if (targetIterator.IsRest && sourceIterator.IsRest)
                {
                    // If both type lists end with a rest type, we have to check their assignability just once.
                    break;
                }
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

    /// <summary>
    /// Iterates over the types of a TypeList.
    /// </summary>
    public class Iterator
    {
        private TypeList typeList;
        private int index;

        public Iterator(TypeList typeList)
        {
            this.typeList = typeList;
            index = -1;
            Advance();
        }

        /// <summary>
        /// The current type, or `null` if there are no more types.
        /// </summary>
        public Type? Current => index < typeList.List.Count ? typeList.List[index] : typeList.Rest;

        /// <summary>
        /// Whether the current type is the "rest" part of the list.
        /// </summary>
        public bool IsRest => index >= typeList.List.Count && typeList.Rest != null;

        /// <summary>
        /// Advances to the next type.
        /// </summary>
        private void Advance()
        {
            index++;
            if (index >= typeList.List.Count && typeList.Continued != null)
            {
                typeList = typeList.Continued;
                index = -1;
                Advance();
            }
        }

        /// <summary>
        /// Can be used to iterate over the types in a while loop.
        /// </summary>
        /// <param name="type">The current type.</param>
        /// <returns>Whether iteration can continue.</returns>
        public bool Next([NotNullWhen(true)] out Type? type)
        {
            type = Current;
            Advance();
            return type != null;
        }
    }

    public Iterator GetIterator()
    {
        return new Iterator(this);
    }
}