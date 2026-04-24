namespace Leda.Lang;

/// <summary>
/// Visits each node of a Tree to create new Symbols for each declaration that's found, and associates names with
/// known Symbols.
/// </summary>
public class Binder
{
    private readonly Source source;
    private List<Diagnostic> Diagnostics { get; } = [];

    private class Scope(Tree.Chunk? chunk, Tree? loop) : Dictionary<string, Binding>
    {
        /// <summary>
        /// The chunk this scope is inside of.
        /// </summary>
        public Tree.Chunk? Chunk => chunk;

        /// <summary>
        /// The loop this scope is inside of.
        /// </summary>
        public Tree? Loop => loop;
    }

    /// <summary>
    /// A list of lexical scopes, where each scope is a dictionary of names with their symbols.
    /// </summary>
    private readonly List<Scope> scopes = [];

    /// <summary>
    /// Any name in the source code might refer to a value, or a type, or both.
    /// </summary>
    private class Binding(Symbol? value, Symbol? type)
    {
        public Symbol? ValueSymbol { get; set; } = value;
        public Symbol? TypeSymbol { get; set; } = type;
        public Symbol.Label? Label { get; set; }
    }

    private Scope CurrentScope => scopes[^1];

    private static readonly Scope InitialScope = new(null, null)
    {
        [Type.Any.Name!] = new(null, Symbol.AnyType),
        [Type.Boolean.Name!] = new(null, Symbol.BooleanType),
        [Type.NumberPrimitive.Name!] = new(null, Symbol.NumberType),
        [Type.StringPrimitive.Name!] = new(null, Symbol.StringType), // TODO stringlib should be a value here
        [Type.FunctionPrimitive.Name!] = new(null, Symbol.FunctionType)
    };

    private readonly Stack<AssignmentPath> assignmentPathStack = [];

    private readonly Dictionary<Tree.LabelName, FlowNode> labelFlowNodes = [];

    private Binder(Source source)
    {
        this.source = source;

        scopes.Add(InitialScope);
        PushChunkScope(source.Chunk);
    }

    private void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Pushes a new scope with the same chunk and loop as the scope before it.
    /// </summary>
    private void PushScope()
    {
        scopes.Add(new Scope(scopes[^1].Chunk, scopes[^1].Loop));
    }

    /// <summary>
    /// Pushes a new scope that belongs to a new chunk.
    /// </summary>
    private void PushChunkScope(Tree.Chunk chunk)
    {
        scopes.Add(new Scope(chunk, null));
    }

    /// <summary>
    /// Pushes a new scope that belongs to the same chunk as the last scope, but where a loop begins.
    /// </summary>
    private void PushLoopScope(Tree loop)
    {
        scopes.Add(new Scope(scopes[^1].Chunk, loop));
    }

    private void PopScope()
    {
        scopes.RemoveAt(scopes.Count - 1);
    }

