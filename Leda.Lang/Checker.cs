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
    /// Returns whether `tree` is a simple literal, which means that when it appears as a key, its type should be
    /// interpreted as constant.
    /// </summary>
    private static bool IsSimpleLiteral(Tree.Expression tree) =>
        tree is Tree.Expression.String or Tree.Expression.Number or Tree.Expression.True or Tree.Expression.False;

    /// <summary>
    /// Visits all of a block's statements.
    /// </summary>
    private void VisitBlock(Tree.Block block)
    {
        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            if (!source.TryGetTreeSymbol(typeDeclaration.Name, out var symbol))
            {
                throw new Exception();
            }

            var type = typeDeclaration.Type.AcceptTypeVisitor(this);
            source.SetSymbolType(symbol, type);
            if (type.UserNameable)
            {
                type.Name = typeDeclaration.Name.Value;
            }
        }

        foreach (var statement in block.Statements)
        {
            statement.AcceptVisitor(this);
        }
    }

    private TypeList VisitCall(Tree.Expression.Call call)
    {
        var parameters = VisitExpressionList(call.Parameters);
        var target = call.Target.AcceptExpressionVisitor(this, false);

        if (target == Type.Unknown)
        {
            return TypeList.Unknown;
        }

        if (!Type.FunctionPrimitive.IsAssignableFrom(target)) // TODO handle __call metamethod
        {
            Report(new Diagnostic.TypeNotCallable(call.Target.Range));
            return TypeList.Unknown;
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

    private TypeList VisitExpressionList(List<Tree.Expression> expressions)
    {
        List<Type> list = new(1);
        TypeList? continued = null;
        foreach (var expression in expressions)
        {
            if (continued != null)
            {
                // If more items are present after the last continued list, only its first value is added.
                // TODO show warning about discarded values?
                list.Add(continued.GetIterator().Current ?? Type.Nil);
            }

            if (expression is Tree.Expression.Call call)
            {
                continued = VisitCall(call);
            }
            else if (expression is Tree.Expression.Vararg)
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

    private Type.Function VisitFunction(Tree.Expression.Function function, Type.Function? targetFunction)
    {
        var iterator = targetFunction?.Parameters.GetIterator();
        List<Type> parameters = [];
        List<string> paramNames = [];
        foreach (var parameter in function.Type.Parameters)
        {
            Type type;
            if (parameter.Type != null)
            {
                type = parameter.Type.AcceptTypeVisitor(this);
            }
            else if (iterator != null && iterator.Next(out var targetParamType))
            {
                type = targetParamType;
            }
            else
            {
                type = Type.Any;
                Report(new Diagnostic.ImplicitAnyType(parameter.Range, parameter.Name.Value));
            }

            parameters.Add(type);

            if (!source.TryGetTreeSymbol(parameter.Name, out var symbol))
            {
                throw new Exception("Parameter doesn't have symbol");
            }

            source.SetSymbolType(symbol, type);

            paramNames.Add(parameter.Name.Value);
        }

        var parameterTypeList = new TypeList(parameters) { NameList = paramNames };
        // TODO handle rest parameter

        var returnTypeList = GetFunctionReturnType(function.Type);
        returnTypeList ??= TypeList.None; // TODO infer return type

        VisitBlock(function.Body);

        return new Type.Function(parameterTypeList, returnTypeList);
    }

    private TypeList? GetFunctionReturnType(Tree.Type.Function functionType)
    {
        if (functionType.ReturnTypes == null)
        {
            return null;
        }

        List<Type> returnTypes = [];
        foreach (var returnType in functionType.ReturnTypes)
        {
            returnTypes.Add(returnType.AcceptTypeVisitor(this));
        }

        // TODO handle Rest and Continued
        return new TypeList(returnTypes);
    }

    private Type GetAccessType(Tree.Expression target, Tree.Expression key, bool isConstant)
    {
        var targetType = target.AcceptExpressionVisitor(this, isConstant);
        // TODO handle __index
        if (targetType is not Type.Table table)
        {
            Report(new Diagnostic.TypeNotIndexable(target.Range, targetType));
            return Type.Unknown;
        }

        var keyType = key.AcceptExpressionVisitor(this, IsSimpleLiteral(key));

        if (keyType == Type.Unknown)
        {
            return Type.Unknown;
        }

        // TODO use lookup
        foreach (var pair in table.Pairs)
        {
            if (pair.Key.IsAssignableFrom(keyType))
            {
                return pair.Value;
            }
        }

        Report(new Diagnostic.TypeDoesntHaveKey(key.Range, targetType, keyType));

        return Type.Unknown;
    }

    public void Visit(Tree.Statement.Do block)
    {
        VisitBlock(block.Body);
    }

    public void Visit(Tree.Statement.NumericalFor numericalFor)
    {
        var startType = numericalFor.Start.AcceptExpressionVisitor(this, false);
        if (!Type.NumberPrimitive.IsAssignableFrom(startType))
        {
            Report(new Diagnostic.ForLoopStartNotNumber(numericalFor.Start.Range, startType));
        }

        var limitType = numericalFor.Limit.AcceptExpressionVisitor(this, false);
        if (!Type.NumberPrimitive.IsAssignableFrom(limitType))
        {
            Report(new Diagnostic.ForLoopLimitNotNumber(numericalFor.Limit.Range, limitType));
        }

        if (numericalFor.Step != null)
        {
            var stepType = numericalFor.Step.AcceptExpressionVisitor(this, false);
            if (!Type.NumberPrimitive.IsAssignableFrom(stepType))
            {
                Report(new Diagnostic.ForLoopStepNotNumber(numericalFor.Step.Range, stepType));
            }
        }

        if (!source.TryGetTreeSymbol(numericalFor.Counter, out var counterSymbol))
        {
            throw new Exception("No symbol for `for` loop counter");
        }

        source.SetSymbolType(counterSymbol, Type.NumberPrimitive);

        VisitBlock(numericalFor.Body);
    }

    public void Visit(Tree.Statement.If ifStatement)
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

    public void Visit(Tree.Statement.Assignment assignment)
    {
        CheckAssignment(assignment, assignment.Values);
    }

    /// <summary>
    /// Checks an assignment of a list of values to a list of targets, inferring types along the way.<br/>
    /// This is used in assignments, local declarations, and TODO function parameters.
    /// </summary>
    /// <param name="targets">The targets being assigned to.</param>
    /// <param name="values">The values being assigned.</param>
    public void CheckAssignment(Tree.IAssignmentTargetList targets, List<Tree.Expression> values)
    {
        for (var i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var targetType = target.AcceptExpressionVisitor(this, false);
            if (i < values.Count)
            {
                var value = values[i];
                if (i == values.Count - 1 && value is Tree.Expression.Call or Tree.Expression.Vararg)
                {
                    // If the last value being assigned could be a type list, switch over to this method.
                    CheckTypeListAssignment(targets, value, i);
                    return;
                }

                CheckSingleAssignment(targetType, value, target);
            }
            else
            {
                CheckSimpleAssign(targetType, Type.Nil, target.Range);
            }
        }
    }

    /// <summary>
    /// Does an assignment check for a list of targets, with a value that evaluates to a type list.
    /// </summary>
    /// <param name="targets">The targets being assigned to.</param>
    /// <param name="value">The value being assigned, that returns a type list.</param>
    /// <param name="i">The index to start from.</param>
    private void CheckTypeListAssignment(Tree.IAssignmentTargetList targets, Tree.Expression value, int i)
    {
        TypeList typeList;
        if (value is Tree.Expression.Call call)
        {
            typeList = VisitCall(call);
        }
        else
        {
            throw new NotImplementedException();
        }

        var iterator = typeList.GetIterator();
        for (; i < targets.Count; i++)
        {
            var target = targets[i];
            var targetType = target.AcceptExpressionVisitor(this, false);
            if (iterator.Next(out var sourceType))
            {
                CheckSimpleAssign(targetType, sourceType, target.Range);
            }
            else
            {
                CheckSimpleAssign(targetType, Type.Nil, target.Range);
            }
        }
    }

    public void Visit(Tree.Expression.MethodCall methodCall)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Expression.Call call)
    {
        VisitCall(call);
    }

    public void Visit(Tree.Expression.Access access)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Expression.Binary binary)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Expression.Unary unary)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Expression.Function function)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Expression.Name name)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Type.Name name)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Expression.Table table)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Statement.Return returnStatement)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        var functionType = VisitFunction(declaration.Function, null);
        if (!source.TryGetTreeSymbol(declaration.Name, out var symbol))
        {
            throw new Exception();
        }

        source.SetSymbolType(symbol, functionType);
    }

    public void Visit(Tree.Statement.GlobalDeclaration declaration)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Statement.LocalDeclaration localDeclaration)
    {
        foreach (var declaration in localDeclaration.Declarations)
        {
            Type variableType;

            if (declaration.Type != null)
            {
                variableType = declaration.Type.AcceptTypeVisitor(this);
            }
            else
            {
                // The variable's type is inferred from the value.
                variableType = new Type.Infer();
            }

            if (!source.TryGetTreeSymbol(declaration.Name, out var symbol))
            {
                throw new Exception("Variable doesn't have a symbol");
            }

            source.SetSymbolType(symbol, variableType);
        }

        CheckAssignment(localDeclaration, localDeclaration.Values);
    }

    public void Visit(Tree.Statement.RepeatUntil repeatUntil)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Statement.While whileLoop)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Statement.IteratorFor forLoop)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Type.Function functionType)
    {
        throw new NotImplementedException();
    }

    public void Visit(Tree.Type.Table table)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Expression.Function function, bool isConstant)
    {
        return VisitFunction(function, null);
    }

    public Type VisitExpression(Tree.Expression.MethodCall methodCall, bool isConstant)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Expression.Call call, bool isConstant)
    {
        // TODO show warning if the call returns more than one value?
        return VisitCall(call).GetIterator().Current ?? Type.Nil;
    }

    public Type VisitExpression(Tree.Expression.Access access, bool isConstant)
    {
        return GetAccessType(access.Target, access.Key, isConstant);
    }

    public Type VisitExpression(Tree.Expression.Binary binary, bool isConstant)
    {
        throw new NotImplementedException();
    }

    public Type VisitExpression(Tree.Expression.Unary unary, bool isConstant)
    {
        var exprType = unary.Expression.AcceptExpressionVisitor(this, isConstant);

        if (unary.Operator is Token.Not)
        {
            return Type.Boolean;
        }

        if (unary.Operator is Token.Length)
        {
            // TODO use __len metamethod
            if (!Type.TablePrimitive.IsAssignableFrom(exprType) && !Type.StringPrimitive.IsAssignableFrom(exprType))
            {
                Report(new Diagnostic.CantGetLength(unary.Range, exprType));
            }

            return Type.NumberPrimitive;
        }

        if (unary.Operator is Token.Minus)
        {
            // TODO use __unm metamethod
            if (!Type.NumberPrimitive.IsAssignableFrom(exprType))
            {
                Report(new Diagnostic.CantNegate(unary.Range, exprType));
            }

            return Type.NumberPrimitive;
        }

        throw new Exception(); // Unreachable.
    }

    public Type VisitExpression(Tree.Expression.Name name, bool isConstant)
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

    public Type VisitExpression(Tree.Expression.Table table, bool isConstant)
    {
        List<Type.Table.Pair> pairs = [];

        foreach (var field in table.Fields)
        {
            pairs.Add(new Type.Table.Pair(
                field.Key.AcceptExpressionVisitor(this, true),
                // The value is visited as a non-constant — until we'll have const assertions like TypeScript's.
                field.Value.AcceptExpressionVisitor(this, false)));
        }

        return new Type.Table(pairs);
    }

    public Type VisitExpression(Tree.Expression.Number number, bool isConstant)
    {
        return isConstant ? new Type.NumberLiteral(number.NumberValue) : Type.NumberPrimitive;
    }

    public Type VisitExpression(Tree.Expression.String stringValue, bool isConstant)
    {
        return isConstant ? new Type.StringLiteral(stringValue.Value) : Type.StringPrimitive;
    }

    public Type VisitExpression(Tree.Expression.True trueValue, bool isConstant)
    {
        return isConstant ? Type.True : Type.Boolean;
    }

    public Type VisitExpression(Tree.Expression.False falseValue, bool isConstant)
    {
        return isConstant ? Type.False : Type.Boolean;
    }

    public Type VisitExpression(Tree.Expression.Nil nil, bool isConstant)
    {
        return Type.Nil;
    }

    public Type VisitExpression(Tree.Expression.Error error, bool isConstant)
    {
        return Type.Unknown;
    }

    private Checker(Source source)
    {
        this.source = source;
    }

    public Type VisitType(Tree.Type.Name name)
    {
        if (source.TryGetTreeSymbol(name, out var symbol))
        {
            if (symbol is Symbol.IntrinsicType intrinsicType)
            {
                return intrinsicType.Type;
            }

            if (source.TryGetSymbolType(symbol, out var type))
            {
                return type;
            }
        }

        // TODO report error?
        return Type.Unknown;
    }

    public Type VisitType(Tree.Type.Function functionType)
    {
        // This code does a lot of the same things that VisitFunction does,
        // perhaps the shared parts could be merged somehow?
        List<Type> parameters = [];
        List<string> paramNames = [];
        foreach (var parameter in functionType.Parameters)
        {
            if (parameter.Type != null)
            {
                parameters.Add(parameter.Type.AcceptTypeVisitor(this));
            }
            else
            {
                Report(new Diagnostic.ImplicitAnyType(parameter.Range, parameter.Name.Value));
            }

            paramNames.Add(parameter.Name.Value);
        }

        var parameterTypeList = new TypeList(parameters) { NameList = paramNames };
        // TODO handle rest parameter

        var returnTypeList = GetFunctionReturnType(functionType);
        returnTypeList ??= TypeList.None;

        return new Type.Function(parameterTypeList, returnTypeList);
    }

    public Type VisitType(Tree.Type.Table table)
    {
        List<Type.Table.Pair> pairs = [];

        foreach (var (key, value) in table.Pairs)
        {
            pairs.Add(new(key.AcceptTypeVisitor(this), value.AcceptTypeVisitor(this)));
        }

        return new Type.Table(pairs);
    }

    public Type VisitType(Tree.Type.StringLiteral stringLiteral)
    {
        return new Type.StringLiteral(stringLiteral.Value);
    }

    public Type VisitType(Tree.Type.NumberLiteral numberLiteral)
    {
        return new Type.NumberLiteral(numberLiteral.Value);
    }

    /// <summary>
    /// Checks an assignment of a single value to a target type. If applicable, errors in the source value will be shown,
    /// and types of function parameters will be inferred.
    /// </summary>
    /// <param name="targetType">The type of the target being assigned to.</param>
    /// <param name="sourceValue">The value being assigned.</param>
    /// <param name="targetValue">The tree node for the target value, if applicable.</param>
    private void CheckSingleAssignment(Type targetType, Tree.Expression sourceValue, Tree.Expression? targetValue)
    {
        var errorRange = (targetValue ?? sourceValue).Range;

        if (sourceValue is Tree.Expression.Table sourceTable && targetType is Type.Table targetTable)
        {
            // TODO use lookup
            var missingKeys = new HashSet<Type>(targetTable.Pairs.Select(p => p.Key));
            foreach (var sourceField in sourceTable.Fields)
            {
                var sourceKeyType = sourceField.Key.AcceptExpressionVisitor(this, IsSimpleLiteral(sourceField.Key));
                var targetKey = missingKeys.FirstOrDefault(k => k.IsAssignableFrom(sourceKeyType));
                if (targetKey == null)
                {
                    // TODO check for duplicate fields
                    Report(new Diagnostic.TableLiteralOnlyKnownKeys(sourceField.Key.Range, targetTable,
                        sourceKeyType));
                    sourceField.Value.AcceptExpressionVisitor(this, false);
                }
                else
                {
                    missingKeys.Remove(targetKey);
                    var targetPair = targetTable.Pairs.Find(p => p.Key == targetKey);
                    CheckSingleAssignment(targetPair.Value, sourceField.Value, sourceField.Key);
                }
            }

            if (missingKeys.Count > 0)
            {
                Report(new Diagnostic.MissingKeys(errorRange, targetType,
                    sourceValue.AcceptExpressionVisitor(this, false),
                    missingKeys.ToList()));
            }
        }
        else if (sourceValue is Tree.Expression.Function sourceFunction &&
                 targetType is Type.Function targetFunction)
        {
            var sourceType = VisitFunction(sourceFunction, targetFunction);
            CheckSimpleAssign(targetType, sourceType, errorRange);
        }
        else
        {
            var valueType = sourceValue.AcceptExpressionVisitor(this, false);
            CheckSimpleAssign(targetType, valueType, errorRange);
        }
    }

    /// <summary>
    /// Checks if `sourceType` is assignable to `targetType`, and reports a diagnostic if it isn't.<br/>
    /// Additionally, updates `Infer` types to the given `sourceType`.
    /// </summary>
    /// <param name="targetType">The type being assigned to.</param>
    /// <param name="sourceType">The type being assigned from.</param>
    /// <param name="errorRange">The range where the diagnostic should be shown.</param>
    private void CheckSimpleAssign(Type targetType, Type sourceType, Range errorRange)
    {
        if (targetType is Type.Infer { Inferred: null } infer)
        {
            // TODO this may have unintended consequences and a type may be inferred way outside of the place it's
            // intended to get inferred in
            infer.Inferred = sourceType;
        }
        else if (!targetType.IsAssignableFrom(sourceType, out var reason))
        {
            Report(new Diagnostic.TypeMismatch(errorRange, reason));
        }
    }

    public static List<Diagnostic> Check(Source source)
    {
        var checker = new Checker(source);
        checker.VisitBlock(source.Tree);
        return checker.Diagnostics;
    }
}