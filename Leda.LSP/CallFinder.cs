using Leda.Lang;

namespace Leda.LSP;

/// <summary>
/// Finds the closest `Call` node that contains the given position, and the parameter that the position is in.<br/>
/// Used by the `SignatureHelp` handler.
/// </summary>
public class CallFinder(Position position) : Visitor
{
    private record struct VisitResult(Tree.Expression.Call Call, int ArgumentIndex);

    private VisitResult? result;

    protected override bool Filter(Tree tree)
    {
        return tree.Range.Contains(position);
    }

    protected override void Visit(Tree tree)
    {
        if (tree is Tree.Expression.Call call && call.ArgsRange.Contains(position))
        {
            // With each argument list we visit, `result` will be overwritten, such that it will contain information
            // about the deepest argument list we found when we stop.
            result = null;
            if (call.Arguments.Count == 0)
            {
                result = new(call, 0);
            }
            else
            {
                for (var i = 0; i < call.Arguments.Count; i++)
                {
                    var argument = call.Arguments[i];
                    if (argument.FullRange.Contains(position))
                    {
                        result = new(call, i);
                        return;
                    }
                }
            }
        }
        else if (tree is Tree.Expression.Function)
        {
            // If we're inside a function that's inside an argument, we don't want to show the signature help.
            result = null;
        }
    }

    protected override void PostVisit(Tree tree)
    {
        if (result is { Call: var foundCall } && tree == foundCall)
        {
            // If `PostVisit` was called on the deepest call node we found, we may stop.
            Stop = true;
        }
    }

    private static string GetCallName(Tree tree)
    {
        if (tree is Tree.Expression.Name name)
        {
            return name.Value;
        }

        if (tree is Tree.Expression.String str)
        {
            return str.Value;
        }

        if (tree is Tree.Expression.Access access)
        {
            return GetCallName(access.Key);
        }

        return "";
    }

    public record struct FindResult(string FunctionName, List<string> Parameters, int ArgumentIndex);

    public static FindResult? FindCall(Source source, Position position, TypeEvaluator typeEvaluator)
    {
        var finder = new CallFinder(position);
        finder.Start(source);

        if (finder.result is { } result &&
            typeEvaluator.GetTypeOfExpression(result.Call.Target) is Lang.Type.Function function)
        {
            return new(GetCallName(result.Call.Target),
                typeEvaluator.TypeListToStringElements(function.Parameters),
                result.ArgumentIndex);
        }

        return null;
    }
}