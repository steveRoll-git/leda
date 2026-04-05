using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

public class Checker
{
    private readonly Source source;
    private List<Diagnostic> Diagnostics { get; } = [];

    private record FunctionInfo(Type.Function Function, bool InferReturn);

    private readonly Stack<FunctionInfo> functionStack = [];

    private readonly TypeEvaluator evaluator;

    private void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Visits all of a block's statements.
    /// </summary>
    private void VisitBlock(Tree.Block block)
    {
        // foreach (var typeDeclaration in block.TypeDeclarations)
        // {
        //     if (!source.TryGetTreeSymbol(typeDeclaration.Name, out var symbol))
        //     {
        //         throw new Exception();
        //     }
        //
        //     var type = VisitType(typeDeclaration.Type);
        //     source.SetSymbolType(symbol, type);
        //     if (type.UserNameable)
        //     {
        //         type.Name = typeDeclaration.Name.Value;
        //     }
        // }

        foreach (var statement in block.Statements)
        {
            VisitStatement(statement);
        }
    }

    private TypeList VisitCall(Tree.Expression.Call call)
    {
        var target = evaluator.GetTypeOfExpression(call.Target);

        if (target == Type.Unknown)
        {
            VisitExpressionList(call.Parameters);
            return TypeList.Unknown;
        }

        // TODO we could store a simple flag for whether a type is callable instead of checking like this.
        if (!IsAssignableFrom(Type.FunctionPrimitive, target)) // TODO handle __call metamethod
        {
            Report(new Diagnostic.TypeNotCallable(call.Target.Range));
            VisitExpressionList(call.Parameters);
            return TypeList.Unknown;
        }

        if (target == Type.FunctionPrimitive)
        {
            VisitExpressionList(call.Parameters);
            return TypeList.Any;
        }

        if (target is Type.Function function)
        {
            // TODO support overloads
            CheckAssignment(function.Parameters, new ExpressionListValueList(call.Parameters),
                TypeList.TypeListKind.Parameter, call.Target.Range);

            return function.Return;
        }

        throw new NotImplementedException("Types other than `Function` aren't callable yet");
    }

    private void VisitExpressionList(List<Tree.Expression> expressions)
    {
        foreach (var expression in expressions)
        {
            VisitExpression(expression);
        }
    }

    private void VisitFunction(Tree.Expression.Function function)
    {
        // TODO handle rest parameter

        if (function.Type.ReturnTypes != null)
        {
            VisitTypeList(function.Type.ReturnTypes);
        }

        functionStack.Push(new(evaluator.GetTypeOfFunction(function), function.Type.ReturnTypes == null));
        VisitBlock(function.Body);
        functionStack.Pop();
    }

    private void VisitTypeList(List<Tree.Type> typeTrees)
    {
        foreach (var typeTree in typeTrees)
        {
            VisitType(typeTree);
        }
    }

    private void VisitStatement(Tree.Statement stmt)
    {
        switch (stmt)
        {
            case Tree.Statement.Assignment assignment:
                VisitStatement(assignment);
                break;
            case Tree.Statement.Call call:
                VisitCall(call.CallExpr);
                break;
            case Tree.Statement.Do @do:
                VisitStatement(@do);
                break;
            case Tree.Statement.GlobalDeclaration globalDeclaration:
                VisitStatement(globalDeclaration);
                break;
            case Tree.Statement.If @if:
                VisitStatement(@if);
                break;
            case Tree.Statement.IteratorFor iteratorFor:
                VisitStatement(iteratorFor);
                break;
            case Tree.Statement.LocalDeclaration localDeclaration:
                VisitStatement(localDeclaration);
                break;
            case Tree.Statement.LocalFunctionDeclaration localFunctionDeclaration:
                VisitStatement(localFunctionDeclaration);
                break;
            case Tree.Statement.MethodCall methodCall:
                VisitExpression(methodCall.CallExpr);
                break;
            case Tree.Statement.NumericalFor numericalFor:
                VisitStatement(numericalFor);
                break;
            case Tree.Statement.RepeatUntil repeatUntil:
                VisitStatement(repeatUntil);
                break;
            case Tree.Statement.Return @return:
                VisitStatement(@return);
                break;
            case Tree.Statement.While @while:
                VisitStatement(@while);
                break;
        }
    }

