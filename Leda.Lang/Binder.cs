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

        symbol.Definition = new(source, node.Range);
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
    private void VisitBlock(Tree.Block block)
    {
        // TODO type declarations need to be traversed in the order they appear in the block,
        // for `typeof` to work correctly.
        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            AddSymbol(typeDeclaration.Name, new Symbol.TypeAlias(typeDeclaration));
        }

        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            Visit(typeDeclaration.Type);
        }

        foreach (var statement in block.Statements)
        {
            Visit(statement);
        }
    }

    private void Visit(Tree.Statement stmt)
    {
        switch (stmt)
        {
            case Tree.Statement.Assignment assignment:
                Visit(assignment);
                break;
            case Tree.Statement.Call call:
                Visit(call.CallExpr);
                break;
            case Tree.Statement.Do @do:
                Visit(@do);
                break;
            case Tree.Statement.GlobalDeclaration globalDeclaration:
                Visit(globalDeclaration);
                break;
            case Tree.Statement.If @if:
                Visit(@if);
                break;
            case Tree.Statement.IteratorFor iteratorFor:
                Visit(iteratorFor);
                break;
            case Tree.Statement.LocalDeclaration localDeclaration:
                Visit(localDeclaration);
                break;
            case Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration:
                Visit(localFunctionDeclaration);
                break;
            case Tree.Statement.MethodCall methodCall:
                Visit(methodCall.CallExpr);
                break;
            case Tree.Statement.NumericalFor numericalFor:
                Visit(numericalFor);
                break;
            case Tree.Statement.RepeatUntil repeatUntil:
                Visit(repeatUntil);
                break;
            case Tree.Statement.Return @return:
                Visit(@return);
                break;
            case Tree.Statement.While @while:
                Visit(@while);
                break;
        }
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

    private void Visit(List<Tree.Type> types)
    {
        foreach (var type in types)
        {
            Visit(type);
        }
    }

    private void Visit(Tree.Statement.Do block)
    {
        PushScope();
        VisitBlock(block.Body);
        PopScope();
    }

    private void Visit(Tree.Statement.NumericalFor numericalFor)
    {
        PushScope();
        Visit(numericalFor.Start);
        Visit(numericalFor.Limit);
        if (numericalFor.Step != null)
        {
            Visit(numericalFor.Step);
        }

        AddSymbol(numericalFor.Counter, new Symbol.NumericForCounter());
        VisitBlock(numericalFor.Body);
        PopScope();
    }

    private void VisitIfBranch(Tree.IfBranch branch)
    {
        Visit(branch.Condition);
        PushScope();
        VisitBlock(branch.Body);
        PopScope();
    }

    private void Visit(Tree.Statement.If ifStatement)
    {
        VisitIfBranch(ifStatement.Primary);
        foreach (var branch in ifStatement.ElseIfs)
        {
            VisitIfBranch(branch);
        }

        if (ifStatement.ElseBody != null)
        {
            PushScope();
            VisitBlock(ifStatement.ElseBody);
            PopScope();
        }
    }

    private void Visit(Tree.Statement.Assignment assignment)
    {
        Visit(assignment.Targets);
        Visit(assignment.Values);
    }

    private void Visit(Tree.Expression.MethodCall methodCall)
    {
        Visit(methodCall.Target);
        Visit(methodCall.Parameters);
    }

    private void Visit(Tree.Expression.Call call)
    {
        Visit(call.Target);
        Visit(call.Parameters);
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

        VisitBlock(function.Body);
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
            Visit(field.Key);
            Visit(field.Value);
        }
    }

    private void Visit(Tree.Statement.Return returnStatement)
    {
        Visit(returnStatement.Values);
    }

    private void Visit(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        AddSymbol(declaration.Name, new Symbol.LocalFunction(declaration));
        PushScope();
        VisitFunction(declaration.Function);
        PopScope();
    }

    private void Visit(Tree.Statement.GlobalDeclaration declaration)
    {
        throw new NotImplementedException();
    }

    private void Visit(Tree.Statement.LocalDeclaration localDeclaration)
    {
        Visit(localDeclaration.Values);

        for (var i = 0; i < localDeclaration.Declarations.Count; i++)
        {
            var declaration = localDeclaration.Declarations[i];
            AddSymbol(declaration.Name, new Symbol.LocalVariable(localDeclaration, i));
            if (declaration.Type != null)
            {
                Visit(declaration.Type);
            }
        }
    }

    private void Visit(Tree.Statement.RepeatUntil repeatUntil)
    {
        PushScope();
        VisitBlock(repeatUntil.Body);
        Visit(repeatUntil.Condition);
        PopScope();
    }

    private void Visit(Tree.Statement.While whileLoop)
    {
        Visit(whileLoop.Condition);
        PushScope();
        VisitBlock(whileLoop.Body);
        PopScope();
    }

    private void Visit(Tree.Statement.IteratorFor forLoop)
    {
        Visit(forLoop.Iterator);

        PushScope();

        for (var i = 0; i < forLoop.Declarations.Count; i++)
        {
            var declaration = forLoop.Declarations[i];
            AddSymbol(declaration.Name, new Symbol.ForVariable(forLoop, i));
        }

        VisitBlock(forLoop.Body);

        PopScope();
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
        foreach (var pair in table.Pairs)
        {
            Visit(pair.Key);
            Visit(pair.Value);
        }
    }

    /// <summary>
    /// Visits all nodes in the given tree and updates the infoStore with the relevant information.
    /// </summary>
    public static List<Diagnostic> Bind(Source source, Tree.Block block)
    {
        var binder = new Binder(source);
        binder.VisitBlock(block);
        return binder.Diagnostics;
    }
}