    /// <summary>
    /// Attempts to find a symbol by its name.
    /// </summary>
    /// <param name="name">The name of the symbol to look for.</param>
    /// <param name="context">The context in which the name appears.</param>
    /// <returns>The symbol and scope it resides in if it exists, or `null` if it wasn't found.</returns>
    private (Symbol? symbol, Scope? scope) TryGetBinding(string name, Tree.NameContext context)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            if (scopes[i].TryGetValue(name, out var binding))
            {
                var symbol = context switch
                {
                    Tree.NameContext.Value => binding.ValueSymbol,
                    Tree.NameContext.Type => binding.TypeSymbol,
                    Tree.NameContext.Label => binding.Label,
                    _ => null
                };

                if (symbol != null)
                {
                    return (symbol, scopes[i]);
                }
            }
        }

        return default;
    }

    /// <summary>
    /// Finds the value symbol that a name refers to.
    /// </summary>
    private Symbol? TryGetBinding(Tree.Expression.Name name)
    {
        return TryGetBinding(name.Value, Tree.NameContext.Value).symbol;
    }

    /// <summary>
    /// Finds the type symbol that a type name refers to.
    /// </summary>
    private Symbol? TryGetBinding(Tree.Type.Name name)
    {
        return TryGetBinding(name.Value, Tree.NameContext.Type).symbol;
    }

    /// <summary>
    /// Adds a named symbol to the current scope. Reports a diagnostic if a symbol with this name has already been
    /// declared in the same scope.
    /// </summary>
    private void AddSymbol(Tree node, string name, Tree.NameContext context, Symbol symbol)
    {
        // TODO report warning if a name is shadowed
        if (TryGetBinding(name, context) is (not null, { } existingScope) && existingScope == CurrentScope)
        {
            Report(new Diagnostic.NameAlreadyDeclared(node.Range, context, name));
        }

        if (!CurrentScope.TryGetValue(name, out var currentBinding))
        {
            currentBinding = new Binding(null, null);
            CurrentScope[name] = currentBinding;
        }

        if (context == Tree.NameContext.Value)
        {
            currentBinding.ValueSymbol = symbol;
        }
        else if (context == Tree.NameContext.Type)
        {
            currentBinding.TypeSymbol = symbol;
        }
        else
        {
            currentBinding.Label = symbol as Symbol.Label;
        }

        source.AttachSymbol(node, symbol, true);
    }

    private void AddSymbol(Tree.Expression.Name name, Symbol symbol)
    {
        AddSymbol(name, name.Value, Tree.NameContext.Value, symbol);
    }

    private void AddSymbol(Tree.Type.Name name, Symbol symbol)
    {
        AddSymbol(name, name.Value, Tree.NameContext.Type, symbol);
    }

    private void Visit(Tree.Expression expr)
    {
        switch (expr)
        {
            case Tree.Expression.Access access:
                Visit(access);
                break;
            case Tree.Expression.Binary binary:
                Visit(binary);
                break;
            case Tree.Expression.Call call:
                Visit(call);
                break;
            case Tree.Expression.Function function:
                Visit(function);
                break;
            case Tree.Expression.MethodCall methodCall:
                Visit(methodCall);
                break;
            case Tree.Expression.Name name:
                Visit(name);
                break;
            case Tree.Expression.Table table:
                Visit(table);
                break;
            case Tree.Expression.Unary unary:
                Visit(unary);
                break;
        }
    }

    private void Visit(List<Tree.Expression> expressions)
    {
        foreach (var expression in expressions)
        {
            Visit(expression);
        }
    }

    private void Visit(List<Tree.Expression> expressions, Tree parent)
    {
        for (var i = 0; i < expressions.Count; i++)
        {
            assignmentPathStack.Push(parent switch
                {
                    Tree.Statement.LocalDeclaration localDeclaration =>
                        new AssignmentPath.LocalVariable(localDeclaration, i),
                    Tree.Statement.Assignment assignment =>
                        new AssignmentPath.AssignmentValue(assignment, i),
                    Tree.Expression.Call call =>
                        new AssignmentPath.Argument(call, i),
                    Tree.Expression.MethodCall => throw new NotImplementedException(),
                    Tree.Statement.Return returnStmt =>
                        new AssignmentPath.ReturnValue(returnStmt, i),
                    _ => throw new Exception() // Unreachable.
                }
            );

            var expression = expressions[i];
            Visit(expression);

            assignmentPathStack.Pop();
        }
    }

    private void Visit(Tree.Expression.MethodCall methodCall)
    {
        Visit(methodCall.Target);
        Visit(methodCall.Parameters, methodCall);
    }

    private void Visit(Tree.Expression.Call call)
    {
        Visit(call.Target);
        Visit(call.Parameters, call);
        if (call.TypeParameters != null)
        {
            Visit(call.TypeParameters);
        }
    }

    private void Visit(Tree.Expression.Access access)
    {
        Visit(access.Target);
        Visit(access.Key);
    }

    private void Visit(Tree.Expression.Binary binary)
    {
        Visit(binary.Left);
        Visit(binary.Right);
    }

    private void Visit(Tree.Expression.Unary unary)
    {
        Visit(unary.Expression);
    }

    private void Visit(Tree.Expression.Function function)
    {
        if (assignmentPathStack.TryPeek(out var path))
        {
            function.AssignmentPath = path with { TableFields = [..path.TableFields] };
        }

        PushChunkScope(function.Chunk);
        VisitFunction(function);
        PopScope();
    }

    private void VisitFunction(Tree.Expression.Function function)
    {
        Visit(function.Type);

        for (var i = 0; i < function.Type.Parameters.Count; i++)
        {
            var parameter = function.Type.Parameters[i];
            AddSymbol(parameter.Name, new Symbol.Parameter(function, i));
        }

        VisitChunk(function.Chunk);
    }

    private void Visit(Tree.Expression.Name name)
    {
        if (TryGetBinding(name) is { } symbol)
        {
            source.AttachSymbol(name, symbol);
        }
        else
        {
            // TODO defer to check for global
            Report(new Diagnostic.NameNotFound(name.Range, name.Value, Tree.NameContext.Value));
        }
    }

    private void Visit(Tree.Expression.Table table)
    {
        foreach (var field in table.Fields)
        {
            assignmentPathStack.TryPeek(out var assignmentPath);
            assignmentPath?.TableFields.Add(field.Key);

            Visit(field.Key);
            Visit(field.Value);

            assignmentPath?.TableFields.RemoveAt(assignmentPath.TableFields.Count - 1);
        }
    }

    private void Visit(Tree.Type type)
    {
        switch (type)
        {
            case Tree.Type.Function function:
                PushScope(); // For type parameters.
                Visit(function);
                PopScope();
                break;
            case Tree.Type.Name name:
                Visit(name);
                break;
            case Tree.Type.Table table:
                Visit(table);
                break;
        }
    }

    private void Visit(List<Tree.Type> types)
    {
        foreach (var type in types)
        {
            Visit(type);
        }
    }

    private void Visit(Tree.Type.Name name)
    {
        if (TryGetBinding(name) is { } symbol)
        {
            source.AttachSymbol(name, symbol);
        }
        else
        {
            // TODO defer to check for global
            Report(new Diagnostic.NameNotFound(name.Range, name.Value, Tree.NameContext.Type));
        }
    }

    private void Visit(Tree.Type.Function functionType)
    {
        if (functionType.TypeParameters != null)
        {
            VisitTypeParameterDeclaration(functionType.TypeParameters);
        }

        foreach (var parameter in functionType.Parameters)
        {
            if (parameter.Type != null)
            {
                Visit(parameter.Type);
            }
        }

        if (functionType.ReturnTypes != null)
        {
            Visit(functionType.ReturnTypes);
        }
    }

    private void Visit(Tree.Type.Table table)
    {
        foreach (var field in table.Fields)
        {
            Visit(field.Key);
            Visit(field.Value);
        }
    }

    private void VisitTypeParameterDeclaration(List<Tree.Type.Name> typeParameters)
    {
        foreach (var typeParameter in typeParameters)
        {
            AddSymbol(typeParameter, new Symbol.TypeParameter(typeParameter));
        }
    }

    private FlowNode? VisitStatement(Tree.Statement stmt, FlowNode? antecedent)
    {
        switch (stmt)
        {
            case Tree.Statement.Call call:
                Visit(call.CallExpr);
                break;
            case Tree.Statement.MethodCall methodCall:
                Visit(methodCall.CallExpr);
                break;
            case Tree.Statement.Assignment assignment:
                return VisitStatement(assignment, antecedent);
            case Tree.Statement.Do @do:
                return VisitStatement(@do, antecedent);
            case Tree.Statement.GlobalDeclaration globalDeclaration:
                return VisitStatement(globalDeclaration, antecedent);
            case Tree.Statement.If @if:
                return VisitStatement(@if, antecedent);
            case Tree.Statement.IteratorFor iteratorFor:
                return VisitStatement(iteratorFor, antecedent);
            case Tree.Statement.LocalDeclaration localDeclaration:
                return VisitStatement(localDeclaration, antecedent);
            case Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration:
                VisitStatement(localFunctionDeclaration);
                break;
            case Tree.Statement.NumericalFor numericalFor:
                return VisitStatement(numericalFor, antecedent);
            case Tree.Statement.RepeatUntil repeatUntil:
                return VisitStatement(repeatUntil, antecedent);
            case Tree.Statement.While @while:
                return VisitStatement(@while, antecedent);
            case Tree.Statement.Return @return:
                VisitStatement(@return);
                return null;
            case Tree.Statement.Break @break:
                VisitStatement(@break);
                return null;
            case Tree.Statement.LabelDefinition label:
                return VisitStatement(label, antecedent);
            case Tree.Statement.Goto @goto:
                VisitStatement(@goto, antecedent);
                return null;
        }

        return antecedent;
    }

    private FlowNode? VisitStatement(Tree.Statement.Do block, FlowNode? antecedent)
    {
        PushScope();
        var descendent = VisitBlock(block.Body, antecedent);
        PopScope();

        return descendent;
    }

    private FlowNode? VisitStatement(Tree.Statement.Assignment assignment, FlowNode? antecedent)
    {
        Visit(assignment.Targets);
        Visit(assignment.Values, assignment);
        return antecedent;
    }

    private void VisitStatement(Tree.Statement.Return returnStatement)
    {
        Visit(returnStatement.Values, returnStatement);
    }

    private void VisitStatement(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        AddSymbol(declaration.Name, new Symbol.LocalFunction(declaration));
        PushChunkScope(declaration.Function.Chunk);
        VisitFunction(declaration.Function);
        PopScope();
    }

    private FlowNode VisitStatement(Tree.Statement.GlobalDeclaration declaration, FlowNode? antecedent)
    {
        throw new NotImplementedException();
    }

    private FlowNode? VisitStatement(Tree.Statement.LocalDeclaration localDeclaration, FlowNode? antecedent)
    {
        Visit(localDeclaration.Values, localDeclaration);

        for (var i = 0; i < localDeclaration.Declarations.Count; i++)
        {
            var declaration = localDeclaration.Declarations[i];
            AddSymbol(declaration.Name, new Symbol.LocalVariable(localDeclaration, i));
            if (declaration.Type != null)
            {
                Visit(declaration.Type);
            }
        }

        return antecedent;
    }

    private FlowNode? VisitIfBranch(Tree.IfBranch branch, FlowNode? antecedent)
    {
        Visit(branch.Condition);
        PushScope();
        var descendent = VisitBlock(branch.Body, antecedent);
        PopScope();

        return descendent;
    }

    private FlowNode? VisitStatement(Tree.Statement.If ifStatement, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();

        AddIfNotNull(descendents, VisitIfBranch(ifStatement.Primary, antecedent));

        foreach (var branch in ifStatement.ElseIfs)
        {
            AddIfNotNull(descendents, VisitIfBranch(branch, antecedent));
        }

        if (ifStatement.ElseBody != null)
        {
            PushScope();
            AddIfNotNull(descendents, VisitBlock(ifStatement.ElseBody, antecedent));
            PopScope();
        }
        else
        {
            AddIfNotNull(descendents, antecedent);
        }

        return descendents.Count > 0 ? new(descendents) : null;
    }

    private FlowNode? VisitStatement(Tree.Statement.RepeatUntil repeatUntil, FlowNode? antecedent)
    {
        PushLoopScope(repeatUntil);
        var descendent = VisitBlock(repeatUntil.Body, antecedent);
        Visit(repeatUntil.Condition);
        PopScope();

        return descendent;
    }

    private FlowNode VisitStatement(Tree.Statement.While whileLoop, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();
        AddIfNotNull(descendents, antecedent);

        Visit(whileLoop.Condition);
        PushLoopScope(whileLoop);
        AddIfNotNull(descendents, VisitBlock(whileLoop.Body, antecedent));
        PopScope();

        return new FlowNode(descendents);
    }

    private FlowNode VisitStatement(Tree.Statement.IteratorFor forLoop, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();
        AddIfNotNull(descendents, antecedent);

        Visit(forLoop.Iterator);

        PushLoopScope(forLoop);

        for (var i = 0; i < forLoop.Declarations.Count; i++)
        {
            var declaration = forLoop.Declarations[i];
            AddSymbol(declaration.Name, new Symbol.ForVariable(forLoop, i));
        }

        AddIfNotNull(descendents, VisitBlock(forLoop.Body, antecedent));

        PopScope();

        return new FlowNode(descendents);
    }

    private FlowNode VisitStatement(Tree.Statement.NumericalFor numericalFor, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();
        AddIfNotNull(descendents, antecedent);

        PushLoopScope(numericalFor);
        Visit(numericalFor.Start);
        Visit(numericalFor.Limit);
        if (numericalFor.Step != null)
        {
            Visit(numericalFor.Step);
        }

        AddSymbol(numericalFor.Counter, new Symbol.NumericForCounter(numericalFor));
        AddIfNotNull(descendents, VisitBlock(numericalFor.Body, antecedent));
        PopScope();

        return new FlowNode(descendents);
    }

    private void VisitStatement(Tree.Statement.Break brk)
    {
        if (scopes[^1].Loop == null)
        {
            Report(new Diagnostic.BreakOutsideOfLoop(brk.Range));
        }
    }

    private FlowNode? VisitStatement(Tree.Statement.LabelDefinition label, FlowNode? antecedent)
    {
        if (!labelFlowNodes.TryGetValue(label.Name, out var flowNode))
        {
            return null;
        }

        if (antecedent != null)
        {
            flowNode.Antecedents.Add(antecedent);
        }

        return flowNode;
    }

    private void VisitStatement(Tree.Statement.Goto @goto, FlowNode? antecedent)
    {
        var name = @goto.Name;

        if (TryGetBinding(name.Value, Tree.NameContext.Label) is ({ } symbol, { } scope) &&
            scope.Chunk == scopes[^1].Chunk)
        {
            source.AttachSymbol(name, symbol);
            if (labelFlowNodes.TryGetValue(name, out var flowNode))
            {
                AddIfNotNull(flowNode.Antecedents, antecedent);
            }
        }
        else
        {
            Report(new Diagnostic.NameNotFound(name.Range, name.Value, Tree.NameContext.Label));
        }
    }

    private FlowNode? VisitBlock(Tree.Block block, FlowNode? antecedent)
    {
        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            AddSymbol(typeDeclaration.Name, new Symbol.TypeAlias(typeDeclaration));
        }

        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            Visit(typeDeclaration.Type);
        }

        foreach (var label in block.Labels)
        {
            AddSymbol(label.Name, label.Name.Value, Tree.NameContext.Label, new Symbol.Label(label.Name));
            labelFlowNodes[label.Name] = new FlowNode([]);
        }

        // If multiple consecutive statements are unreachable, we report just one diagnostic for all of them.
        Range? unreachableRange = null;
        // If the block's antecedent is null, the entire block is unreachable. In this case, the unreachable diagnostic
        // will be reported by the parent block, so we don't have to here.
        var entireBlockUnreachable = antecedent == null;

        var descendant = antecedent;

        foreach (var statement in block.Statements)
        {
            var previous = descendant;
            descendant = VisitStatement(statement, descendant);

            if (!entireBlockUnreachable)
            {
                if (descendant == null && previous == null)
                {
                    unreachableRange = unreachableRange is { } range ? range.Union(statement.Range) : statement.Range;
                }
                else if (unreachableRange is { } range)
                {
                    Report(new Diagnostic.UnreachableCode(range));
                    unreachableRange = null;
                }
            }
        }

        if (unreachableRange is { } warnRange)
        {
            Report(new Diagnostic.UnreachableCode(warnRange));
        }

        return descendant;
    }

    private void VisitChunk(Tree.Chunk chunk)
    {
        var startNode = new FlowNode([]);
        var descendent = VisitBlock(chunk, startNode);
        chunk.AllPathsReturn = descendent == null;
    }

    /// <summary>
    /// Visits all nodes in the given tree and updates the infoStore with the relevant information.
    /// </summary>
    public static List<Diagnostic> Bind(Source source, Tree.Chunk chunk)
    {
        var binder = new Binder(source);
        binder.VisitChunk(chunk);
        return binder.Diagnostics;
    }

    /// <summary>
    /// Adds the item to the list if it isn't null.
    /// </summary>
    private static void AddIfNotNull<T>(List<T> list, T? item)
    {
        if (item != null)
        {
            list.Add(item);
        }
    }
}