    private void VisitExpression(Tree.Expression expr)
    {
        switch (expr)
        {
            case Tree.Expression.Access access:
                VisitExpression(access);
                break;
            case Tree.Expression.Binary binary:
                VisitExpression(binary);
                break;
            case Tree.Expression.Call call:
                VisitExpression(call);
                break;
            case Tree.Expression.Function function:
                VisitExpression(function);
                break;
            case Tree.Expression.MethodCall methodCall:
                VisitExpression(methodCall);
                break;
            case Tree.Expression.Table table:
                VisitExpression(table);
                break;
            case Tree.Expression.Unary unary:
                VisitExpression(unary);
                break;
        }
    }

    private void VisitType(Tree.Type type)
    {
        switch (type)
        {
            case Tree.Type.Function function:
                VisitType(function);
                break;
            case Tree.Type.Name name:
                VisitType(name);
                break;
            case Tree.Type.Table table:
                VisitType(table);
                break;
            case Tree.Type.StringLiteral stringLiteral:
                VisitType(stringLiteral);
                break;
            case Tree.Type.NumberLiteral numberLiteral:
                VisitType(numberLiteral);
                break;
        }

        throw new ArgumentOutOfRangeException(nameof(type));
    }

    private void VisitStatement(Tree.Statement.Do block)
    {
        VisitBlock(block.Body);
    }

    private void VisitStatement(Tree.Statement.NumericalFor numericalFor)
    {
        var startType = evaluator.GetTypeOfExpression(numericalFor.Start);
        if (!IsAssignableFrom(Type.NumberPrimitive, startType))
        {
            Report(new Diagnostic.ForLoopStartNotNumber(numericalFor.Start.Range, evaluator.TypeToString(startType)));
        }

        var limitType = evaluator.GetTypeOfExpression(numericalFor.Limit);
        if (!IsAssignableFrom(Type.NumberPrimitive, limitType))
        {
            Report(new Diagnostic.ForLoopLimitNotNumber(numericalFor.Limit.Range, evaluator.TypeToString(limitType)));
        }

        if (numericalFor.Step != null)
        {
            var stepType = evaluator.GetTypeOfExpression(numericalFor.Step);
            if (!IsAssignableFrom(Type.NumberPrimitive, stepType))
            {
                Report(new Diagnostic.ForLoopStepNotNumber(numericalFor.Step.Range, evaluator.TypeToString(stepType)));
            }
        }

        VisitBlock(numericalFor.Body);
    }

    private void VisitStatement(Tree.Statement.If ifStatement)
    {
        VisitExpression(ifStatement.Primary.Condition);
        VisitBlock(ifStatement.Primary.Body);

        foreach (var branch in ifStatement.ElseIfs)
        {
            VisitExpression(branch.Condition);
            VisitBlock(branch.Body);
        }

        if (ifStatement.ElseBody != null)
        {
            VisitBlock(ifStatement.ElseBody);
        }
    }

    private class ExpressionListValueList(List<Tree.Expression> expressions) : ITypeValueList
    {
        public ITypeValueList.TypeValue this[int index] =>
            new() { Value = index < expressions.Count ? expressions[index] : null };
    }

    private void VisitStatement(Tree.Statement.Assignment assignment)
    {
        for (var i = 0; i < assignment.Values.Count; i++)
        {
            var value = assignment.Values[i];
            VisitExpression(value);
            if (i >= assignment.Targets.Count)
            {
                Report(new Diagnostic.ValueNotAssigned(value.Range));
            }
        }

        for (var i = 0; i < assignment.Targets.Count; i++)
        {
            var target = assignment.Targets[i];
            var targetType = evaluator.GetTypeOfExpression(target);
            if (i < assignment.Values.Count)
            {
                var value = assignment.Values[i];
                CheckValueToType(targetType, value, target);
            }
            else
            {
                // TODO check trailing values
                CheckTypeToType(targetType, Type.Nil, target.Range);
                Report(new Diagnostic.TargetNotAssigned(target.Range));
            }
        }
    }

