using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

using Scope = Dictionary<string, Binder.Binding>;

/// <summary>
/// Visits each node of a Tree to create new Symbols for each declaration that's found, and associates names with
/// known Symbols.
/// </summary>
public class Binder
{
    private readonly Source source;
    public List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// A list of lexical scopes, where each scope is a dictionary of names with their symbols.
    /// </summary>
    private readonly List<Scope> scopes = [];

    /// <summary>
    /// Any name in the source code might refer to a value, or a type, or both.
    /// </summary>
    internal class Binding(Symbol? value, Symbol? type)
    {
        public Symbol? ValueSymbol { get; set; } = value;
        public Symbol? TypeSymbol { get; set; } = type;
    }

    private Scope CurrentScope => scopes[^1];

    private static readonly Scope InitialScope = new()
    {
        [Type.Any.Name!] = new(null, Symbol.AnyType),
        [Type.Boolean.Name!] = new(null, Symbol.BooleanType),
        [Type.NumberPrimitive.Name!] = new(null, Symbol.NumberType),
        [Type.StringPrimitive.Name!] = new(null, Symbol.StringType), // TODO stringlib should be a value here
        [Type.FunctionPrimitive.Name!] = new(null, Symbol.FunctionType)
    };

    private readonly Stack<AssignmentPath> assignmentPathStack = [];

    private record FunctionInfo(Stack<Tree.Statement> LoopStack);

    private readonly Stack<FunctionInfo> functionStack = [];

    private Binder(Source source)
    {
        this.source = source;

        scopes.Add(InitialScope);
        scopes.Add(new Scope());
    }

    private void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    private void PushScope()
    {
        scopes.Add(new Scope());
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
    /// <param name="symbol">Out variable to store the symbol at.</param>
    /// <param name="scope">Out variable to store the scope where the symbol was found.</param>
    /// <returns>True if a symbol with this name was found, false otherwise.</returns>
    private bool TryGetBinding(string name, Tree.NameContext context, [NotNullWhen(true)] out Symbol? symbol,
        [NotNullWhen(true)] out Scope? scope)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            if (scopes[i].TryGetValue(name, out var binding))
            {
                symbol = context == Tree.NameContext.Value ? binding.ValueSymbol : binding.TypeSymbol;
                if (symbol != null)
                {
                    scope = scopes[i];
                    return true;
                }
            }
        }

