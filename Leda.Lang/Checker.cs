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
        foreach (var typeDeclaration in block.TypeDeclarations)
        {
            VisitType(typeDeclaration.Type);
        }

        foreach (var statement in block.Statements)
        {
            VisitStatement(statement);
        }
    }

    private void VisitCall(Tree.Expression.Call call)
    {
        VisitExpression(call.Target);
        VisitExpressionList(call.Parameters);

        var target = evaluator.GetTypeOfExpression(call.Target);

        if (target == Type.Unknown)
        {
            return;
        }

        // TODO we could store a simple flag for whether a type is callable instead of checking like this.
        if (!IsAssignableFrom(Type.FunctionPrimitive, target)) // TODO handle __call metamethod
        {
            Report(new Diagnostic.TypeNotCallable(call.Target.Range));
            return;
        }

        if (target is Type.Function function)
        {
            // TODO support overloads
            CheckAssignment(function.Parameters, call.Parameters, TypeListKind.Parameter, call.Target.Range);
        }
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

        for (var i = 0; i < function.Type.Parameters.Count; i++)
        {
            var parameter = function.Type.Parameters[i];
            if (parameter.Type == null && evaluator.GetInferredParameterType(function, i) == null)
            {
                Report(new Diagnostic.ImplicitAnyType(parameter.Name.Range, parameter.Name.Value));
            }
        }

        functionStack.Push(new(evaluator.GetTypeOfFunction(function), function.Type.ReturnTypes == null));
        VisitBlock(function.Chunk);
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
            case Tree.Type.Table table:
                VisitType(table);
                break;
        }
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

    private void VisitStatement(Tree.Statement.Assignment assignment)
    {
        foreach (var value in assignment.Values)
        {
            VisitExpression(value);
        }

        var sideErrorRange = assignment.Values.Count >= 1
            ? assignment.Values[0].Range.Union(assignment.Values[^1].Range)
            : assignment.Range;

        CheckAssignment(new TypeList.AssignmentTargets(assignment.Targets), assignment.Values, TypeListKind.Value,
            sideErrorRange);
    }

    /// <summary>
    /// Checks an assignment of a list of values to a list of target types.
    /// </summary>
    /// <param name="targets">The targets being assigned to.</param>
    /// <param name="sources">The values being assigned.</param>
    /// <param name="kind">The kind of typelist being checked.</param>
    /// <param name="sideErrorRange">The range to show an error, if there are no source or target nodes.</param>
    private void CheckAssignment(TypeList targets, List<Tree.Expression> sources, TypeListKind kind,
        Range sideErrorRange)
    {
        var expectedValues = evaluator.GetTypeListMinimum(targets);
        var gotValues = evaluator.GetMinimumNumberOfValues(sources);
        if (gotValues < expectedValues)
        {
            Report(new Diagnostic.TypeMismatch(sideErrorRange,
                new TypeMismatch.NotEnoughValues(expectedValues, gotValues, kind)));
            return;
        }

        var targetsHaveRest = evaluator.DoesTypeListHaveRest(targets);
        var maximum = evaluator.GetTypeListMaximum(targets);

        for (var i = 0; i < sources.Count && (targetsHaveRest || i < maximum); i++)
        {
            var value = sources[i];
            // If the last expression is one that returns a TypeList (and that TypeList returns more than one value),
            // check it with `IsAssignableFrom`.
            if (i == sources.Count - 1 && i < maximum - 1 &&
                evaluator.GetTypeListOfExpression(value) is { } sourceTypeList &&
                evaluator.GetTypeListMinimum(sourceTypeList) > 1)
            {
                if (!IsAssignableFrom(targets, sourceTypeList, out var reasons, TypeListKind.Return, targetIndex: i))
                {
                    Report(new Diagnostic.TypeMismatch(value.Range,
                        new TypeMismatch.TrailingValuesIncompatible { Children = reasons }));
                }

                break;
            }

            // TODO store & reuse existing rest type
            var targetType = evaluator.GetTypeInTypeList(targets, i);
            CheckValueToType(targetType, value,
                targets is TypeList.AssignmentTargets { Targets: var targetValues } ? targetValues[i] : null);
        }

        if (!targetsHaveRest && sources.Count > maximum)
        {
            var firstExcessive = sources[maximum];
            var lastExcessive = sources[^1];
            Report(new Diagnostic.TooManyValues(firstExcessive.Range.Union(lastExcessive.Range), kind, maximum,
                sources.Count));
        }
    }

    private void VisitStatement(Tree.Statement.Return returnStatement)
    {
        var (function, inferReturn) = functionStack.Peek();
        if (!inferReturn)
        {
            CheckAssignment(function.Return, returnStatement.Values, TypeListKind.Return, returnStatement.Range);
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
                var targetType = evaluator.GetTypeOfTypeAnnotation(declaration.Type);
                if (i < localDeclaration.Values.Count)
                {
                    var value = localDeclaration.Values[i];
                    CheckValueToType(targetType, value, declaration.Name);
                }
                else if (localDeclaration.Values.Count >= 1 &&
                         evaluator.GetTypeListOfExpression(localDeclaration.Values[^1]) is { } typeList)
                {
                    CheckTypeToType(targetType,
                        evaluator.GetTypeInTypeList(typeList, i - localDeclaration.Values.Count + 1),
                        declaration.Name.Range);
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
                TypeListKind.FunctionTypeParameter))
        {
            reasons.AddRange(parameterReasons);
        }

        if (!IsAssignableFrom(targetFunction.Return, sourceFunction.Return, out var returnReasons,
                TypeListKind.FunctionTypeReturn))
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

    private bool IsAssignableFrom(TypeList targets, TypeList sources,
        [NotNullWhen(false)] out List<TypeMismatch>? reasons,
        TypeListKind kind,
        int targetIndex = 0)
    {
        reasons = [];

        var targetMinimum = evaluator.GetTypeListMinimum(targets) - targetIndex;
        var sourceMinimum = evaluator.GetTypeListMinimum(sources);
        if (sourceMinimum < targetMinimum)
        {
            reasons.Add(new TypeMismatch.NotEnoughValues(targetMinimum, sourceMinimum, kind));
            return false;
        }

        var sourcesHaveRest = evaluator.DoesTypeListHaveRest(sources);
        var targetsHaveRest = evaluator.DoesTypeListHaveRest(targets);

        int maximum;
        if (sourcesHaveRest && !targetsHaveRest)
        {
            maximum = evaluator.GetTypeListMaximum(targets);
        }
        else if (!sourcesHaveRest && targetsHaveRest)
        {
            maximum = evaluator.GetTypeListMaximum(sources) + targetIndex;
        }
        else
        {
            maximum = Math.Min(evaluator.GetTypeListMaximum(targets),
                evaluator.GetTypeListMaximum(sources) + targetIndex);
        }

        var sourceIndex = 0;
        for (; targetIndex < maximum; targetIndex++)
        {
            var sourceType = evaluator.GetTypeInTypeList(sources, sourceIndex);
            var targetType = evaluator.GetTypeInTypeList(targets, targetIndex);

            if (!IsAssignableFrom(targetType, sourceType, out var subReason))
            {
                reasons.Add(new TypeMismatch.ValueInListIncompatible(sourceIndex, kind) { Children = [subReason] });
            }

            sourceIndex++;
        }

        if (targetsHaveRest && sourcesHaveRest)
        {
            // TODO compare rest types
        }

        if (reasons.Count > 0)
        {
            return false;
        }

        reasons = null;
        return true;
    }

    private Checker(Source source, TypeEvaluator evaluator)
    {
        this.source = source;
        this.evaluator = evaluator;
    }

    public static List<Diagnostic> Check(Source source, TypeEvaluator evaluator)
    {
        var checker = new Checker(source, evaluator);
        checker.functionStack.Push(new(new Type.Function(TypeList.Any, TypeList.Any, []), false)); // TODO
        checker.VisitBlock(source.Tree);
        return checker.Diagnostics;
    }
}