    /// <summary>
    /// Checks an assignment of a list of values to a list of targets, inferring types along the way.<br/>
    /// This is used in assignments, local declarations, and function parameters.
    /// </summary>
    /// <param name="targets">The targets being assigned to.</param>
    /// <param name="sources">The values being assigned.</param>
    /// <param name="kind">The kind of typelist being checked.</param>
    /// <param name="sideErrorRange">The range to show an error, if there are no source or target nodes.</param>
    private void CheckAssignment(ITypeValueList targets, ITypeValueList sources, TypeList.TypeListKind kind,
        Range sideErrorRange = new())
    {
        Range errorRange = new(); // TODO can we figure out an initial value for this?

        var targetIndex = 0;
        var sourceIndex = 0;

        while (true)
        {
            // TODO check rest
            var (targetType, targetExpression, _) = targets[targetIndex];
            if (targetExpression != null)
            {
                targetType = evaluator.GetTypeOfExpression(targetExpression);
                errorRange = targetExpression.Range;
            }

            var (sourceType, sourceExpression, _) = sources[sourceIndex];
            if (targetExpression == null && sourceExpression != null)
            {
                errorRange = sourceExpression.Range;
            }

            if (sources[sourceIndex + 1].IsNone)
            {
                // If this is the last source value, and it may produce a type list of its own, iterate over that.
                if (sourceExpression is Tree.Expression.Call call)
                {
                    sources = VisitCall(call);
                    sourceIndex = 0;
                    sourceType = sources[sourceIndex].Type;
                    sourceExpression = null;
                }

                if (sourceExpression is Tree.Expression.Vararg)
                {
                    throw new NotImplementedException();
                }
            }

            // TODO if target and source are typelists, control should probably be transferred to typelist's IsAssignableFrom

            if (targetType != null && targetType != Type.Unknown && targetType != Type.Any)
            {
                if (sourceType != null)
                {
                    CheckTypeToType(targetType, sourceType, errorRange);
                }
                else if (sourceExpression != null)
                {
                    CheckValueToType(targetType, sourceExpression, targetExpression);
                }
                else
                {
                    if (targets is TypeList targetTypeList &&
                        sourceIndex < targetTypeList.MinimumValues)
                    {
                        Report(new Diagnostic.TypeMismatch(sideErrorRange,
                            new TypeMismatch.NotEnoughValues(targetTypeList.MinimumValues, sourceIndex,
                                kind)));
                        break;
                    }

                    CheckTypeToType(targetType, Type.Nil, errorRange);
                }
            }
            else if (sourceExpression != null)
            {
                VisitExpression(sourceExpression);
            }
            else
            {
                break;
            }

            targetIndex++;
            sourceIndex++;
        }
    }

    private void VisitStatement(Tree.Statement.Return returnStatement)
    {
        var (function, inferReturn) = functionStack.Peek();
        if (!inferReturn)
        {
            CheckAssignment(function.Return, new ExpressionListValueList(returnStatement.Values),
                TypeList.TypeListKind.Return,
                returnStatement.Range);
        }
    }

    private void VisitStatement(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        VisitFunction(declaration.Function);
    }

    private void VisitStatement(Tree.Statement.GlobalDeclaration declaration)
    {
        throw new NotImplementedException();
    }

    private void VisitStatement(Tree.Statement.LocalDeclaration localDeclaration)
    {
        for (var i = 0; i < localDeclaration.Values.Count; i++)
        {
            var value = localDeclaration.Values[i];
            VisitExpression(value);
            if (i >= localDeclaration.Declarations.Count)
            {
                Report(new Diagnostic.ValueNotAssigned(value.Range));
            }
        }

        // For each local variable with a type annotation and a value assigned to it, we check the value with the type.
        for (var i = 0; i < localDeclaration.Declarations.Count; i++)
        {
            var declaration = localDeclaration.Declarations[i];
            if (declaration.Type != null)
            {
                if (i < localDeclaration.Values.Count)
                {
                    var targetType = evaluator.GetTypeOfTypeAnnotation(declaration.Type);
                    var value = localDeclaration.Values[i];
                    CheckValueToType(targetType, value, declaration.Name);
                }
                else
                {
                    // TODO check trailing values
                }
            }
        }
    }

