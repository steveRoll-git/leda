using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

using Scope = Dictionary<string, Binder.Binding>;

/// <summary>
/// Visits each node of a Tree to create new Symbols for each declaration that's found, and associates names with
/// known Symbols.
/// </summary>
public class Binder : Tree.IVisitor
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
    internal class Binding(Symbol? value, Symbol.TypeSymbol? type)
    {
        public Symbol? ValueSymbol { get; set; } = value;
        public Symbol.TypeSymbol? TypeSymbol { get; set; } = type;
    }

    private Scope CurrentScope => scopes[^1];

    private Binder(Source source)
    {
        this.source = source;

        // Add the standard types.
        // TODO maybe these should originate from a declaration file instead
        scopes.Add(new()
        {
            [Type.Any.Name!] = new(null, new Symbol.TypeSymbol(Type.Any)),
            [Type.Boolean.Name!] = new(null, new Symbol.TypeSymbol(Type.Boolean)),
            [Type.NumberPrimitive.Name!] = new(null, new Symbol.TypeSymbol(Type.NumberPrimitive)),
            [Type.StringPrimitive.Name!] =
                new(null, new Symbol.TypeSymbol(Type.StringPrimitive)), // TODO stringlib should be a value here
            [Type.FunctionPrimitive.Name!] = new(null, new Symbol.TypeSymbol(Type.FunctionPrimitive))
        });
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
    private bool TryGetBinding(Tree.Name name, [NotNullWhen(true)] out Symbol? symbol)
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
        if (TryGetBinding(name, context, out var existingSymbol, out var existingScope) &&
            existingScope == CurrentScope)
        {
            if (context == Tree.NameContext.Value)
            {
                Report(new Diagnostic.ValueAlreadyDeclared(node.Range, name, existingSymbol));
            }
            else if (context == Tree.NameContext.Type)
            {
                Report(new Diagnostic.TypeAlreadyDeclared(node.Range, name));
            }
        }

        if (!CurrentScope.TryGetValue(name, out var currentBinding))
        {
            currentBinding = new Binding(null, null);
            CurrentScope[name] = currentBinding;
        }

        if (symbol is Symbol.TypeSymbol typeSymbol)
        {
            currentBinding.TypeSymbol = typeSymbol;
        }
        else
        {
            currentBinding.ValueSymbol = symbol;
        }

        symbol.Definition = new(source, node.Range);
        source.AttachSymbol(node, symbol, true);
    }

    /// <summary>
    /// Adds a value name's symbol to the current scope.
    /// </summary>
    private void AddSymbol(Tree.Name name, Symbol symbol)
    {
        AddSymbol(name, name.Value, Tree.NameContext.Value, symbol);
    }

    /// <summary>
    /// Adds a type name's symbol to the current scope.
    /// </summary>
    private void AddTypeSymbol(Tree.Type.Name name, Symbol symbol)
    {
        AddSymbol(name, name.Value, Tree.NameContext.Type, symbol);
    }

    /// <summary>
    /// Visits all of a block's statements.
    /// </summary>
    public void VisitBlock(Tree.Block block)
    {
        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            AddTypeSymbol(typeDeclaration.Name, new Symbol.TypeSymbol());
            typeDeclaration.Type.AcceptVisitor(this);
        }

        foreach (var statement in block.Statements)
        {
            statement.AcceptVisitor(this);
        }
    }

    public void Visit(Tree.Do block)
    {
        PushScope();
        VisitBlock(block.Body);
        PopScope();
    }

    public void Visit(Tree.NumericalFor numericalFor)
    {
        PushScope();
        numericalFor.Start.AcceptVisitor(this);
        numericalFor.Limit.AcceptVisitor(this);
        numericalFor.Step?.AcceptVisitor(this);
        AddSymbol(numericalFor.Counter, new Symbol.LocalVariable());
        VisitBlock(numericalFor.Body);
        PopScope();
    }

    private void VisitIfBranch(Tree.IfBranch branch)
    {
        branch.Condition.AcceptVisitor(this);
        PushScope();
        VisitBlock(branch.Body);
        PopScope();
    }

    public void Visit(Tree.If ifStatement)
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

    public void Visit(Tree.Assignment assignment)
    {
        foreach (var target in assignment.Targets)
        {
            target.AcceptVisitor(this);
        }

        foreach (var value in assignment.Values)
        {
            value.AcceptVisitor(this);
        }
    }

    public void Visit(Tree.MethodCall methodCall)
    {
        methodCall.Target.AcceptVisitor(this);
        foreach (var parameter in methodCall.Parameters)
        {
            parameter.AcceptVisitor(this);
        }
    }

    public void Visit(Tree.Call call)
    {
        call.Target.AcceptVisitor(this);
        foreach (var parameter in call.Parameters)
        {
            parameter.AcceptVisitor(this);
        }
    }

    public void Visit(Tree.Access access)
    {
        access.Target.AcceptVisitor(this);
        access.Key.AcceptVisitor(this);
    }

    public void Visit(Tree.Binary binary)
    {
        binary.Left.AcceptVisitor(this);
        binary.Right.AcceptVisitor(this);
    }

    public void Visit(Tree.Unary unary)
    {
        unary.Expression.AcceptVisitor(this);
    }

    public void Visit(Tree.Function function)
    {
        PushScope();
        VisitFunction(function);
        PopScope();
    }

    public void VisitFunction(Tree.Function function)
    {
        Visit(function.Type);

        foreach (var parameter in function.Type.Parameters)
        {
            AddSymbol(parameter.Name, new Symbol.Parameter());
        }

        VisitBlock(function.Body);
    }

    public void Visit(Tree.Name name)
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

    public void Visit(Tree.Type.Name name)
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

    public void Visit(Tree.Table table)
    {
        foreach (var field in table.Fields)
        {
            field.Key.AcceptVisitor(this);
            field.Value.AcceptVisitor(this);
        }
    }

    public void Visit(Tree.Return returnStatement)
    {
        returnStatement.Expression?.AcceptVisitor(this);
    }

    public void Visit(Tree.LocalFunctionDeclaration declaration)
    {
        AddSymbol(declaration.Name, new Symbol.LocalVariable());
        PushScope();
        VisitFunction(declaration.Function);
        PopScope();
    }

    public void Visit(Tree.GlobalDeclaration declaration)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.LocalDeclaration localDeclaration)
    {
        foreach (var value in localDeclaration.Values)
        {
            value.AcceptVisitor(this);
        }

        foreach (var declaration in localDeclaration.Declarations)
        {
            // TODO should the declaration node be the definition?
            AddSymbol(declaration.Name, new Symbol.LocalVariable());
            declaration.Type?.AcceptVisitor(this);
        }
    }

    public void Visit(Tree.RepeatUntil repeatUntil)
    {
        PushScope();
        VisitBlock(repeatUntil.Body);
        repeatUntil.Condition.AcceptVisitor(this);
        PopScope();
    }

    public void Visit(Tree.While whileLoop)
    {
        whileLoop.Condition.AcceptVisitor(this);
        PushScope();
        VisitBlock(whileLoop.Body);
        PopScope();
    }

    public void Visit(Tree.IteratorFor forLoop)
    {
        forLoop.Iterator.AcceptVisitor(this);

        PushScope();

        foreach (var declaration in forLoop.Declarations)
        {
            AddSymbol(declaration.Name, new Symbol.LocalVariable());
        }

        VisitBlock(forLoop.Body);

        PopScope();
    }

    public void Visit(Tree.Type.Function functionType)
    {
        foreach (var parameter in functionType.Parameters)
        {
            parameter.Type?.AcceptVisitor(this);
        }

        if (functionType.ReturnTypes != null)
        {
            foreach (var returnType in functionType.ReturnTypes)
            {
                returnType.AcceptVisitor(this);
            }
        }
    }

    public void Visit(Tree.Type.Table table)
    {
        foreach (var pair in table.Pairs)
        {
            pair.Key.AcceptVisitor(this);
            pair.Value.AcceptVisitor(this);
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