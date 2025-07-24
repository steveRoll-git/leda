namespace Leda.Lang;

public class Checker : Tree.IVisitor, Tree.IExpressionVisitor<Type>, Tree.ITypeVisitor<Type>
{
    private readonly Source source;
    public List<Diagnostic> Diagnostics { get; } = [];

    private void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Visits all of a block's statements.
    /// </summary>
    private void VisitBlock(Tree.Block block)
    {
        // TODO iterate over block's type declarations
        foreach (var statement in block.Statements)
        {
            statement.AcceptVisitor(this);
        }
    }

    private TypeList VisitCall(Tree.Call call)
    {
        var parameters = VisitExpressionList(call.Parameters);
        var target = call.Target.AcceptExpressionVisitor(this, false);

        if (!Type.FunctionPrimitive.IsAssignableFrom(target)) // TODO handle __call metamethod
        {
            Report(new Diagnostic.TypeNotCallable(call.Target.Range));
            return TypeList.None;
        }

        if (target == Type.FunctionPrimitive)
        {
            return TypeList.Any;
        }

        if (target is Type.Function function)
        {
            // TODO support overloads
            if (!function.Parameters.IsAssignableFrom(parameters, out var reasons, TypeList.TypeListKind.Parameter))
            {
                foreach (var reason in reasons)
                {
                    if (reason is TypeMismatch.ValueInListIncompatible incompatible)
                    {
                        var faultyParam = call.Parameters[Math.Min(call.Parameters.Count - 1, incompatible.Index)];
                        Report(new Diagnostic.TypeMismatch(faultyParam.Range, reason));
                    }
                    else if (reason is TypeMismatch.NotEnoughValues)
                    {
                        Report(new Diagnostic.TypeMismatch(call.Target.Range, reason));
                    }
                }
            }

            return function.Return;
        }

        throw new NotImplementedException("Types other than `Function` aren't callable yet");
    }

    private TypeList VisitExpressionList(List<Tree> expressions)
    {
        List<Type> list = new(1);
        TypeList? continued = null;
        foreach (var expression in expressions)
        {
            if (continued != null)
            {
                // If more items are present after the last continued list, only its first value is added.
                // TODO show warning about discarded values?
                list.Add(continued.Types().First().Type);
            }

            if (expression is Tree.Call call)
            {
                continued = VisitCall(call);
            }
            else if (expression is Tree.Vararg)
            {
                throw new NotImplementedException();
            }
            else
            {
                var type = expression.AcceptExpressionVisitor(this, false);
                list.Add(type);
            }
        }

        if (list.Count == 0)
        {
            return continued ?? TypeList.None;
        }

        return new TypeList(list, continued);
    }

    private Type VisitFunctionType(Tree.FunctionType functionType, bool addSymbols = false)
    {
        List<Type> parameters = [];
        List<string> paramNames = [];
        foreach (var parameter in functionType.Parameters)
        {
            if (parameter.Type != null)
            {
                var type = parameter.Type.AcceptTypeVisitor(this);
                parameters.Add(type);

                if (addSymbols)
                {
                    if (!source.TryGetTreeSymbol(parameter.Name, out var symbol))
                    {
                        throw new Exception("Parameter doesn't have symbol");
                    }

                    source.SetSymbolType(symbol, type);
                }
            }
            else
            {
                // TODO infer parameter types from target
                parameters.Add(Type.Any);
            }

            paramNames.Add(parameter.Name.Value);
        }

        var parameterTypeList = new TypeList(parameters) { NameList = paramNames };
        // TODO handle rest parameter

        var returnTypeList = GetFunctionReturnType(functionType);
        returnTypeList ??= TypeList.None; // TODO infer return type

        return new Type.Function { Parameters = parameterTypeList, Return = returnTypeList };
    }

