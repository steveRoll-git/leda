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

    public record NotEnoughValues(int Expected, int Got, TypeListKind Kind) : TypeMismatch
    {
        public override string Message =>
            Kind switch
            {
                TypeListKind.FunctionTypeParameter =>
                    $"Target type doesn't provide enough parameters. Expected at least {Expected}, got {Got}.",
                _ => $"Not enough {TypeList.ItemNoun(Kind)}(s) are given. Expected at least {Expected} but got {Got}."
            };
    }

    public record ValueInListIncompatible(int Index, TypeListKind Kind) : TypeMismatch
    {
        public override string Message => $"Type of {TypeList.ItemNoun(Kind)} #{Index + 1} is incompatible:";
    }

    public record SourceMissingKey(string Target, string Source, string Key) : TypeMismatch
    {
        public override string Message =>
            $"Type '{Source}' does not have a key of type '{Key}' required by type '{Target}'.";
    }

    public record TableKeyIncompatible(string Key) : TypeMismatch
    {
        public override string Message => $"Values at key '{Key}' are incompatible.";
    }

    private string ToString(int indent)
    {
        var result = new string(' ', indent * 2) + Message;
        foreach (var child in Children)
        {
            result += "\n" + child.ToString(indent + 1);
        }

        return result;
    }

    public sealed override string ToString()
    {
        return ToString(0);
    }
}