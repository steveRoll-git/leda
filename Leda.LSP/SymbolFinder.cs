using Leda.Lang;
using Range = Leda.Lang.Range;
using Type = Leda.Lang.Type;

namespace Leda.LSP;

public static class SymbolFinder
{
    /// <summary>
    /// A found name along with other information.
    /// </summary>
    /// <param name="Name">The name node that was found.</param>
    /// <param name="GetTreeType">A function that returns the type of the tree node.</param>
    private record struct NameFindResult(Tree Name, Func<TypeEvaluator, Type>? GetTreeType);

    private static NameFindResult? GetNameAtPosition<T>(List<T>? trees, Position position) where T : Tree
    {
        if (trees == null)
        {
            return null;
        }

        foreach (var tree in trees)
        {
            if (GetNameAtPosition(tree, position) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static NameFindResult? GetNameAtPosition(Tree.IfBranch branch, Position position)
    {
        return GetNameAtPosition(branch.Condition, position) ??
               GetNameAtPosition(branch.Body.Statements, position);
    }

    private static NameFindResult? GetNameAtPosition(List<Tree.IfBranch> branches, Position position)
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

    private static NameFindResult? GetNameAtPosition(Tree.Type.Table table, Position position)
    {
        foreach (var field in table.Fields)
        {
            if (GetNameAtPosition(field.Key, position) is { } foundKey)
            {
                return foundKey with { GetTreeType = ev => ev.GetTypeOfTypeAnnotation(field.Value) };
            }

            if (GetNameAtPosition(field.Value, position) is { } foundValue)
            {
                return foundValue;
            }
        }

        return null;
    }

    private static NameFindResult? GetNameAtPosition(Tree.Expression.Table table, Position position)
    {
        foreach (var field in table.Fields)
        {
            if (GetNameAtPosition(field.Key, position) is { } foundKey)
            {
                return foundKey with
                {
                    GetTreeType = table.ValueLocation != null
                        ? ev => ev.GetTypeAtValueLocation(new ValueLocation.TableField(field, table.ValueLocation))
                        : null,
                };
            }

            if (GetNameAtPosition(field.Value, position) is { } foundValue)
            {
                return foundValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the name that lies on the given position by recursively descending the tree.
    /// </summary>
    /// <returns>
    /// The expression or type name under the given position, or null if it wasn't found.<br/>
    /// The returned node may be one of the following: Tree.Expression.Name, Tree.Type.Name, Tree.Expression.String, or
    /// Tree.Type.StringLiteral.
    /// </returns>
    private static NameFindResult? GetNameAtPosition(Tree? tree, Position position)
    {
        if (tree == null)
        {
            return null;
        }

        if (!tree.Range.Contains(position))
        {
            return null;
        }

        if (tree is Tree.Expression.Name or Tree.Type.Name or Tree.LabelName or Tree.Expression.String
            or Tree.Type.StringLiteral)
        {
            return new(tree, tree is Tree.Expression expression ? ev => ev.GetTypeOfExpression(expression) : null);
        }

        return tree switch
        {
            Tree.Expression.Binary binary => GetNameAtPosition(binary.Left, position) ??
                                             GetNameAtPosition(binary.Right, position),

            Tree.Expression.Access access => GetNameAtPosition(access.Target, position) ??
                                             (GetNameAtPosition(access.Key, position) is { } accessKey
                                                 // When hovering over the key of the access, we want to show the type
                                                 // of the access itself.
                                                 ? accessKey with { GetTreeType = ev => ev.GetTypeOfExpression(access) }
                                                 : null),

            Tree.Statement.Assignment assignment => GetNameAtPosition(assignment.Targets, position) ??
                                                    GetNameAtPosition(assignment.Values, position),

            Tree.Expression.Call call => GetNameAtPosition(call.Target, position) ??
                                         GetNameAtPosition(call.Arguments, position) ??
                                         GetNameAtPosition(call.TypeArguments, position),

            Tree.Declaration declaration => GetNameAtPosition(declaration.Name, position) ??
                                            GetNameAtPosition(declaration.Type, position),

            Tree.Statement.Do doBlock => GetNameAtPosition(doBlock.Body, position),

            Tree.Expression.Function function => GetNameAtPosition(function.Type, position) ??
                                                 GetNameAtPosition(function.Chunk, position),

            Tree.Type.Function functionType => GetNameAtPosition(functionType.Parameters, position) ??
                                               GetNameAtPosition(functionType.ReturnTypes, position) ??
                                               GetNameAtPosition(functionType.TypeParameters, position),

            Tree.Statement.If ifStmt => GetNameAtPosition(ifStmt.Primary, position) ??
                                        GetNameAtPosition(ifStmt.ElseIfs, position) ??
                                        GetNameAtPosition(ifStmt.ElseBody, position),

            Tree.Statement.IteratorFor iteratorFor => GetNameAtPosition(iteratorFor.Declarations, position) ??
                                                      GetNameAtPosition(iteratorFor.Iterator, position) ??
                                                      GetNameAtPosition(iteratorFor.Body, position),

            Tree.Expression.Unary unary => GetNameAtPosition(unary.Expression, position),

            Tree.Statement.VariableDeclaration variableDeclaration => GetNameAtPosition(
                                                                          variableDeclaration.Declarations, position) ??
                                                                      GetNameAtPosition(variableDeclaration.Values,
                                                                          position),

            Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration =>
                GetNameAtPosition(localFunctionDeclaration.Name, position) ??
                GetNameAtPosition(localFunctionDeclaration.Function, position),

            Tree.Expression.MethodCall methodCall => GetNameAtPosition(methodCall.Target, position) ??
                                                     GetNameAtPosition(methodCall.FuncName, position) ??
                                                     GetNameAtPosition(methodCall.Arguments, position),

            Tree.Statement.NumericalFor numericalFor => GetNameAtPosition(numericalFor.Counter, position) ??
                                                        GetNameAtPosition(numericalFor.Start, position) ??
                                                        GetNameAtPosition(numericalFor.Limit, position) ??
                                                        GetNameAtPosition(numericalFor.Step, position) ??
                                                        GetNameAtPosition(numericalFor.Body, position),

            Tree.Statement.RepeatUntil repeatUntil => GetNameAtPosition(repeatUntil.Body, position) ??
                                                      GetNameAtPosition(repeatUntil.Condition, position),

            Tree.Statement.Return returnStatement => GetNameAtPosition(returnStatement.Values, position),

            Tree.Expression.Table table => GetNameAtPosition(table, position),

            Tree.Type.Table table => GetNameAtPosition(table, position),

            Tree.Type.Nillable nillable => GetNameAtPosition(nillable.Inner, position),

            Tree.Statement.While whileStatement => GetNameAtPosition(whileStatement.Condition, position) ??
                                                   GetNameAtPosition(whileStatement.Body, position),

            Tree.TypeAliasDeclaration typeDeclaration => GetNameAtPosition(typeDeclaration.Name, position) ??
                                                         GetNameAtPosition(typeDeclaration.Type, position),

            Tree.Statement.Call call => GetNameAtPosition(call.CallExpr, position),
            Tree.Statement.MethodCall methodCall => GetNameAtPosition(methodCall.CallExpr, position),

            Tree.Statement.LabelDefinition label => GetNameAtPosition(label.Name, position),
            Tree.Statement.Goto @goto => GetNameAtPosition(@goto.Name, position),

            _ => null,
        };
    }

    private static NameFindResult? GetNameAtPosition(Tree.Block? block, Position position)
    {
        if (block == null)
        {
            return null;
        }

        return GetNameAtPosition(block.TypeDeclarations, position) ?? GetNameAtPosition(block.Statements, position);
    }

    /// <summary>
    /// Information about a tree node.
    /// </summary>
    /// <param name="Symbol">The symbol that the tree node is associated with.</param>
    /// <param name="Range">The range of the tree node.</param>
    /// <param name="GetTreeType">A function that returns the type of the tree node.</param>
    public record struct GetSymbolResult(Symbol? Symbol, Range Range, Func<TypeEvaluator, Type>? GetTreeType);

    /// <summary>
    /// Returns information about the tree node under the given position, if it exists.
    /// </summary>
    public static GetSymbolResult GetSymbolAtPosition(Project project, Source source, Position position)
    {
        var result = GetNameAtPosition(source.File, position);
        if (result is { Name: var name, GetTreeType: var getType })
        {
            return new(project.GetTreeSymbol(name), name.Range, getType);
        }

        return default;
    }
}