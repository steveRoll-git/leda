namespace Leda.Lang;

/// <summary>
/// Visits all the nodes in the syntax tree, and calls the Visit method on them.
/// </summary>
public abstract class Visitor
{
    protected virtual void Visit(Tree tree, Tree? parent)
    {
        Visit(tree);
    }

    protected virtual void Visit(Tree tree) { }

    /// <summary>
    /// Called after this node and all its contents have been visited.
    /// </summary>
    protected virtual void PostVisit(Tree tree) { }

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

    private void VisitAll<T>(List<T>? trees, Tree? parent) where T : Tree
    {
        if (trees == null)
        {
            return;
        }

        foreach (var tree in trees)
        {
            VisitAll(tree, parent);
            if (Stop)
            {
                return;
            }
        }
    }

    private void VisitAll(Tree.Block? block, Tree? parent)
    {
        if (block == null)
        {
            return;
        }

        foreach (var statement in block.Statements)
        {
            VisitAll(statement, parent);
            if (Stop)
            {
                return;
            }
        }

        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            VisitAll(typeDeclaration, parent);
            if (Stop)
            {
                return;
            }
        }
    }

    private void VisitAll(Tree.IfBranch branch, Tree? parent)
    {
        VisitAll(branch.Condition, parent);
        VisitAll(branch.Body, parent);
    }

    /// <summary>
    /// Recursively calls `Visit` on the tree and all its children.
    /// </summary>
    private void VisitAll(Tree? tree, Tree? parent)
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

        Visit(tree, parent);
        if (Stop)
        {
            return;
        }

        switch (tree)
        {
            case Tree.Declaration declaration:
                VisitAll(declaration.Name, tree);
                VisitAll(declaration.Type, tree);
                break;
            case Tree.Expression.Access access:
                VisitAll(access.Target, tree);
                VisitAll(access.Key, tree);
                break;
            case Tree.Expression.Binary binary:
                VisitAll(binary.Left, tree);
                VisitAll(binary.Right, tree);
                break;
            case Tree.Expression.Call call:
                VisitAll(call.Target, tree);
                VisitAll(call.Arguments, tree);
                VisitAll(call.TypeArguments, tree);
                break;
            case Tree.Expression.Function function:
                VisitAll(function.Type, tree);
                VisitAll(function.Chunk, tree);
                break;
            case Tree.Expression.MethodCall methodCall:
                VisitAll(methodCall.Target, tree);
                VisitAll(methodCall.FuncName, tree);
                VisitAll(methodCall.Arguments, tree);
                break;
            case Tree.Expression.Table table:
                foreach (var field in table.Fields)
                {
                    VisitAll(field.Key, tree);
                    VisitAll(field.Value, tree);
                    if (Stop)
                    {
                        return;
                    }
                }

                break;
            case Tree.Expression.Unary unary:
                VisitAll(unary.Expression, tree);
                break;
            case Tree.Statement.Assignment assignment:
                VisitAll(assignment.Targets, tree);
                VisitAll(assignment.Values, tree);
                break;
            case Tree.Statement.Call callStatement:
                VisitAll(callStatement.CallExpr, tree);
                break;
            case Tree.Statement.Do @do:
                VisitAll(@do.Body, tree);
                break;
            case Tree.Statement.Goto @goto:
                VisitAll(@goto.Name, tree);
                break;
            case Tree.Statement.If @if:
                VisitAll(@if.Primary, tree);
                foreach (var ifBranch in @if.ElseIfs)
                {
                    VisitAll(ifBranch, tree);
                    if (Stop)
                    {
                        return;
                    }
                }

                VisitAll(@if.ElseBody, tree);
                break;
            case Tree.Statement.IteratorFor iteratorFor:
                VisitAll(iteratorFor.Declarations, tree);
                VisitAll(iteratorFor.Iterator, tree);
                VisitAll(iteratorFor.Body, tree);
                break;
            case Tree.Statement.LabelDefinition labelDefinition:
                VisitAll(labelDefinition.Name, tree);
                break;
            case Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration:
                VisitAll(localFunctionDeclaration.Name, tree);
                VisitAll(localFunctionDeclaration.Function, tree);
                break;
            case Tree.Statement.MethodCall methodCallStatement:
                VisitAll(methodCallStatement.CallExpr, tree);
                break;
            case Tree.Statement.NumericalFor numericalFor:
                VisitAll(numericalFor.Counter, tree);
                VisitAll(numericalFor.Start, tree);
                VisitAll(numericalFor.Limit, tree);
                VisitAll(numericalFor.Step, tree);
                VisitAll(numericalFor.Body, tree);
                break;
            case Tree.Statement.RepeatUntil repeatUntil:
                VisitAll(repeatUntil.Body, tree);
                VisitAll(repeatUntil.Condition, tree);
                break;
            case Tree.Statement.Return @return:
                VisitAll(@return.Values, tree);
                break;
            case Tree.Statement.VariableDeclaration variableDeclaration: // Covers both local and global declarations.
                VisitAll(variableDeclaration.Declarations, tree);
                VisitAll(variableDeclaration.Values, tree);
                break;
            case Tree.Statement.While @while:
                VisitAll(@while.Condition, tree);
                VisitAll(@while.Body, tree);
                break;
            case Tree.TypeAliasDeclaration typeAliasDeclaration:
                VisitAll(typeAliasDeclaration.Name, tree);
                VisitAll(typeAliasDeclaration.Type, tree);
                break;
            case Tree.Type.Function function:
                VisitAll(function.Parameters, tree);
                VisitAll(function.ReturnTypes, tree);
                VisitAll(function.TypeParameters, tree);
                break;
            case Tree.Type.Nillable nillable:
                VisitAll(nillable.Inner, tree);
                break;
            case Tree.Type.Table table:
                foreach (var field in table.Fields)
                {
                    VisitAll(field.Key, tree);
                    VisitAll(field.Value, tree);
                    if (Stop)
                    {
                        return;
                    }
                }

                break;
            case Tree.Type.Array array:
                VisitAll(array.ElementType, tree);
                break;
        }

        PostVisit(tree);
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

    public void Start(Source source)
    {
        VisitAll(source.File, null);
    }

    /// <summary>
    /// Calls the given callback on all the nodes in the source's syntax tree.
    /// </summary>
    public static void VisitAllWithCallback(Source source, Action<Tree> callback)
    {
        new CallbackVisitor(callback).Start(source);
    }

    /// <summary>
    /// Runs `searchFunc` on all nodes until it returns a non-null value, and returns it.
    /// </summary>
    public static T? Search<T>(Source source, Func<Tree, T?> searchFunc, Func<Tree, bool> filterFunc)
    {
        var visitor = new SearchVisitor<T>(searchFunc, filterFunc);
        visitor.Start(source);
        return visitor.Result;
    }
}