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
    private readonly IDiagnosticReporter reporter;

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

    private Binder(Source source, IDiagnosticReporter reporter)
    {
        this.source = source;
        this.reporter = reporter;

        // Add the standard types.
        // TODO maybe these should originate from a declaration file instead
        scopes.Add(new()
        {
            [Type.Boolean.Name] = new(null, new Symbol.TypeSymbol(Type.Boolean)),
            [Type.Number.Name] = new(null, new Symbol.TypeSymbol(Type.Number)),
            [Type.String.Name] = new(null, new Symbol.TypeSymbol(Type.String)) // TODO stringlib should be a value here
        });
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
    /// Attempts to find a binding by its name.
    /// </summary>
    /// <param name="name">The name of the symbol to look for.</param>
    /// <param name="symbol">Out variable to store the symbol at.</param>
    /// <param name="scope">Out variable to store the scope where the symbol was found.</param>
    /// <returns>True if a symbol with this name was found, false otherwise.</returns>
    private bool TryGetBinding(string name, [NotNullWhen(true)] out Binding? symbol,
        [NotNullWhen(true)] out Scope? scope)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            if (scopes[i].TryGetValue(name, out symbol))
            {
                scope = scopes[i];
                return true;
            }
        }

        scope = null;
        symbol = null;
        return false;
    }

    private bool TryGetBinding(string name, [NotNullWhen(true)] out Binding? symbol)
    {
        return TryGetBinding(name, out symbol, out _);
    }

    /// <summary>
    /// Adds a named symbol to the current scope. Reports a diagnostic if a symbol with this name has already been
    /// declared in the same scope.
    /// </summary>
    private void AddSymbol(Tree.Name name, Symbol symbol)
    {
        // TODO report warning if a name is shadowed
        if (TryGetBinding(name.Value, out var existingBinding, out var existingScope) && existingScope == CurrentScope)
        {
            if (symbol is Symbol.TypeSymbol && existingBinding.TypeSymbol != null)
            {
                reporter.Report(new Diagnostic.TypeAlreadyDeclared(source, name.Range, name.Value));
            }
            else if (symbol is not Symbol.TypeSymbol && existingBinding.ValueSymbol != null)
            {
                reporter.Report(new Diagnostic.ValueAlreadyDeclared(source, name.Range, name.Value,
                    existingBinding.ValueSymbol));
            }
        }

        if (!CurrentScope.TryGetValue(name.Value, out var currentBinding))
        {
            currentBinding = new Binding(null, null);
            CurrentScope[name.Value] = currentBinding;
        }

        if (symbol is Symbol.TypeSymbol typeSymbol)
        {
            currentBinding.TypeSymbol = typeSymbol;
        }
        else
        {
            currentBinding.ValueSymbol = symbol;
        }

        symbol.Definition = new(source, name.Range);
        source.AttachValueSymbol(name, symbol);
    }

    /// <summary>
    /// Visits all of a block's statements.
    /// </summary>
    public void VisitBlock(Tree.Block block)
    {
        // TODO iterate over block's type declarations
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
        foreach (var parameter in function.Parameters)
        {
            AddSymbol(parameter.Name, new Symbol.Parameter());
        }

        VisitBlock(function.Body);
    }

    public void Visit(Tree.Name name)
    {
        if (TryGetBinding(name.Value, out var binding))
        {
            if (binding.ValueSymbol != null)
            {
                source.AttachValueSymbol(name, binding.ValueSymbol);
            }

            if (binding.TypeSymbol != null)
            {
                source.AttachTypeSymbol(name, binding.TypeSymbol);
            }
        }
        else
        {
            // TODO defer to check for global
            reporter.Report(new Diagnostic.NameNotFound(source, name));
        }
    }

    public void Visit(Tree.Return returnStatement)
    {
        returnStatement.Expression?.AcceptVisitor(this);
    }

    public void Visit(Tree.LocalFunctionDeclaration declaration)
    {
        throw new NotImplementedException();
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

    /// <summary>
    /// Visits all nodes in the given tree and updates the infoStore with the relevant information.
    /// </summary>
    public static void Bind(Source source, Tree.Block block, IDiagnosticReporter reporter)
    {
        var binder = new Binder(source, reporter);
        binder.VisitBlock(block);
    }
}