    private void VisitStatement(Tree.Statement.RepeatUntil repeatUntil)
    {
        throw new NotImplementedException();
    }

    private void VisitStatement(Tree.Statement.While whileLoop)
    {
        throw new NotImplementedException();
    }

    private void VisitStatement(Tree.Statement.IteratorFor forLoop)
    {
        throw new NotImplementedException();
    }

    private void VisitExpression(Tree.Expression.Function function)
    {
        VisitFunction(function);
    }

    private void VisitExpression(Tree.Expression.MethodCall methodCall)
    {
        throw new NotImplementedException();
    }

    private void VisitExpression(Tree.Expression.Call call)
    {
        VisitCall(call);
    }

    private void VisitExpression(Tree.Expression.Access access)
    {
        VisitExpression(access.Target);
        VisitExpression(access.Key);

        var targetType = evaluator.GetTypeOfExpression(access.Target);
        if (targetType is not Type.Table)
        {
            Report(new Diagnostic.TypeNotIndexable(access.Target.Range, evaluator.TypeToString(targetType)));
            return;
        }

        if (evaluator.GetTypeOfAccess(access) == null)
        {
            Report(new Diagnostic.TypeDoesntHaveKey(access.Key.Range, evaluator.TypeToString(targetType),
                evaluator.TypeToString(evaluator.GetTypeOfExpression(access.Key, true))));
        }
    }

    private void VisitExpression(Tree.Expression.Binary binary)
    {
        throw new NotImplementedException();
    }

    private void VisitExpression(Tree.Expression.Unary unary)
    {
        VisitExpression(unary.Expression);
    }

    private void VisitExpression(Tree.Expression.Table table)
    {
        foreach (var field in table.Fields)
        {
            VisitExpression(field.Key);
            VisitExpression(field.Value);
        }
    }

    private Checker(Source source, TypeEvaluator evaluator)
    {
        this.source = source;
        this.evaluator = evaluator;
    }

    private void VisitType(Tree.Type.Function functionType)
    {
        foreach (var parameter in functionType.Parameters)
        {
            if (parameter.Type == null)
            {
                Report(new Diagnostic.ImplicitAnyType(parameter.Range, parameter.Name.Value));
            }
        }
    }

    private void VisitType(Tree.Type.Table table)
    {
        foreach (var (key, value) in table.Pairs)
        {
            VisitType(key);
            VisitType(value);
        }
    }

    /// <summary>
    /// Checks an assignment of a single value to a target type. If applicable, errors in the source value will be shown.
    /// </summary>
    /// <param name="targetType">The type of the target being assigned to.</param>
    /// <param name="sourceValue">The value being assigned.</param>
    /// <param name="targetValue">The tree node for the target value, if applicable.</param>
    private void CheckValueToType(Type targetType, Tree.Expression sourceValue, Tree.Expression? targetValue)
    {
        var errorRange = (targetValue ?? sourceValue).Range;

        targetType = Dereference(targetType);

        if (sourceValue is Tree.Expression.Table sourceTable && targetType is Type.Table targetTable)
        {
            evaluator.CompleteTableType(targetTable);

            var missingStrings = new HashSet<string>(targetTable.StringLiterals.Select(p => p.Key));
            // TODO check number literals too
            foreach (var sourceField in sourceTable.Fields)
            {
                var sourceKeyType = evaluator.GetTypeOfExpression(sourceField.Key, true);
                Type? targetValueType;
                if (sourceKeyType is Type.StringLiteral stringLiteral)
                {
                    targetValueType = targetTable.StringLiterals.GetValueOrDefault(stringLiteral.Literal);
                    missingStrings.Remove(stringLiteral.Literal);
                }
                else
                {
                    targetValueType = targetTable.Indexers.FirstOrDefault(p => IsAssignableFrom(p.Key, sourceKeyType))
                        .Value;
                }

                if (targetValueType == null)
                {
                    Report(new Diagnostic.TableLiteralOnlyKnownKeys(sourceField.Key.Range,
                        evaluator.TypeToString(targetTable),
                        evaluator.TypeToString(sourceKeyType)));
                }
                else
                {
                    // TODO check for duplicate fields
                    CheckValueToType(targetValueType, sourceField.Value, sourceField.Key);
                }
            }

            if (missingStrings.Count > 0)
            {
                Report(new Diagnostic.MissingStringKeys(errorRange, evaluator.TypeToString(targetType),
                    evaluator.TypeToString(evaluator.GetTypeOfExpression(sourceValue)),
                    missingStrings.ToList()));
            }
        }
        else
        {
            var valueType = evaluator.GetTypeOfExpression(sourceValue);
            CheckTypeToType(targetType, valueType, errorRange);
        }
    }

