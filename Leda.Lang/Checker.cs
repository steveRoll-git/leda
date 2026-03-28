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

            var type = VisitType(typeDeclaration.Type);
            source.SetSymbolType(symbol, type);
            if (type.UserNameable)
            {
                type.Name = typeDeclaration.Name.Value;
            }
        }

        foreach (var statement in block.Statements)
        {
            VisitStatement(statement);
        }
    }

    private TypeList VisitCall(Tree.Expression.Call call)
    {
        var target = VisitExpression(call.Target, false);

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

    private TypeList VisitExpressionList(List<Tree.Expression> expressions)
    {
        if (expressions.Count == 0)
        {
            return TypeList.None;
        }

        List<Type> list = new(1);
        TypeList? continued = null;
        foreach (var expression in expressions)
        {
            if (continued != null)
            {
                // If more items are present after the last continued list, only its first value is added.
                // TODO show warning about discarded values?
                list.Add(continued[0].Type ?? Type.Nil);
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
                var type = VisitExpression(expression, false);
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
        List<Type.TypeParameter> typeParameters = [];
        if (function.Type.TypeParameters != null)
        {
            foreach (var name in function.Type.TypeParameters)
            {
                var typeParameter = new Type.TypeParameter(name.Value);
                typeParameters.Add(typeParameter);
                if (source.TryGetTreeSymbol(name, out var symbol))
                {
                    source.SetSymbolType(symbol, typeParameter);
                }
            }
        }

        var targetParamIndex = 0;
        List<Type> parameters = [];
        List<string> paramNames = [];
        foreach (var parameter in function.Type.Parameters)
        {
            Type type;
            if (parameter.Type != null)
            {
                type = VisitType(parameter.Type);
            }
            else if (targetFunction?.Parameters[targetParamIndex].Type is { } targetParamType)
            {
                type = targetParamType;
                targetParamIndex++;
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

        TypeList? returnTypeList = null;
        if (function.Type.ReturnTypes != null)
        {
            returnTypeList = VisitTypeList(function.Type.ReturnTypes);
        }

        var functionType = new Type.Function(parameterTypeList, returnTypeList ?? TypeList.Unknown, typeParameters);

        functionStack.Push(new(functionType, returnTypeList == null));
        VisitBlock(function.Body);
        functionStack.Pop();

        return functionType;
    }

    private TypeList VisitTypeList(List<Tree.Type> typeTrees)
    {
        List<Type> types = [];
        foreach (var typeTree in typeTrees)
        {
            types.Add(VisitType(typeTree));
        }

        return new TypeList(types);
    }

    private Type GetAccessType(Tree.Expression target, Tree.Expression key, bool isConstant)
    {
        var targetType = VisitExpression(target, isConstant);
        // TODO handle __index
        if (targetType is not Type.Table table)
        {
            Report(new Diagnostic.TypeNotIndexable(target.Range, targetType));
            return Type.Unknown;
        }

        var keyType = VisitExpression(key, IsSimpleLiteral(key));

        if (keyType == Type.Unknown)
        {
            return Type.Unknown;
        }

        // TODO use lookup
        foreach (var pair in table.Pairs)
        {
            if (IsAssignableFrom(pair.Key, keyType))
            {
                return pair.Value;
            }
        }

        Report(new Diagnostic.TypeDoesntHaveKey(key.Range, targetType, keyType));

        return Type.Unknown;
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
                VisitExpression(methodCall.CallExpr, false);
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

    private Type VisitExpression(Tree.Expression expr, bool isConstant)
    {
        switch (expr)
        {
            case Tree.Expression.Access access:
                return VisitExpression(access, isConstant);
            case Tree.Expression.Binary binary:
                return VisitExpression(binary, isConstant);
            case Tree.Expression.Call call:
                return VisitExpression(call, isConstant);
            case Tree.Expression.Error error:
                return VisitExpression(error, isConstant);
            case Tree.Expression.False @false:
                return VisitExpression(@false, isConstant);
            case Tree.Expression.Function function:
                return VisitExpression(function, isConstant);
            case Tree.Expression.MethodCall methodCall:
                return VisitExpression(methodCall, isConstant);
            case Tree.Expression.Name name:
                return VisitExpression(name, isConstant);
            case Tree.Expression.Nil nil:
                return VisitExpression(nil, isConstant);
            case Tree.Expression.Number number:
                return VisitExpression(number, isConstant);
            case Tree.Expression.String s:
                return VisitExpression(s, isConstant);
            case Tree.Expression.Table table:
                return VisitExpression(table, isConstant);
            case Tree.Expression.True @true:
                return VisitExpression(@true, isConstant);
            case Tree.Expression.Unary unary:
                return VisitExpression(unary, isConstant);
        }

        throw new ArgumentOutOfRangeException(nameof(expr));
    }

    private Type VisitType(Tree.Type type)
    {
        switch (type)
        {
            case Tree.Type.Function function:
                return VisitType(function);
            case Tree.Type.Name name:
                return VisitType(name);
            case Tree.Type.Table table:
                return VisitType(table);
            case Tree.Type.StringLiteral stringLiteral:
                return VisitType(stringLiteral);
            case Tree.Type.NumberLiteral numberLiteral:
                return VisitType(numberLiteral);
        }

        throw new ArgumentOutOfRangeException(nameof(type));
    }

    private void VisitStatement(Tree.Statement.Do block)
    {
        VisitBlock(block.Body);
    }

    private void VisitStatement(Tree.Statement.NumericalFor numericalFor)
    {
        var startType = VisitExpression(numericalFor.Start, false);
        if (!IsAssignableFrom(Type.NumberPrimitive, startType))
        {
            Report(new Diagnostic.ForLoopStartNotNumber(numericalFor.Start.Range, startType));
        }

        var limitType = VisitExpression(numericalFor.Limit, false);
        if (!IsAssignableFrom(Type.NumberPrimitive, limitType))
        {
            Report(new Diagnostic.ForLoopLimitNotNumber(numericalFor.Limit.Range, limitType));
        }

        if (numericalFor.Step != null)
        {
            var stepType = VisitExpression(numericalFor.Step, false);
            if (!IsAssignableFrom(Type.NumberPrimitive, stepType))
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

    private void VisitStatement(Tree.Statement.If ifStatement)
    {
        VisitExpression(ifStatement.Primary.Condition, false);
        VisitBlock(ifStatement.Primary.Body);

        foreach (var branch in ifStatement.ElseIfs)
        {
            VisitExpression(branch.Condition, false);
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

    private class DeclarationValueList(List<Tree.Declaration> declarations) : ITypeValueList
    {
        public ITypeValueList.TypeValue this[int index] =>
            new() { Value = index < declarations.Count ? declarations[index].Name : null };
    }

    private void VisitStatement(Tree.Statement.Assignment assignment)
    {
        CheckAssignment(new ExpressionListValueList(assignment.Targets),
            new ExpressionListValueList(assignment.Values), TypeList.TypeListKind.Value);
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
                targetType = VisitExpression(targetExpression, false);
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
                targetType = Dereference(targetType);
                if (targetType is Type.Infer infer)
                {
                    sourceType ??= sourceExpression != null ? VisitExpression(sourceExpression, false) : Type.Nil;
                    infer.OnInferred(sourceType);
                }
                else if (sourceType != null)
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
                VisitExpression(sourceExpression, false);
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
        if (inferReturn && function.Return == TypeList.Unknown)
        {
            // TODO make the function's return type a union of all possible returns, if they're different
            function.Return = VisitExpressionList(returnStatement.Values);
        }
        else
        {
            CheckAssignment(function.Return, new ExpressionListValueList(returnStatement.Values),
                TypeList.TypeListKind.Return,
                returnStatement.Range);
        }
    }

    private void VisitStatement(Tree.Statement.LocalFunctionDeclaration declaration)
    {
        var functionType = VisitFunction(declaration.Function, null);
        if (!source.TryGetTreeSymbol(declaration.Name, out var symbol))
        {
            throw new Exception();
        }

        source.SetSymbolType(symbol, functionType);
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
            VisitExpression(value, false);
            if (i < localDeclaration.Declarations.Count)
            {
                var declaration = localDeclaration.Declarations[i];
                if (declaration.Type != null)
                {
                    var targetType = evaluator.GetTypeOfTypeAnnotation(declaration.Type);
                    var sourceType = evaluator.GetTypeOfExpression(value, false);
                    CheckTypeToType(targetType, sourceType, declaration.Name.Range);
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

    private Type VisitExpression(Tree.Expression.Function function, bool isConstant)
    {
        return VisitFunction(function, null);
    }

    private Type VisitExpression(Tree.Expression.MethodCall methodCall, bool isConstant)
    {
        throw new NotImplementedException();
    }

    private Type VisitExpression(Tree.Expression.Call call, bool isConstant)
    {
        // TODO show warning if the call returns more than one value?
        return VisitCall(call)[0].Type ?? Type.Nil;
    }

    private Type VisitExpression(Tree.Expression.Access access, bool isConstant)
    {
        return GetAccessType(access.Target, access.Key, isConstant);
    }

    private Type VisitExpression(Tree.Expression.Binary binary, bool isConstant)
    {
        throw new NotImplementedException();
    }

    private Type VisitExpression(Tree.Expression.Unary unary, bool isConstant)
    {
        var exprType = VisitExpression(unary.Expression, isConstant);

        if (unary.Operator is Token.Not)
        {
            return Type.Boolean;
        }

        if (unary.Operator is Token.Length)
        {
            // TODO use __len metamethod
            if (!IsAssignableFrom(Type.TablePrimitive, exprType) &&
                !IsAssignableFrom(Type.StringPrimitive, exprType))
            {
                Report(new Diagnostic.CantGetLength(unary.Range, exprType));
            }

            return Type.NumberPrimitive;
        }

        if (unary.Operator is Token.Minus)
        {
            // TODO use __unm metamethod
            if (!IsAssignableFrom(Type.NumberPrimitive, exprType))
            {
                Report(new Diagnostic.CantNegate(unary.Range, exprType));
            }

            return Type.NumberPrimitive;
        }

        throw new Exception(); // Unreachable.
    }

    private Type VisitExpression(Tree.Expression.Name name, bool isConstant)
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

    private Type VisitExpression(Tree.Expression.Table table, bool isConstant)
    {
        List<Type.Table.Pair> pairs = [];

        foreach (var field in table.Fields)
        {
            pairs.Add(new Type.Table.Pair(
                VisitExpression(field.Key, true),
                // The value is visited as a non-constant — until we'll have const assertions like TypeScript's.
                VisitExpression(field.Value, false)));
        }

        return new Type.Table(pairs);
    }

    private Type VisitExpression(Tree.Expression.Number number, bool isConstant)
    {
        return isConstant ? new Type.NumberLiteral(number.NumberValue) : Type.NumberPrimitive;
    }

    private Type VisitExpression(Tree.Expression.String stringValue, bool isConstant)
    {
        return isConstant ? new Type.StringLiteral(stringValue.Value) : Type.StringPrimitive;
    }

    private Type VisitExpression(Tree.Expression.True trueValue, bool isConstant)
    {
        return isConstant ? Type.True : Type.Boolean;
    }

    private Type VisitExpression(Tree.Expression.False falseValue, bool isConstant)
    {
        return isConstant ? Type.False : Type.Boolean;
    }

    private Type VisitExpression(Tree.Expression.Nil nil, bool isConstant)
    {
        return Type.Nil;
    }

    private Type VisitExpression(Tree.Expression.Error error, bool isConstant)
    {
        return Type.Unknown;
    }

    private Checker(Source source)
    {
        this.source = source;
        evaluator = new TypeEvaluator(source);
    }

    private Type VisitType(Tree.Type.Name name)
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

            return new Type.Reference(symbol, name.Value);
        }

        // TODO report error?
        return Type.Unknown;
    }

    private Type VisitType(Tree.Type.Function functionType)
    {
        // TODO type parameters are stored in the function type, but currently we don't check them if they're not used
        // in a function value.

        // This code does a lot of the same things that VisitFunction does,
        // perhaps the shared parts could be merged somehow?
        List<Type> parameters = [];
        List<string> paramNames = [];
        foreach (var parameter in functionType.Parameters)
        {
            if (parameter.Type != null)
            {
                parameters.Add(VisitType(parameter.Type));
            }
            else
            {
                Report(new Diagnostic.ImplicitAnyType(parameter.Range, parameter.Name.Value));
            }

            paramNames.Add(parameter.Name.Value);
        }

        var parameterTypeList = new TypeList(parameters) { NameList = paramNames };
        // TODO handle rest parameter

        var returnTypeList = functionType.ReturnTypes != null ? VisitTypeList(functionType.ReturnTypes) : TypeList.None;

        return new Type.Function(parameterTypeList, returnTypeList, []);
    }

    private Type VisitType(Tree.Type.Table table)
    {
        List<Type.Table.Pair> pairs = [];

        foreach (var (key, value) in table.Pairs)
        {
            pairs.Add(new(VisitType(key), VisitType(value)));
        }

        return new Type.Table(pairs);
    }

    private Type VisitType(Tree.Type.StringLiteral stringLiteral)
    {
        return new Type.StringLiteral(stringLiteral.Value);
    }

    private Type VisitType(Tree.Type.NumberLiteral numberLiteral)
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
    private void CheckValueToType(Type targetType, Tree.Expression sourceValue, Tree.Expression? targetValue)
    {
        var errorRange = (targetValue ?? sourceValue).Range;

        targetType = Dereference(targetType);

        if (sourceValue is Tree.Expression.Table sourceTable && targetType is Type.Table targetTable)
        {
            // TODO use lookup
            var missingKeys = new HashSet<Type>(targetTable.Pairs.Select(p => p.Key));
            foreach (var sourceField in sourceTable.Fields)
            {
                var sourceKeyType = VisitExpression(sourceField.Key, IsSimpleLiteral(sourceField.Key));
                var targetKey = missingKeys.FirstOrDefault(targetKey => IsAssignableFrom(targetKey, sourceKeyType));
                if (targetKey == null)
                {
                    // TODO check for duplicate fields
                    Report(new Diagnostic.TableLiteralOnlyKnownKeys(sourceField.Key.Range, targetTable,
                        sourceKeyType));
                    VisitExpression(sourceField.Value, false);
                }
                else
                {
                    missingKeys.Remove(targetKey);
                    var targetPair = targetTable.Pairs.Find(p => p.Key == targetKey);
                    CheckValueToType(targetPair.Value, sourceField.Value, sourceField.Key);
                }
            }

            if (missingKeys.Count > 0)
            {
                Report(new Diagnostic.MissingKeys(errorRange, targetType,
                    VisitExpression(sourceValue, false),
                    missingKeys.ToList()));
            }
        }
        else if (sourceValue is Tree.Expression.Function sourceFunction &&
                 targetType is Type.Function targetFunction)
        {
            var sourceType = VisitFunction(sourceFunction, targetFunction);
            CheckTypeToType(targetType, sourceType, errorRange);
        }
        else
        {
            var valueType = VisitExpression(sourceValue, false);
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
            if (source.TryGetSymbolType(reference.Symbol, out var dereferenced))
            {
                return Dereference(dereferenced);
            }

            return Type.Unknown;
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

            reason = new TypeMismatch.Primitive(targetType, sourceType);
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

            reason = new TypeMismatch.Primitive(targetType, sourceType);
            return false;
        }

        if (targetType is Type.StringLiteral stringLiteral)
        {
            if (sourceType is Type.StringLiteral sourceLiteral &&
                stringLiteral.Literal == sourceLiteral.Literal)
            {
                return true;
            }

            reason = new TypeMismatch.Primitive(targetType, sourceType);
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

        reason = new TypeMismatch.Primitive(targetType, sourceType);
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

        foreach (var targetPair in targetTable.Pairs)
        {
            // TODO use lookup
            var sourcePair =
                sourceTable.Pairs.FirstOrDefault(sourcePair => IsAssignableFrom(targetPair.Key, sourcePair.Key));
            if (sourcePair.Key == null)
            {
                reasons.Add(new TypeMismatch.SourceMissingKey(targetTable, sourceTable, targetPair.Key));
                continue;
            }

            if (!IsAssignableFrom(targetPair.Value, sourcePair.Value, out var valueReason))
            {
                reasons.Add(new TypeMismatch.TableKeyIncompatible(targetPair.Key) { Children = [valueReason] });
            }
        }

        if (reasons.Count > 0)
        {
            reason = new TypeMismatch.Primitive(targetTable, sourceTable) { Children = reasons };
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
            reason = new TypeMismatch.Primitive(targetFunction, sourceFunction) { Children = reasons };
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

    public static List<Diagnostic> Check(Source source)
    {
        var checker = new Checker(source);
        checker.functionStack.Push(new(new Type.Function(TypeList.Any, TypeList.Any, []), false)); // TODO
        checker.VisitBlock(source.Tree);
        return checker.Diagnostics;
    }
}