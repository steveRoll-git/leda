namespace Leda.Lang;

/// <summary>
/// Represents the reason a type is incompatible with another type.
/// </summary>
public abstract class TypeMismatch
{
    private static string TypeListItemNoun(TypeList.TypeListKind kind) => kind switch
    {
        TypeList.TypeListKind.Parameter or TypeList.TypeListKind.FunctionTypeParameter => "parameter",
        TypeList.TypeListKind.Return or TypeList.TypeListKind.FunctionTypeReturn => "return value",
        TypeList.TypeListKind.Value => "value",
        _ => throw new Exception()
    };

    public abstract string Message { get; }

    /// <summary>
    /// Additional mismatches that provide detail to this mismatch.
    /// </summary>
    public List<TypeMismatch> Children = [];

    public class Primitive(Type target, Type source) : TypeMismatch
    {
        public override string Message => $"Type '{source}' is not assignable to type '{target}'.";
    }

    public class NotEnoughValues(int expected, int got, TypeList.TypeListKind kind) : TypeMismatch
    {
        public override string Message =>
            kind switch
            {
                TypeList.TypeListKind.FunctionTypeParameter =>
                    $"Target type doesn't provide enough parameters. Expected at least {expected}, got {got}.",
                _ => $"Not enough {TypeListItemNoun(kind)}(s) are given. Expected at least {expected} but got {got}."
            };
    }

    public class ValueInListIncompatible(int index, TypeList.TypeListKind kind) : TypeMismatch
    {
        public override string Message => $"Type of {TypeListItemNoun(kind)} #{index} is incompatible:";
    }

    public class ParameterIncompatible(string targetName, string sourceName) : TypeMismatch
    {
        public override string Message => $"Types of parameters '{sourceName}' and '{targetName}' are incompatible.";
    }

    private string ToString(int indent)
    {
        var result = (indent > 0 ? new string(' ', indent * 2 - 1) + "└" : "") + Message;
        foreach (var child in Children)
        {
            result += "\n" + child.ToString(indent + 1);
        }

        return result;
    }

    public override string ToString()
    {
        return ToString(0);
    }
}