        scope = null;
        symbol = null;
        return false;
    }

    /// <summary>
    /// Finds the value symbol that a name refers to.
    /// </summary>
    private bool TryGetBinding(Tree.Expression.Name name, [NotNullWhen(true)] out Symbol? symbol)
    {
        return TryGetBinding(name.Value, Tree.NameContext.Value, out symbol, out _);
    }

    /// <summary>
    /// Finds the type symbol that a type name refers to.
    /// </summary>
    private bool TryGetBinding(Tree.Type.Name name, [NotNullWhen(true)] out Symbol? symbol)
    {
        return TryGetBinding(name.Value, Tree.NameContext.Type, out symbol, out _);
    }

    /// <summary>
    /// Adds a named symbol to the current scope. Reports a diagnostic if a symbol with this name has already been
    /// declared in the same scope.
    /// </summary>
    private void AddSymbol(Tree node, string name, Tree.NameContext context, Symbol symbol)
    {
        // TODO report warning if a name is shadowed
        if (TryGetBinding(name, context, out _, out var existingScope) &&
            existingScope == CurrentScope)
        {
            if (context == Tree.NameContext.Value)
            {
                Report(new Diagnostic.ValueAlreadyDeclared(node.Range, name));
            }
            else
            {
                Report(new Diagnostic.TypeAlreadyDeclared(node.Range, name));
            }
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
        else
        {
            currentBinding.TypeSymbol = symbol;
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

    /// <summary>
    /// Visits all of a block's statements.
    /// </summary>
    private FlowNode? VisitBlock(Tree.Block block, FlowNode antecedent)
    {
        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            AddSymbol(typeDeclaration.Name, new Symbol.TypeAlias(typeDeclaration));
        }

        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            Visit(typeDeclaration.Type);
        }

        var stopped = false;

        foreach (var statement in block.Statements)
        {
            var descendant = VisitStatement(statement, antecedent);
            if (descendant == null)
            {
                // TODO report unreachable code
                stopped = true;
            }
            else
            {
                antecedent = descendant;
            }
        }

        return stopped ? null : antecedent;
    }

    private void VisitChunk(Tree.Chunk chunk)
    {
        var startNode = new FlowNode([]);

        functionStack.Push(new([]));
        var descendent = VisitBlock(chunk, startNode);
        functionStack.Pop();

        chunk.AllPathsReturn = descendent == null;
    }

    private FlowNode? VisitStatement(Tree.Statement stmt, FlowNode antecedent)
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
        }

        return antecedent;
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

    private void Visit(List<Tree.Type> types)
    {
        foreach (var type in types)
        {
            Visit(type);
        }
    }

    private FlowNode? VisitStatement(Tree.Statement.Do block, FlowNode antecedent)
    {
        PushScope();
        var descendent = VisitBlock(block.Body, antecedent);
        PopScope();

        return descendent;
    }

    private FlowNode? VisitIfBranch(Tree.IfBranch branch, FlowNode antecedent)
    {
        Visit(branch.Condition);
        PushScope();
        var descendent = VisitBlock(branch.Body, antecedent);
        PopScope();

        return descendent;
    }

    private FlowNode? VisitStatement(Tree.Statement.If ifStatement, FlowNode antecedent)
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
            descendents.Add(antecedent);
        }

        return descendents.Count > 0 ? new(descendents) : null;
    }

    private FlowNode VisitStatement(Tree.Statement.Assignment assignment, FlowNode antecedent)
    {
        Visit(assignment.Targets);
        Visit(assignment.Values, assignment);
        return antecedent;
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

        PushScope();
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
        if (TryGetBinding(name, out var symbol))
        {
            source.AttachSymbol(name, symbol);
        }
        else
        {
            // TODO defer to check for global
            Report(new Diagnostic.NameNotFound(name.Range, name.Value, Tree.NameContext.Value));
        }
    }

    private void Visit(Tree.Type.Name name)
    {
        if (TryGetBinding(name, out var symbol))
        {
            source.AttachSymbol(name, symbol);
        }
        else
        {
            // TODO defer to check for global
            Report(new Diagnostic.NameNotFound(name.Range, name.Value, Tree.NameContext.Type));
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

    private void VisitStatement(Tree.Statement.Return returnStatement)
    {
        Visit(returnStatement.Values, returnStatement);
    }

    private void VisitStatement(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        AddSymbol(declaration.Name, new Symbol.LocalFunction(declaration));
        PushScope();
        VisitFunction(declaration.Function);
        PopScope();
    }

    private FlowNode VisitStatement(Tree.Statement.GlobalDeclaration declaration, FlowNode antecedent)
    {
        throw new NotImplementedException();
    }

    private FlowNode VisitStatement(Tree.Statement.LocalDeclaration localDeclaration, FlowNode antecedent)
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

    private FlowNode? VisitStatement(Tree.Statement.RepeatUntil repeatUntil, FlowNode antecedent)
    {
        functionStack.Peek().LoopStack.Push(repeatUntil);
        PushScope();
        var descendent = VisitBlock(repeatUntil.Body, antecedent);
        Visit(repeatUntil.Condition);
        PopScope();
        functionStack.Peek().LoopStack.Pop();

        return descendent;
    }

    private FlowNode VisitStatement(Tree.Statement.While whileLoop, FlowNode antecedent)
    {
        var descendents = new List<FlowNode> { antecedent };

        Visit(whileLoop.Condition);
        functionStack.Peek().LoopStack.Push(whileLoop);
        PushScope();
        AddIfNotNull(descendents, VisitBlock(whileLoop.Body, antecedent));
        PopScope();
        functionStack.Peek().LoopStack.Pop();

        return new FlowNode(descendents);
    }

    private FlowNode VisitStatement(Tree.Statement.IteratorFor forLoop, FlowNode antecedent)
    {
        var descendents = new List<FlowNode> { antecedent };

        Visit(forLoop.Iterator);

        functionStack.Peek().LoopStack.Push(forLoop);
        PushScope();

        for (var i = 0; i < forLoop.Declarations.Count; i++)
        {
            var declaration = forLoop.Declarations[i];
            AddSymbol(declaration.Name, new Symbol.ForVariable(forLoop, i));
        }

        AddIfNotNull(descendents, VisitBlock(forLoop.Body, antecedent));

        PopScope();
        functionStack.Peek().LoopStack.Pop();

        return new FlowNode(descendents);
    }

    private FlowNode VisitStatement(Tree.Statement.NumericalFor numericalFor, FlowNode antecedent)
    {
        var descendents = new List<FlowNode> { antecedent };

        functionStack.Peek().LoopStack.Push(numericalFor);
        PushScope();
        Visit(numericalFor.Start);
        Visit(numericalFor.Limit);
        if (numericalFor.Step != null)
        {
            Visit(numericalFor.Step);
        }

        AddSymbol(numericalFor.Counter, new Symbol.NumericForCounter());
        AddIfNotNull(descendents, VisitBlock(numericalFor.Body, antecedent));
        PopScope();
        functionStack.Peek().LoopStack.Pop();

        return new FlowNode(descendents);
    }

    private void VisitTypeParameterDeclaration(List<Tree.Type.Name> typeParameters)
    {
        foreach (var typeParameter in typeParameters)
        {
            AddSymbol(typeParameter, new Symbol.TypeParameter());
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

    private void VisitStatement(Tree.Statement.Break brk)
    {
        if (functionStack.Peek().LoopStack.Count == 0)
        {
            Report(new Diagnostic.BreakOutsideOfLoop(brk.Range));
        }
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