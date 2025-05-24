namespace Leda.Lang;

public class Checker : Tree.IVisitor, Tree.IExpressionVisitor<Type>, Tree.ITypeVisitor<Type>
{
    private readonly Source source;
    private readonly IDiagnosticReporter reporter;

    /// <summary>
    /// Maps Symbols to their types.
    /// </summary>
    private readonly Dictionary<Symbol, Type> typeMap = [];

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
        VisitBlock(block.Body);
    }

    public void Visit(Tree.NumericalFor numericalFor)
    {
        var startType = numericalFor.Start.AcceptExpressionVisitor(this);
        if (!Type.Number.IsAssignableFrom(startType))
        {
            reporter.Report(new Diagnostic.ForLoopStartNotNumber(source, numericalFor.Start.Range, startType));
        }

        var limitType = numericalFor.Limit.AcceptExpressionVisitor(this);
        if (!Type.Number.IsAssignableFrom(limitType))
        {
            reporter.Report(new Diagnostic.ForLoopLimitNotNumber(source, numericalFor.Limit.Range, limitType));
        }

        if (numericalFor.Step != null)
        {
            var stepType = numericalFor.Step.AcceptExpressionVisitor(this);
            if (!Type.Number.IsAssignableFrom(stepType))
            {
                reporter.Report(new Diagnostic.ForLoopStepNotNumber(source, numericalFor.Step.Range, stepType));
            }
        }

        if (!source.TryGetValueSymbol(numericalFor.Counter, out var counterSymbol))
        {
            throw new Exception("No symbol for `for` loop counter");
        }

        typeMap.Add(counterSymbol, Type.Number);

        VisitBlock(numericalFor.Body);
    }

    public void Visit(Tree.If ifStatement)
    {
        ifStatement.Primary.Condition.AcceptExpressionVisitor(this);
        VisitBlock(ifStatement.Primary.Body);

        foreach (var branch in ifStatement.ElseIfs)
        {
            branch.Condition.AcceptExpressionVisitor(this);
            VisitBlock(branch.Body);
        }

        if (ifStatement.ElseBody != null)
        {
            VisitBlock(ifStatement.ElseBody);
        }
    }

    public void Visit(Tree.Assignment assignment)
    {
        for (var i = 0; i < assignment.Targets.Count; i++)
        {
            var target = assignment.Targets[i];
            var targetType = target.AcceptExpressionVisitor(this);

            var valueType = Type.Nil;
            if (i < assignment.Values.Count)
            {
                valueType = assignment.Values[i].AcceptExpressionVisitor(this);
            }
            else
            {
                // TODO report error/warning
            }

            if (!targetType.IsAssignableFrom(valueType))
            {
                reporter.Report(new Diagnostic.TypeNotAssignableToType(source, target.Range, targetType, valueType));
            }
        }
    }

    public void Visit(Tree.MethodCall methodCall)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Call call)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Access access)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Binary binary)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Unary unary)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Function function)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Name name)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Return returnStatement)
    {
        throw new NotImplementedException();
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
        for (var i = 0; i < localDeclaration.Declarations.Count; i++)
        {
            var declaration = localDeclaration.Declarations[i];

            var valueType = Type.Nil;
            if (i < localDeclaration.Values.Count)
            {
                valueType = localDeclaration.Values[i].AcceptExpressionVisitor(this);
            }
            else
            {
                // TODO report error/warning
            }

            Type variableType;

            if (declaration.Type != null)
            {
                var declarationType = declaration.Type.AcceptTypeVisitor(this);
                if (!declarationType.IsAssignableFrom(valueType))
                {
                    reporter.Report(new Diagnostic.TypeNotAssignableToType(source, declaration.Name.Range,
                        declarationType, valueType));
                }

                variableType = declarationType;
            }
            else
            {
                // The variable's type is inferred from the value.
                variableType = valueType;
            }

            if (!source.TryGetValueSymbol(declaration.Name, out var symbol))
            {
                throw new Exception("Variable doesn't have a symbol");
            }

            typeMap.Add(symbol, variableType);
        }
    }

    public void Visit(Tree.RepeatUntil repeatUntil)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.While whileLoop)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.IteratorFor forLoop)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Function function)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.MethodCall methodCall)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Call call)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Access access)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Binary binary)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Unary unary)
    {
        var exprType = unary.Expression.AcceptExpressionVisitor(this);

        if (unary is Tree.Not)
        {
            return Type.Boolean;
        }

        if (unary is Tree.Length)
        {
            // TODO use __len metamethod
            if (!Type.Table.IsAssignableFrom(exprType) && !Type.String.IsAssignableFrom(exprType))
            {
                reporter.Report(new Diagnostic.CantGetLength(source, unary.Range, exprType));
            }

            return Type.Number;
        }

        if (unary is Tree.Negate)
        {
            // TODO use __unm metamethod
            if (!Type.Number.IsAssignableFrom(exprType))
            {
                reporter.Report(new Diagnostic.CantNegate(source, unary.Range, exprType));
            }

            return Type.Number;
        }

        throw new Exception(); // Unreachable.
    }

    public Type VisitExpression(Tree.Name name)
    {
        if (!source.TryGetValueSymbol(name, out var symbol))
        {
            // Unresolved names should be reported by the Binder.
            return Type.Unknown;
        }

        if (!typeMap.TryGetValue(symbol, out var type))
        {
            // TODO detection of types should be deferred if needed.
            throw new Exception();
        }

        return type;
    }

    public Type VisitExpression(Tree.Table table)
    {
        // TODO iterate over fields
        return Type.Table;
    }

    public Type VisitExpression(Tree.Number number)
    {
        return Type.Number;
    }

    public Type VisitExpression(Tree.String stringValue)
    {
        return Type.String;
    }

    public Type VisitExpression(Tree.True trueValue)
    {
        return Type.True;
    }

    public Type VisitExpression(Tree.False falseValue)
    {
        return Type.False;
    }

    public Type VisitExpression(Tree.Nil nil)
    {
        return Type.Nil;
    }

    public Type VisitExpression(Tree.Error error)
    {
        return Type.Unknown;
    }

    private Checker(Source source, IDiagnosticReporter reporter)
    {
        this.source = source;
        this.reporter = reporter;
    }

    public static void Check(Source source, IDiagnosticReporter reporter)
    {
        new Checker(source, reporter).VisitBlock(source.Tree);
    }

    public Type VisitType(Tree.Name name)
    {
        if (source.TryGetTypeSymbol(name, out var symbol))
        {
            return symbol.Type;
        }

        // TODO report error?
        return Type.Unknown;
    }
}