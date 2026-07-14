namespace Leda.Lang;

/// <summary>
/// Visits each node of a Tree to create new Symbols for each declaration that's found, and associates names with
/// known Symbols.<br/>
/// Additionally, creates the control flow graph used later by the Checker.
/// </summary>
public class Binder
{
    private readonly Source source;
    private List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// Stores information about a scope including which symbols are defined inside it, and which chunk and loop contain
    /// it.
    /// </summary>
    private class Scope(Tree.Chunk? chunk, Tree? loop) : Dictionary<string, Binding>
    {
        /// <summary>
        /// The chunk that contains this scope.
        /// </summary>
        public Tree.Chunk? Chunk => chunk;

        /// <summary>
        /// The loop that contains this scope.
        /// </summary>
        public Tree? Loop => loop;
    }

    /// <summary>
    /// A list of lexical scopes, where each scope is a dictionary of names with their symbols.
    /// </summary>
    private readonly List<Scope> scopes = [];

    /// <summary>
    /// Stores the different kinds of symbols which a name is bound to at a given scope.
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
        [Type.Nil.Name!] = new(null, Symbol.NilType),
        [Type.Boolean.Name!] = new(null, Symbol.BooleanType),
        [Type.NumberPrimitive.Name!] = new(null, Symbol.NumberType),
        [Type.StringPrimitive.Name!] = new(null, Symbol.StringType), // TODO stringlib should be a value here
        [Type.FunctionPrimitive.Name!] = new(null, Symbol.FunctionType),
    };

    private readonly Stack<AssignmentPath> assignmentPathStack = [];

    private readonly Dictionary<Tree.LabelName, FlowNode.Label> labelFlowNodes = [];

    private record struct ConditionBranch(FlowNode? FalseBranch, FlowNode? TrueBranch);

    private Binder(Source source)
    {
        this.source = source;

        scopes.Add(InitialScope);
        PushChunkScope(source.File);
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
        scopes.Add(new Scope(CurrentScope.Chunk, CurrentScope.Loop));
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
        scopes.Add(new Scope(CurrentScope.Chunk, loop));
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
                    _ => null,
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

    private void VisitExpression(Tree.Expression expression, FlowNode? flowNode)
    {
        expression.FlowNode = flowNode;
        switch (expression)
        {
            case Tree.Expression.Access access:
                VisitExpression(access, flowNode);
                break;
            case Tree.Expression.Binary binary:
                VisitExpression(binary, flowNode);
                break;
            case Tree.Expression.Call call:
                VisitExpression(call, flowNode);
                break;
            case Tree.Expression.Function function:
                VisitExpression(function);
                break;
            case Tree.Expression.MethodCall methodCall:
                VisitExpression(methodCall, flowNode);
                break;
            case Tree.Expression.Name name:
                VisitExpression(name);
                break;
            case Tree.Expression.Table table:
                VisitExpression(table, flowNode);
                break;
            case Tree.Expression.Unary unary:
                VisitExpression(unary, flowNode);
                break;
        }
    }

    /// <summary>
    /// Visits the expression and returns its true and false branches,
    /// and correctly sets the inner expressions' FlowNodes.
    /// </summary>
    private ConditionBranch VisitExpressionFlowNodes(Tree.Expression expression, FlowNode? antecedent)
    {
        if (expression is Tree.Expression.Unary { Operator.Kind: TokenKind.Not, Expression: var notInner })
        {
            var (falseBranch, trueBranch) = VisitExpressionFlowNodes(notInner, antecedent);
            return new(trueBranch, falseBranch);
        }

        if (expression is Tree.Expression.Binary { Operator.Kind: TokenKind.And or TokenKind.Or } binary)
        {
            var (leftFalse, leftTrue) = VisitExpressionFlowNodes(binary.Left, antecedent);
            if (binary.Operator.Kind == TokenKind.And)
            {
                var (rightFalse, rightTrue) = VisitExpressionFlowNodes(binary.Right, leftTrue);
                var falseAntecedents = new List<FlowNode>();
                AddIfNotNull(falseAntecedents, leftFalse);
                AddIfNotNull(falseAntecedents, rightFalse);
                return new(FlowNode.FromAntecedents(falseAntecedents), rightTrue);
            }
            else
            {
                var (rightFalse, rightTrue) = VisitExpressionFlowNodes(binary.Right, leftFalse);
                var trueAntecedents = new List<FlowNode>();
                AddIfNotNull(trueAntecedents, leftTrue);
                AddIfNotNull(trueAntecedents, rightTrue);
                return new(rightFalse, FlowNode.FromAntecedents(trueAntecedents));
            }
        }

        VisitExpression(expression, antecedent);
        return antecedent == null
            ? default
            : new(
                new FlowNode.Condition(antecedent, expression, false),
                new FlowNode.Condition(antecedent, expression, true));
    }

    private void Visit(List<Tree.Expression> expressions, FlowNode? flowNode)
    {
        foreach (var expression in expressions)
        {
            VisitExpression(expression, flowNode);
        }
    }

    private void Visit(List<Tree.Expression> expressions, Tree parent, FlowNode? flowNode)
    {
        for (var i = 0; i < expressions.Count; i++)
        {
            assignmentPathStack.Push(parent switch
                {
                    Tree.Statement.VariableDeclaration variableDeclaration =>
                        new AssignmentPath.Variable(variableDeclaration, i),
                    Tree.Statement.Assignment assignment =>
                        new AssignmentPath.AssignmentValue(assignment, i),
                    Tree.Expression.Call call =>
                        new AssignmentPath.Argument(call, i),
                    Tree.Expression.MethodCall => throw new NotImplementedException(),
                    Tree.Statement.Return returnStmt =>
                        new AssignmentPath.ReturnValue(returnStmt, i),
                    _ => throw new Exception(), // Unreachable.
                }
            );

            var expression = expressions[i];
            VisitExpression(expression, flowNode);

            assignmentPathStack.Pop();
        }
    }

    private void VisitExpression(Tree.Expression.MethodCall methodCall, FlowNode? flowNode)
    {
        VisitExpression(methodCall.Target, flowNode);
        Visit(methodCall.Arguments, methodCall, flowNode);
    }

    private void VisitExpression(Tree.Expression.Call call, FlowNode? flowNode)
    {
        VisitExpression(call.Target, flowNode);
        Visit(call.Arguments, call, flowNode);
        if (call.TypeArguments != null)
        {
            Visit(call.TypeArguments);
        }
    }

    private void VisitExpression(Tree.Expression.Access access, FlowNode? flowNode)
    {
        VisitExpression(access.Target, flowNode);
        VisitExpression(access.Key, flowNode);
    }

    private void VisitExpression(Tree.Expression.Binary binary, FlowNode? flowNode)
    {
        if (binary.Operator.Kind is TokenKind.And or TokenKind.Or)
        {
            VisitExpressionFlowNodes(binary, flowNode);
        }
        else
        {
            VisitExpression(binary.Left, flowNode);
            VisitExpression(binary.Right, flowNode);
        }
    }

    private void VisitExpression(Tree.Expression.Unary unary, FlowNode? flowNode)
    {
        VisitExpression(unary.Expression, flowNode);
    }

    private void VisitExpression(Tree.Expression.Function function)
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

    private void VisitExpression(Tree.Expression.Name name)
    {
        if (TryGetBinding(name) is { } symbol)
        {
            source.AttachSymbol(name, symbol);
        }
        else
        {
            if (!source.GlobalNames.TryGetValue(name.Value, out var names))
            {
                names = [];
                source.GlobalNames[name.Value] = names;
            }

            names.Add(name);
        }
    }

    private void VisitExpression(Tree.Expression.Table table, FlowNode? flowNode)
    {
        foreach (var field in table.Fields)
        {
            assignmentPathStack.TryPeek(out var assignmentPath);
            assignmentPath?.TableFields.Add(field.Key);

            VisitExpression(field.Key, flowNode);
            VisitExpression(field.Value, flowNode);

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
            case Tree.Type.Nillable { Inner: var inner }:
                Visit(inner);
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

    private FlowNode? VisitStatement(Tree.Statement statement, FlowNode? antecedent)
    {
        statement.FlowNode = antecedent;
        switch (statement)
        {
            case Tree.Statement.Call call:
                VisitExpression(call.CallExpr, antecedent);
                break;
            case Tree.Statement.MethodCall methodCall:
                VisitExpression(methodCall.CallExpr, antecedent);
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
                VisitStatement(@return, antecedent);
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

    private FlowNode.Assignment? VisitStatement(Tree.Statement.Assignment assignment, FlowNode? antecedent)
    {
        Visit(assignment.Targets, antecedent);
        Visit(assignment.Values, assignment, antecedent);
        return antecedent == null ? null : new FlowNode.Assignment(antecedent, assignment);
    }

    private void VisitStatement(Tree.Statement.Return returnStatement, FlowNode? antecedent)
    {
        Visit(returnStatement.Values, returnStatement, antecedent);
    }

    private void VisitStatement(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        AddSymbol(declaration.Name, new Symbol.LocalFunction(declaration));
        PushChunkScope(declaration.Function.Chunk);
        VisitFunction(declaration.Function);
        PopScope();
    }

    /// <summary>
    /// Returns whether a declared variable is not initialized with a value.
    /// </summary>
    public static bool IsVariableUninitialized(Tree.Statement.VariableDeclaration declaration, int index)
    {
        return index >= declaration.Values.Count &&
               (declaration.Values.Count <= 0 ||
                declaration.Values[^1] is not Tree.Expression.Call or Tree.Expression.Vararg);
    }

    private FlowNode.VariableDeclaration? VisitStatement(Tree.Statement.LocalDeclaration localDeclaration,
        FlowNode? antecedent)
    {
        Visit(localDeclaration.Values, localDeclaration, antecedent);

        for (var i = 0; i < localDeclaration.Declarations.Count; i++)
        {
            var declaration = localDeclaration.Declarations[i];

            AddSymbol(declaration.Name, new Symbol.LocalVariable(
                localDeclaration,
                i,
                IsVariableUninitialized(localDeclaration, i),
                CurrentScope.Chunk!));

            if (declaration.Type != null)
            {
                Visit(declaration.Type);
            }
        }

        return antecedent == null ? null : new FlowNode.VariableDeclaration(antecedent, localDeclaration);
    }

    private FlowNode.VariableDeclaration? VisitStatement(Tree.Statement.GlobalDeclaration globalDeclaration,
        FlowNode? antecedent)
    {
        Visit(globalDeclaration.Values, globalDeclaration, antecedent);

        // Symbols for global variables are added by the Project, so we don't need to here.
        foreach (var declaration in globalDeclaration.Declarations)
        {
            if (declaration.Type != null)
            {
                Visit(declaration.Type);
            }
        }

        return antecedent == null ? null : new FlowNode.VariableDeclaration(antecedent, globalDeclaration);
    }

    private ConditionBranch VisitIfBranch(Tree.IfBranch branch, FlowNode? antecedent)
    {
        var (conditionFalse, conditionTrue) = VisitExpressionFlowNodes(branch.Condition, antecedent);

        PushScope();
        var bodyDescendent = VisitBlock(branch.Body, conditionTrue);
        PopScope();

        return new(conditionFalse, bodyDescendent);
    }

    private FlowNode? VisitStatement(Tree.Statement.If ifStatement, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();

        var falseBranch = antecedent;

        foreach (var branch in ifStatement.ElseIfs.Prepend(ifStatement.Primary))
        {
            // Each `IfBranch`'s antecedent is the preceding `false` branch.
            (falseBranch, var trueBranch) = VisitIfBranch(branch, falseBranch);
            AddIfNotNull(descendents, trueBranch);
        }

        if (ifStatement.ElseBody != null)
        {
            PushScope();
            AddIfNotNull(descendents, VisitBlock(ifStatement.ElseBody, falseBranch));
            PopScope();
        }
        else
        {
            AddIfNotNull(descendents, falseBranch);
        }

        return FlowNode.FromAntecedents(descendents);
    }

    private FlowNode? VisitStatement(Tree.Statement.RepeatUntil repeatUntil, FlowNode? antecedent)
    {
        PushLoopScope(repeatUntil);
        var descendent = VisitBlock(repeatUntil.Body, antecedent);
        VisitExpression(repeatUntil.Condition, antecedent);
        PopScope();

        return descendent;
    }

    private FlowNode? VisitStatement(Tree.Statement.While whileLoop, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();
        AddIfNotNull(descendents, antecedent);

        VisitExpression(whileLoop.Condition, antecedent);
        PushLoopScope(whileLoop);
        AddIfNotNull(descendents, VisitBlock(whileLoop.Body, antecedent));
        PopScope();

        return FlowNode.FromAntecedents(descendents);
    }

    private FlowNode? VisitStatement(Tree.Statement.IteratorFor forLoop, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();
        AddIfNotNull(descendents, antecedent);

        VisitExpression(forLoop.Iterator, antecedent);

        PushLoopScope(forLoop);

        for (var i = 0; i < forLoop.Declarations.Count; i++)
        {
            var declaration = forLoop.Declarations[i];
            AddSymbol(declaration.Name, new Symbol.ForVariable(forLoop, i));
        }

        AddIfNotNull(descendents, VisitBlock(forLoop.Body, antecedent));

        PopScope();

        return FlowNode.FromAntecedents(descendents);
    }

    private FlowNode? VisitStatement(Tree.Statement.NumericalFor numericalFor, FlowNode? antecedent)
    {
        var descendents = new List<FlowNode>();
        AddIfNotNull(descendents, antecedent);

        PushLoopScope(numericalFor);
        VisitExpression(numericalFor.Start, antecedent);
        VisitExpression(numericalFor.Limit, antecedent);
        if (numericalFor.Step != null)
        {
            VisitExpression(numericalFor.Step, antecedent);
        }

        AddSymbol(numericalFor.Counter, new Symbol.NumericForCounter(numericalFor));
        AddIfNotNull(descendents, VisitBlock(numericalFor.Body, antecedent));
        PopScope();

        return FlowNode.FromAntecedents(descendents);
    }

    private void VisitStatement(Tree.Statement.Break brk)
    {
        if (CurrentScope.Loop == null)
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
            scope.Chunk == CurrentScope.Chunk)
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
            labelFlowNodes[label.Name] = new FlowNode.Label([]);
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
        var startNode = new FlowNode.Start();
        var descendent = VisitBlock(chunk, startNode);
        chunk.AllPathsReturn = descendent == null;
    }

    /// <summary>
    /// Visits all nodes in the given tree to assign Symbols to top-level Name nodes, and to generate the control flow
    /// graph.
    /// </summary>
    public static List<Diagnostic> Bind(Source source)
    {
        source.GlobalNames.Clear();

        var binder = new Binder(source);
        binder.VisitChunk(source.File);
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