    /// <summary>
    /// Checks if `sourceType` is assignable to `targetType`, and reports a diagnostic if it isn't.<br/>
    /// </summary>
    /// <param name="targetType">The type being assigned to.</param>
    /// <param name="sourceType">The type being assigned from.</param>
    /// <param name="errorRange">The range where the diagnostic should be shown.</param>
    private void CheckTypeToType(Type targetType, Type sourceType, Range errorRange)
    {
        if (!IsAssignableFrom(targetType, sourceType, out var reason))
        {
            Report(new Diagnostic.TypeMismatch(errorRange, reason));
        }
    }

    /// <summary>
    /// If the type points to another type, returns the pointed-to type.
    /// </summary>
    private Type Dereference(Type type)
    {
        if (type is Type.Reference reference)
        {
            // TODO this should be done in the evaluator
            return Dereference(evaluator.GetTypeOfSymbol(reference.Symbol));
        }

        return type;
    }

    private static bool IsTriviallyAssignableFrom(Type targetType, Type sourceType)
    {
        if (targetType == Type.Unknown || sourceType == Type.Unknown)
        {
            return true;
        }

        return targetType == sourceType;
    }

    private bool IsAssignableFrom(Type targetType, Type sourceType, [NotNullWhen(false)] out TypeMismatch? reason)
    {
        reason = null;
        if (IsTriviallyAssignableFrom(targetType, sourceType))
        {
            return true;
        }

        targetType = Dereference(targetType);
        sourceType = Dereference(sourceType);
        if (IsTriviallyAssignableFrom(targetType, sourceType))
        {
            return true;
        }

        if (targetType is Type.PrimitiveType primitive)
        {
            if (primitive.AssignableFunc(sourceType))
            {
                return true;
            }

            reason = new TypeMismatch.Primitive(evaluator.TypeToString(targetType), evaluator.TypeToString(sourceType));
            return false;
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (targetType is Type.NumberLiteral numberLiteral)
        {
            if (sourceType is Type.NumberLiteral sourceLiteral &&
                numberLiteral.Literal == sourceLiteral.Literal)
            {
                return true;
            }

            reason = new TypeMismatch.Primitive(evaluator.TypeToString(targetType), evaluator.TypeToString(sourceType));
            return false;
        }

        if (targetType is Type.StringLiteral stringLiteral)
        {
            if (sourceType is Type.StringLiteral sourceLiteral &&
                stringLiteral.Literal == sourceLiteral.Literal)
            {
                return true;
            }

            reason = new TypeMismatch.Primitive(evaluator.TypeToString(targetType), evaluator.TypeToString(sourceType));
            return false;
        }

        if (targetType is Type.Table targetTable && sourceType is Type.Table sourceTable)
        {
            return IsAssignableFrom(targetTable, sourceTable, out reason);
        }

        if (targetType is Type.Function targetFunction && sourceType is Type.Function sourceFunction)
        {
            return IsAssignableFrom(targetFunction, sourceFunction, out reason);
        }

        reason = new TypeMismatch.Primitive(evaluator.TypeToString(targetType), evaluator.TypeToString(sourceType));
        return false;
    }

    private bool IsAssignableFrom(Type targetType, Type sourceType)
    {
        return IsAssignableFrom(targetType, sourceType, out _);
    }

    private bool IsAssignableFrom(Type.Table targetTable, Type.Table sourceTable,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        List<TypeMismatch> reasons = [];

        evaluator.CompleteTableType(targetTable);

        foreach (var (targetKey, targetValue) in targetTable.StringLiterals)
        {
            if (targetValue == null)
            {
                continue;
            }

            var sourceValue = evaluator.GetTypeOfStringKeyInTable(sourceTable, targetKey);
            if (sourceValue == null)
            {
                reasons.Add(new TypeMismatch.SourceMissingKey(evaluator.TypeToString(targetTable),
                    evaluator.TypeToString(sourceTable),
                    '"' + targetKey + '"'));
                continue;
            }

            if (!IsAssignableFrom(targetValue, sourceValue, out var valueReason))
            {
                reasons.Add(new TypeMismatch.TableKeyIncompatible('"' + targetKey + '"')
                    { Children = [valueReason] });
            }
        }
        // TODO check number literals too

        if (reasons.Count > 0)
        {
            reason = new TypeMismatch.Primitive(evaluator.TypeToString(targetTable),
                evaluator.TypeToString(sourceTable)) { Children = reasons };
            return false;
        }

        reason = null;
        return true;
    }

    private bool IsAssignableFrom(Type.Function targetFunction, Type.Function sourceFunction,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        List<TypeMismatch> reasons = [];
        if (!IsAssignableFrom(sourceFunction.Parameters, targetFunction.Parameters, out var parameterReasons,
                TypeList.TypeListKind.FunctionTypeParameter))
        {
            reasons.AddRange(parameterReasons);
        }

        if (!IsAssignableFrom(targetFunction.Return, sourceFunction.Return, out var returnReasons,
                TypeList.TypeListKind.FunctionTypeReturn))
        {
            reasons.AddRange(returnReasons);
        }

        if (reasons.Count > 0)
        {
            reason = new TypeMismatch.Primitive(evaluator.TypeToString(targetFunction),
                evaluator.TypeToString(sourceFunction)) { Children = reasons };
            return false;
        }

        reason = null;
        return true;
    }

    private bool IsAssignableFrom(TypeList target, TypeList other, [NotNullWhen(false)] out List<TypeMismatch>? reasons,
        TypeList.TypeListKind kind)
    {
        if (other.MinimumValues < target.MinimumValues)
        {
            reasons = [new TypeMismatch.NotEnoughValues(target.MinimumValues, other.MinimumValues, kind)];
            return false;
        }

        reasons = [];

        var targetIndex = 0;
        var sourceIndex = 0;

        while (true)
        {
            var (sourceType, _, isSourceRest) = other[sourceIndex];
            var (targetType, _, isTargetRest) = target[targetIndex];

            if (targetType == null)
            {
                // No more target types, there is no need to check the source further.
                // (But a warning about excessive values could be shown here)
                break;
            }

            if (isTargetRest && sourceType == null)
            {
                // If the target type list has a rest type, we only need to check them as long as the source is
                // providing unique types.
                break;
            }

            if (!IsAssignableFrom(targetType, sourceType ?? Type.Nil, out var subReason))
            {
                reasons.Add(new TypeMismatch.ValueInListIncompatible(sourceIndex, kind) { Children = [subReason] });
            }

            if (isTargetRest && isSourceRest)
            {
                // If both type lists end with a rest type, we have to check their assignability just once.
                break;
            }

            sourceIndex++;
            targetIndex++;
        }

        if (reasons.Count > 0)
        {
            return false;
        }

        reasons = null;
        return true;
    }

    public static List<Diagnostic> Check(Source source, TypeEvaluator evaluator)
    {
        var checker = new Checker(source, evaluator);
        checker.functionStack.Push(new(new Type.Function(TypeList.Any, TypeList.Any, []), false)); // TODO
        checker.VisitBlock(source.Tree);
        return checker.Diagnostics;
    }
}