    private TypeList? GetFunctionReturnType(Tree.FunctionType functionType)
    {
        if (functionType.ReturnTypes != null)
        {
            List<Type> returnTypes = [];
            foreach (var returnType in functionType.ReturnTypes)
            {
                returnTypes.Add(returnType.AcceptTypeVisitor(this));
            }

            // TODO handle Rest and Continued
            return new TypeList(returnTypes);
        }

        return null;
    }

    public void Visit(Tree.Do block)
    {
        VisitBlock(block.Body);
    }

    public void Visit(Tree.NumericalFor numericalFor)
    {
        var startType = numericalFor.Start.AcceptExpressionVisitor(this, false);
        if (!Type.Number.IsAssignableFrom(startType))
        {
            Report(new Diagnostic.ForLoopStartNotNumber(numericalFor.Start.Range, startType));
        }

        var limitType = numericalFor.Limit.AcceptExpressionVisitor(this, false);
        if (!Type.Number.IsAssignableFrom(limitType))
        {
            Report(new Diagnostic.ForLoopLimitNotNumber(numericalFor.Limit.Range, limitType));
        }

        if (numericalFor.Step != null)
        {
            var stepType = numericalFor.Step.AcceptExpressionVisitor(this, false);
            if (!Type.Number.IsAssignableFrom(stepType))
            {
                Report(new Diagnostic.ForLoopStepNotNumber(numericalFor.Step.Range, stepType));
            }
        }

        if (!source.TryGetTreeSymbol(numericalFor.Counter, out var counterSymbol))
        {
            throw new Exception("No symbol for `for` loop counter");
        }

        source.SetSymbolType(counterSymbol, Type.Number);

        VisitBlock(numericalFor.Body);
    }

    public void Visit(Tree.If ifStatement)
    {
        ifStatement.Primary.Condition.AcceptExpressionVisitor(this, false);
        VisitBlock(ifStatement.Primary.Body);

        foreach (var branch in ifStatement.ElseIfs)
        {
            branch.Condition.AcceptExpressionVisitor(this, false);
            VisitBlock(branch.Body);
        }

        if (ifStatement.ElseBody != null)
        {
            VisitBlock(ifStatement.ElseBody);
        }
    }

    public void Visit(Tree.Assignment assignment)
    {
        using var valueTypes = VisitExpressionList(assignment.Values).Types().GetEnumerator();
        foreach (var target in assignment.Targets)
        {
            var targetType = target.AcceptExpressionVisitor(this, false);

            var valueType = Type.Nil;
            if (valueTypes.MoveNext())
            {
                valueType = valueTypes.Current.Type; // TODO handle `Rest` values
            }
            else
            {
                // TODO report error/warning
            }

            if (!targetType.IsAssignableFrom(valueType, out var reason))
            {
                Report(
                    new Diagnostic.TypeMismatch(target.Range, reason));
            }
        }
    }

