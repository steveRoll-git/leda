using System.Diagnostics.CodeAnalysis;
using Leda.Lang;

namespace Leda.LSP;

public static class NameFinder
{
    private static Tree? GetNameAtPosition<T>(List<T> trees, Position position) where T : Tree
    {
        foreach (var tree in trees)
        {
            if (GetNameAtPosition(tree, position) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Tree? GetNameAtPosition(Tree.IfBranch branch, Position position)
    {
        return GetNameAtPosition(branch.Condition, position) ??
               GetNameAtPosition(branch.Body.Statements, position);
    }

    private static Tree? GetNameAtPosition(List<Tree.IfBranch> branches, Position position)
    {
        foreach (var ifBranch in branches)
        {
            if (GetNameAtPosition(ifBranch, position) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Tree? GetNameAtPosition(List<Tree.TypeDeclaration.Pair> fields, Position position)
    {
        foreach (var field in fields)
        {
            if (GetNameAtPosition(field.Key, position) is { } foundKey)
            {
                return foundKey;
            }

            if (GetNameAtPosition(field.Value, position) is { } foundValue)
            {
                return foundValue;
            }
        }

        return null;
    }

    public static Tree? GetNameAtPosition(Tree.Block block, Position position)
    {
        return GetNameAtPosition(block.Statements, position);
    }

    /// <summary>
    /// Finds the name that lies on the given position by recursively descending the tree.
    /// </summary>
    /// <returns>The Tree.Name under the given position, or null if it wasn't found.</returns>
    public static Tree? GetNameAtPosition(Tree tree, Position position)
    {
        if (!tree.Range.Contains(position))
        {
            return null;
        }

        if (tree is Tree.Name or Tree.TypeDeclaration.Name)
        {
            return tree;
        }

        return tree switch
        {
            Tree.Binary binary => GetNameAtPosition(binary.Left, position) ??
                                  GetNameAtPosition(binary.Right, position),

            Tree.Access access => GetNameAtPosition(access.Key, position) ??
                                  GetNameAtPosition(access.Target, position),

            Tree.Assignment assignment => GetNameAtPosition(assignment.Targets, position) ??
                                          GetNameAtPosition(assignment.Values, position),

            Tree.Call call => GetNameAtPosition(call.Target, position) ??
                              GetNameAtPosition(call.Parameters, position),

            Tree.Declaration declaration => GetNameAtPosition(declaration.Name, position) ??
                                            (declaration.Type != null
                                                ? GetNameAtPosition(declaration.Type, position)
                                                : null),

            Tree.Do doBlock => GetNameAtPosition(doBlock.Body, position),

            Tree.Function function => GetNameAtPosition(function.Type, position) ??
                                      GetNameAtPosition(function.Body, position),

            Tree.TypeDeclaration.Function functionType => GetNameAtPosition(functionType.Parameters, position) ??
                                                          (functionType.ReturnTypes != null
                                                              ? GetNameAtPosition(functionType.ReturnTypes, position)
                                                              : null),

            Tree.GlobalDeclaration globalDeclaration => throw new NotImplementedException(),

            Tree.If ifBlock => GetNameAtPosition(ifBlock.Primary, position) ??
                               GetNameAtPosition(ifBlock.ElseIfs, position) ??
                               (ifBlock.ElseBody != null
                                   ? GetNameAtPosition(ifBlock.ElseBody, position)
                                   : null),

            Tree.IteratorFor iteratorFor => GetNameAtPosition(iteratorFor.Declarations, position) ??
                                            GetNameAtPosition(iteratorFor.Iterator, position) ??
                                            GetNameAtPosition(iteratorFor.Body, position),

            Tree.Unary unary => GetNameAtPosition(unary.Expression, position),

            Tree.LocalDeclaration localDeclaration => GetNameAtPosition(localDeclaration.Declarations, position) ??
                                                      GetNameAtPosition(localDeclaration.Values, position),

            Tree.LocalFunctionDeclaration localFunctionDeclaration =>
                GetNameAtPosition(localFunctionDeclaration.Name, position) ??
                GetNameAtPosition(localFunctionDeclaration.Function, position),

            Tree.MethodCall methodCall => GetNameAtPosition(methodCall.Target, position) ??
                                          GetNameAtPosition(methodCall.FuncName, position) ??
                                          GetNameAtPosition(methodCall.Parameters, position),

            Tree.NumericalFor numericalFor => GetNameAtPosition(numericalFor.Counter, position) ??
                                              GetNameAtPosition(numericalFor.Start, position) ??
                                              GetNameAtPosition(numericalFor.Limit, position) ??
                                              (numericalFor.Step != null
                                                  ? GetNameAtPosition(numericalFor.Step, position)
                                                  : null) ??
                                              GetNameAtPosition(numericalFor.Body, position),

            Tree.RepeatUntil repeatUntil => GetNameAtPosition(repeatUntil.Body, position) ??
                                            GetNameAtPosition(repeatUntil.Condition, position),

            Tree.Return returnStatement => returnStatement.Expression != null
                ? GetNameAtPosition(returnStatement.Expression, position)
                : null,

            Tree.Table table => GetNameAtPosition(table.Fields, position),

            Tree.TableField tableField => GetNameAtPosition(tableField.Key, position) ??
                                          GetNameAtPosition(tableField.Value, position),

            Tree.TypeDeclaration.Table table => GetNameAtPosition(table.Pairs, position),

            Tree.TypeDeclaration.Union union => throw new NotImplementedException(),

            Tree.While whileStatement => GetNameAtPosition(whileStatement.Condition, position) ??
                                         GetNameAtPosition(whileStatement.Body, position),

            _ => null
        };
    }
}