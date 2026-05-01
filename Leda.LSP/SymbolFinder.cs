using Leda.Lang;
using Range = Leda.Lang.Range;

namespace Leda.LSP;

public static class SymbolFinder
{
    // TODO could the return type of all these methods be something more specific than the base Tree?

    private static Tree? GetNameAtPosition<T>(List<T>? trees, Position position) where T : Tree
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

    private static Tree? GetNameAtPosition(List<Tree.Type.Table.Field> fields, Position position)
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

    /// <summary>
    /// Finds the name that lies on the given position by recursively descending the tree.
    /// </summary>
    /// <returns>
    /// The expression or type name under the given position, or null if it wasn't found.<br/>
    /// The returned node may be one of the following: Tree.Expression.Name, Tree.Type.Name, Tree.Expression.String, or
    /// Tree.Type.StringLiteral.
    /// </returns>
    private static Tree? GetNameAtPosition(Tree? tree, Position position)
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
            return tree;
        }

        return tree switch
        {
            Tree.Expression.Binary binary => GetNameAtPosition(binary.Left, position) ??
                                             GetNameAtPosition(binary.Right, position),

            Tree.Expression.Access access => GetNameAtPosition(access.Key, position) ??
                                             GetNameAtPosition(access.Target, position),

            Tree.Statement.Assignment assignment => GetNameAtPosition(assignment.Targets, position) ??
                                                    GetNameAtPosition(assignment.Values, position),

            Tree.Expression.Call call => GetNameAtPosition(call.Target, position) ??
                                         GetNameAtPosition(call.Parameters, position),

            Tree.Declaration declaration => GetNameAtPosition(declaration.Name, position) ??
                                            GetNameAtPosition(declaration.Type, position),

            Tree.Statement.Do doBlock => GetNameAtPosition(doBlock.Body, position),

            Tree.Expression.Function function => GetNameAtPosition(function.Type, position) ??
                                                 GetNameAtPosition(function.Chunk, position),

            Tree.Type.Function functionType => GetNameAtPosition(functionType.Parameters, position) ??
                                               GetNameAtPosition(functionType.ReturnTypes, position) ??
                                               GetNameAtPosition(functionType.TypeParameters, position),

            Tree.Statement.GlobalDeclaration globalDeclaration => throw new NotImplementedException(),

            Tree.Statement.If ifStmt => GetNameAtPosition(ifStmt.Primary, position) ??
                                        GetNameAtPosition(ifStmt.ElseIfs, position) ??
                                        GetNameAtPosition(ifStmt.ElseBody, position),

            Tree.Statement.IteratorFor iteratorFor => GetNameAtPosition(iteratorFor.Declarations, position) ??
                                                      GetNameAtPosition(iteratorFor.Iterator, position) ??
                                                      GetNameAtPosition(iteratorFor.Body, position),

            Tree.Expression.Unary unary => GetNameAtPosition(unary.Expression, position),

            Tree.Statement.LocalDeclaration localDeclaration => GetNameAtPosition(localDeclaration.Declarations,
                                                                    position) ??
                                                                GetNameAtPosition(localDeclaration.Values, position),

            Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration =>
                GetNameAtPosition(localFunctionDeclaration.Name, position) ??
                GetNameAtPosition(localFunctionDeclaration.Function, position),

            Tree.Expression.MethodCall methodCall => GetNameAtPosition(methodCall.Target, position) ??
                                                     GetNameAtPosition(methodCall.FuncName, position) ??
                                                     GetNameAtPosition(methodCall.Parameters, position),

            Tree.Statement.NumericalFor numericalFor => GetNameAtPosition(numericalFor.Counter, position) ??
                                                        GetNameAtPosition(numericalFor.Start, position) ??
                                                        GetNameAtPosition(numericalFor.Limit, position) ??
                                                        GetNameAtPosition(numericalFor.Step, position) ??
                                                        GetNameAtPosition(numericalFor.Body, position),

            Tree.Statement.RepeatUntil repeatUntil => GetNameAtPosition(repeatUntil.Body, position) ??
                                                      GetNameAtPosition(repeatUntil.Condition, position),

            Tree.Statement.Return returnStatement => GetNameAtPosition(returnStatement.Values, position),

            Tree.Expression.Table table => GetNameAtPosition(table.Fields, position),

            Tree.Expression.Table.Field tableField => GetNameAtPosition(tableField.Key, position) ??
                                                      GetNameAtPosition(tableField.Value, position),

            Tree.Type.Table table => GetNameAtPosition(table.Fields, position),

            Tree.Type.Nillable nillable => GetNameAtPosition(nillable.Inner, position),

            Tree.Statement.While whileStatement => GetNameAtPosition(whileStatement.Condition, position) ??
                                                   GetNameAtPosition(whileStatement.Body, position),

            Tree.TypeAliasDeclaration typeDeclaration => GetNameAtPosition(typeDeclaration.Name, position) ??
                                                         GetNameAtPosition(typeDeclaration.Type, position),

            Tree.Statement.Call call => GetNameAtPosition(call.CallExpr, position),
            Tree.Statement.MethodCall methodCall => GetNameAtPosition(methodCall.CallExpr, position),

            Tree.Statement.LabelDefinition label => GetNameAtPosition(label.Name, position),
            Tree.Statement.Goto @goto => GetNameAtPosition(@goto.Name, position),

            _ => null
        };
    }

    private static Tree? GetNameAtPosition(Tree.Block? block, Position position)
    {
        if (block == null)
        {
            return null;
        }

        return GetNameAtPosition(block.TypeDeclarations, position) ?? GetNameAtPosition(block.Statements, position);
    }

    /// <summary>
    /// Returns the symbol and range of the tree node under the given position, if it exists.
    /// </summary>
    public static (Symbol? symbol, Range range) GetSymbolAtPosition(Source source, Position position)
    {
        var name = GetNameAtPosition(source.Chunk, position);
        if (name != null)
        {
            return (source.GetTreeSymbol(name), name.Range);
        }

        return default;
    }
}