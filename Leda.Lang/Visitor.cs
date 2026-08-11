namespace Leda.Lang;

/// <summary>
/// Visits all the nodes in the syntax tree, and calls the Visit method on them.
/// </summary>
public abstract class Visitor
{
    protected abstract void Visit(Tree tree);

    /// <summary>
    /// Only nodes for which this function returns `true` will be visited recursively.
    /// </summary>
    protected virtual bool Filter(Tree tree)
    {
        return true;
    }

    /// <summary>
    /// If this is true, no more nodes will be visited.
    /// </summary>
    protected bool Stop { get; set; }

    private void VisitAll<T>(List<T>? trees) where T : Tree
    {
        if (trees == null)
        {
            return;
        }

        foreach (var tree in trees)
        {
            VisitAll(tree);
            if (Stop)
            {
                return;
            }
        }
    }

    private void VisitAll(Tree.Block? block)
    {
        if (block == null)
        {
            return;
        }

        foreach (var statement in block.Statements)
        {
            VisitAll(statement);
            if (Stop)
            {
                return;
            }
        }

        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            VisitAll(typeDeclaration);
            if (Stop)
            {
                return;
            }
        }
    }

    private void VisitAll(Tree.IfBranch branch)
    {
        VisitAll(branch.Condition);
        VisitAll(branch.Body);
    }

    /// <summary>
    /// Recursively calls `Visit` on the tree and all its children.
    /// </summary>
    private void VisitAll(Tree? tree)
    {
        if (Stop)
        {
            return;
        }

        if (tree == null)
        {
            return;
        }

        if (!Filter(tree))
        {
            return;
        }

        Visit(tree);
        if (Stop)
        {
            return;
        }

        switch (tree)
        {
            case Tree.Declaration declaration:
                VisitAll(declaration.Name);
                VisitAll(declaration.Type);
                break;
            case Tree.Expression.Access access:
                VisitAll(access.Target);
                VisitAll(access.Key);
                break;
            case Tree.Expression.Binary binary:
                VisitAll(binary.Left);
                VisitAll(binary.Right);
                break;
            case Tree.Expression.Call call:
                VisitAll(call.Target);
                VisitAll(call.Arguments);
                VisitAll(call.TypeArguments);
                break;
            case Tree.Expression.Function function:
                VisitAll(function.Type);
                VisitAll(function.Chunk);
                break;
            case Tree.Expression.MethodCall methodCall:
                VisitAll(methodCall.Target);
                VisitAll(methodCall.FuncName);
                VisitAll(methodCall.Arguments);
                break;
            case Tree.Expression.Table table:
                foreach (var field in table.Fields)
                {
                    VisitAll(field.Key);
                    VisitAll(field.Value);
                    if (Stop)
                    {
                        return;
                    }
                }

                break;
            case Tree.Expression.Unary unary:
                VisitAll(unary.Expression);
                break;
            case Tree.Statement.Assignment assignment:
                VisitAll(assignment.Targets);
                VisitAll(assignment.Values);
                break;
            case Tree.Statement.Call callStatement:
                VisitAll(callStatement.CallExpr);
                break;
            case Tree.Statement.Do @do:
                VisitAll(@do.Body);
                break;
            case Tree.Statement.Goto @goto:
                VisitAll(@goto.Name);
                break;
            case Tree.Statement.If @if:
                VisitAll(@if.Primary);
                foreach (var ifBranch in @if.ElseIfs)
                {
                    VisitAll(ifBranch);
                    if (Stop)
                    {
                        return;
                    }
                }

                VisitAll(@if.ElseBody);
                break;
            case Tree.Statement.IteratorFor iteratorFor:
                VisitAll(iteratorFor.Declarations);
                VisitAll(iteratorFor.Iterator);
                VisitAll(iteratorFor.Body);
                break;
            case Tree.Statement.LabelDefinition labelDefinition:
                VisitAll(labelDefinition.Name);
                break;
            case Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration:
                VisitAll(localFunctionDeclaration.Name);
                VisitAll(localFunctionDeclaration.Function);
                break;
            case Tree.Statement.MethodCall methodCallStatement:
                VisitAll(methodCallStatement.CallExpr);
                break;
            case Tree.Statement.NumericalFor numericalFor:
                VisitAll(numericalFor.Counter);
                VisitAll(numericalFor.Start);
                VisitAll(numericalFor.Limit);
                VisitAll(numericalFor.Step);
                VisitAll(numericalFor.Body);
                break;
            case Tree.Statement.RepeatUntil repeatUntil:
                VisitAll(repeatUntil.Body);
                VisitAll(repeatUntil.Condition);
                break;
            case Tree.Statement.Return @return:
                VisitAll(@return.Values);
                break;
            case Tree.Statement.VariableDeclaration variableDeclaration: // Covers both local and global declarations.
                VisitAll(variableDeclaration.Declarations);
                VisitAll(variableDeclaration.Values);
                break;
            case Tree.Statement.While @while:
                VisitAll(@while.Condition);
                VisitAll(@while.Body);
                break;
            case Tree.TypeAliasDeclaration typeAliasDeclaration:
                VisitAll(typeAliasDeclaration.Name);
                VisitAll(typeAliasDeclaration.Type);
                break;
            case Tree.Type.Function function:
                VisitAll(function.Parameters);
                VisitAll(function.ReturnTypes);
                VisitAll(function.TypeParameters);
                break;
            case Tree.Type.Nillable nillable:
                VisitAll(nillable.Inner);
                break;
            case Tree.Type.Table table:
                foreach (var field in table.Fields)
                {
                    VisitAll(field.Key);
                    VisitAll(field.Value);
                    if (Stop)
                    {
                        return;
                    }
                }

                break;
            case Tree.Type.Array array:
                VisitAll(array.ElementType);
                break;
        }
    }

    private class CallbackVisitor(Action<Tree> callback) : Visitor
    {
        protected override void Visit(Tree tree)
        {
            callback(tree);
        }
    }

    private class SearchVisitor<T>(Func<Tree, T?> searchFunc, Func<Tree, bool> filterFunc) : Visitor
    {
        internal T? Result { get; private set; }

        protected override bool Filter(Tree tree)
        {
            return filterFunc(tree);
        }

        protected override void Visit(Tree tree)
        {
            Result = searchFunc(tree);
            if (Result != null)
            {
                Stop = true;
            }
        }
    }

    /// <summary>
    /// Calls the given callback on all the nodes in the source's syntax tree.
    /// </summary>
    public static void VisitAllWithCallback(Source source, Action<Tree> callback)
    {
        new CallbackVisitor(callback).VisitAll(source.File);
    }

    /// <summary>
    /// Runs `searchFunc` on all nodes until it returns a non-null value, and returns it.
    /// </summary>
    public static T? Search<T>(Source source, Func<Tree, T?> searchFunc, Func<Tree, bool> filterFunc)
    {
        var visitor = new SearchVisitor<T>(searchFunc, filterFunc);
        visitor.VisitAll(source.File);
        return visitor.Result;
    }
}