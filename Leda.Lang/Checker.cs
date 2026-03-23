using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

public class Checker : Tree.IVisitor, Tree.IExpressionVisitor<Type>, Tree.ITypeVisitor<Type>
{
    private readonly Source source;
    public List<Diagnostic> Diagnostics { get; } = [];

    private record FunctionInfo(Type.Function Function, bool InferReturn);

    private readonly Stack<FunctionInfo> functionStack = [];

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
        var target = call.Target.AcceptExpressionVisitor(this, false);

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
        var targetParamIndex = 0;
        List<Type> parameters = [];
        List<string> paramNames = [];
        foreach (var parameter in function.Type.Parameters)
        {
            Type type;
            if (parameter.Type != null)
            {
                type = parameter.Type.AcceptTypeVisitor(this);
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

        var functionType = new Type.Function(parameterTypeList, returnTypeList ?? TypeList.Unknown);

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
            types.Add(typeTree.AcceptTypeVisitor(this));
        }

        return new TypeList(types);
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
            if (IsAssignableFrom(pair.Key, keyType))
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
        if (!IsAssignableFrom(Type.NumberPrimitive, startType))
        {
            Report(new Diagnostic.ForLoopStartNotNumber(numericalFor.Start.Range, startType));
        }

        var limitType = numericalFor.Limit.AcceptExpressionVisitor(this, false);
        if (!IsAssignableFrom(Type.NumberPrimitive, limitType))
        {
            Report(new Diagnostic.ForLoopLimitNotNumber(numericalFor.Limit.Range, limitType));
        }

        if (numericalFor.Step != null)
        {
            var stepType = numericalFor.Step.AcceptExpressionVisitor(this, false);
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

    public void Visit(Tree.Statement.Assignment assignment)
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
                targetType = targetExpression.AcceptExpressionVisitor(this, false);
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
                }

                if (sourceExpression is Tree.Expression.Vararg)
                {
                    throw new NotImplementedException();
                }
            }

            // TODO if target and source are typelists, control should probably be transferred to typelist's IsAssignableFrom

            if (targetType != null && targetType != Type.Unknown && targetType != Type.Any)
            {
                if (targetType is Type.Infer infer)
                {
                    sourceType ??= sourceExpression?.AcceptExpressionVisitor(this, false) ?? Type.Nil;
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
                sourceExpression.AcceptExpressionVisitor(this, false);
            }
            else
            {
                break;
            }

            targetIndex++;
            sourceIndex++;
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

            if (!source.TryGetTreeSymbol(declaration.Name, out var symbol))
            {
                throw new Exception("Variable doesn't have a symbol");
            }

            if (declaration.Type != null)
            {
                variableType = declaration.Type.AcceptTypeVisitor(this);
            }
            else
            {
                // The variable's type is inferred from the value.
                variableType = new Type.Infer(inferred => source.SetSymbolType(symbol, inferred));
            }

            source.SetSymbolType(symbol, variableType);
        }

        CheckAssignment(new DeclarationValueList(localDeclaration.Declarations),
            new ExpressionListValueList(localDeclaration.Values), TypeList.TypeListKind.Value);
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
        return VisitCall(call)[0].Type ?? Type.Nil;
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

        var returnTypeList = functionType.ReturnTypes != null ? VisitTypeList(functionType.ReturnTypes) : TypeList.None;

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
    private void CheckValueToType(Type targetType, Tree.Expression sourceValue, Tree.Expression? targetValue)
    {
        var errorRange = (targetValue ?? sourceValue).Range;

        if (sourceValue is Tree.Expression.Table sourceTable && targetType is Type.Table targetTable)
        {
            // TODO use lookup
            var missingKeys = new HashSet<Type>(targetTable.Pairs.Select(p => p.Key));
            foreach (var sourceField in sourceTable.Fields)
            {
                var sourceKeyType = sourceField.Key.AcceptExpressionVisitor(this, IsSimpleLiteral(sourceField.Key));
                var targetKey = missingKeys.FirstOrDefault(targetKey => IsAssignableFrom(targetKey, sourceKeyType));
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
                    CheckValueToType(targetPair.Value, sourceField.Value, sourceField.Key);
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
            CheckTypeToType(targetType, sourceType, errorRange);
        }
        else
        {
            var valueType = sourceValue.AcceptExpressionVisitor(this, false);
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

    private Type Dereference(Type type)
    {
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
        checker.functionStack.Push(new(new Type.Function(TypeList.Any, TypeList.Any), false)); // TODO
        checker.VisitBlock(source.Tree);
        return checker.Diagnostics;
    }
}