namespace Leda.Lang;

/// <summary>
/// Represents the reason a type is incompatible with another type.
/// </summary>
public abstract record TypeMismatch
{
    public abstract string Message { get; }

    /// <summary>
    /// Additional mismatches that provide detail to this mismatch.
    /// </summary>
    public List<TypeMismatch> Children = [];

    public record Primitive(string Target, string Source) : TypeMismatch
    {
        public override string Message => $"Type '{Source}' is not assignable to type '{Target}'.";
    }

    public record NotEnoughValues(int Minimum, int Got, TypeListKind Kind, bool Exact) : TypeMismatch
    {
        private string AmountPhrase => Exact ? "" : "at least ";

        public override string Message =>
            Kind switch
            {
                TypeListKind.FunctionTypeParameter =>
                    $"Target type doesn't provide enough parameters. Expected {AmountPhrase}{Minimum}, got {Got}.",
                _ =>
                    $"Not enough {TypeList.ItemNoun(Kind)}(s) are given. Expected {AmountPhrase}{Minimum} but got {Got}.",
            };
    }

    public record ValueInListIncompatible(int Index, TypeListKind Kind) : TypeMismatch
    {
        public override string Message => $"Type of {TypeList.ItemNoun(Kind)} #{Index + 1} is incompatible:";
    }

    public record MissingKeys(string Target, string Source, List<string> Keys) : TypeMismatch
    {
        public override string Message => Keys.Count == 1
            ? $"Key {Keys[0]} is missing in type '{Source}' but required in type '{Target}'."
            : $"Type '{Source}' is missing the following keys from type '{Target}': {string.Join(", ", Keys)}";
    }

    public record TableKeyIncompatible(string Key) : TypeMismatch
    {
        public override string Message => $"Values at key '{Key}' are incompatible.";
    }

    public record TrailingValuesIncompatible : TypeMismatch
    {
        public override string Message => "The values returned here are incompatible.";
    }

    public static string ListToString(List<TypeMismatch> mismatches, int indent = 0)
    {
        var result = "";
        foreach (var child in mismatches)
        {
            result += "\n" + child.ToString(indent + 1);
        }

        return result;
    }

    private string ToString(int indent)
    {
        return new string(' ', indent * 2) + Message + ListToString(Children, indent);
    }

    public sealed override string ToString()
    {
        return ToString(0);
    }
}