    public void Visit(Tree.MethodCall methodCall)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Call call)
    {
        VisitCall(call);
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
        var functionType = VisitFunctionType(declaration.Function.Type, true);
        if (!source.TryGetTreeSymbol(declaration.Name, out var symbol))
        {
            throw new Exception();
        }

        source.SetSymbolType(symbol, functionType);
        VisitBlock(declaration.Function.Body);
    }

    public void Visit(Tree.GlobalDeclaration declaration)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.LocalDeclaration localDeclaration)
    {
        using var valueTypes = VisitExpressionList(localDeclaration.Values).Types().GetEnumerator();
        for (var i = 0; i < localDeclaration.Declarations.Count; i++)
        {
            var declaration = localDeclaration.Declarations[i];

            var valueType = Type.Nil;
            if (valueTypes.MoveNext())
            {
                valueType = valueTypes.Current.Type; // TODO handle `Rest` values
            }
            else
            {
                // TODO report error/warning
            }

            Type variableType;

            if (declaration.Type != null)
            {
                var declarationType = declaration.Type.AcceptTypeVisitor(this);
                if (!declarationType.IsAssignableFrom(valueType, out var reason))
                {
                    Report(new Diagnostic.TypeMismatch(declaration.Name.Range, reason));
                }

                variableType = declarationType;
            }
            else
            {
                // The variable's type is inferred from the value.
                variableType = valueType;
            }

            if (!source.TryGetTreeSymbol(declaration.Name, out var symbol))
            {
                throw new Exception("Variable doesn't have a symbol");
            }

            source.SetSymbolType(symbol, variableType);
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

    public void Visit(Tree.FunctionType functionType)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Function function, bool isConstant)
    {
        var functionType = VisitFunctionType(function.Type, true);
        VisitBlock(function.Body);
        return functionType;
    }

    public Type VisitExpression(Tree.MethodCall methodCall, bool isConstant)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Call call, bool isConstant)
    {
        // TODO show warning if the call returns more than one value?
        return VisitCall(call).Types().FirstOrDefault((Type: Type.Nil, false)).Type;
    }

    public Type VisitExpression(Tree.Access access, bool isConstant)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Binary binary, bool isConstant)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Unary unary, bool isConstant)
    {
        var exprType = unary.Expression.AcceptExpressionVisitor(this, isConstant);

        if (unary is Tree.Not)
        {
            return Type.Boolean;
        }

        if (unary is Tree.Length)
        {
            // TODO use __len metamethod
            if (!Type.Table.IsAssignableFrom(exprType) && !Type.StringPrimitive.IsAssignableFrom(exprType))
            {
                Report(new Diagnostic.CantGetLength(unary.Range, exprType));
            }

            return Type.Number;
        }

        if (unary is Tree.Negate)
        {
            // TODO use __unm metamethod
            if (!Type.Number.IsAssignableFrom(exprType))
            {
                Report(new Diagnostic.CantNegate(unary.Range, exprType));
            }

            return Type.Number;
        }

        throw new Exception(); // Unreachable.
    }

    public Type VisitExpression(Tree.Name name, bool isConstant)
    {
        if (!source.TryGetTreeSymbol(name, out var symbol))
        {
            // Unresolved names should be reported by the Binder.
            return Type.Unknown;
        }

        if (!source.TryGetSymbolType(symbol, out var type))
        {
            // TODO detection of types should be deferred if needed.
            throw new Exception();
        }

        return type;
    }

    public Type VisitExpression(Tree.Table table, bool isConstant)
    {
        // TODO iterate over fields
        return Type.Table;
    }

    public Type VisitExpression(Tree.Number number, bool isConstant)
    {
        return Type.Number;
    }

    public Type VisitExpression(Tree.String stringValue, bool isConstant)
    {
        return isConstant ? Type.StringPrimitive : new Type.StringConstant(stringValue.Value);
    }

    public Type VisitExpression(Tree.True trueValue, bool isConstant)
    {
        return isConstant ? Type.True : Type.Boolean;
    }

    public Type VisitExpression(Tree.False falseValue, bool isConstant)
    {
        return isConstant ? Type.False : Type.Boolean;
    }

    public Type VisitExpression(Tree.Nil nil, bool isConstant)
    {
        return Type.Nil;
    }

    public Type VisitExpression(Tree.Error error, bool isConstant)
    {
        return Type.Unknown;
    }

    private Checker(Source source)
    {
        this.source = source;
    }

    public Type VisitType(Tree.Name name)
    {
        if (source.TryGetTreeSymbol(name, out var symbol) && symbol is Symbol.TypeSymbol typeSymbol)
        {
            return typeSymbol.Type;
        }

        // TODO report error?
        return Type.Unknown;
    }

    public Type VisitType(Tree.FunctionType functionType)
    {
        return VisitFunctionType(functionType);
    }

    public static List<Diagnostic> Check(Source source)
    {
        var checker = new Checker(source);
        checker.VisitBlock(source.Tree);
        return checker.Diagnostics;
    }
}