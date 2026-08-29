namespace Leda.Lang;

/// <summary>
/// Visits all nodes in the syntax tree, and uses the TypeEvaluator to get their types and to show relevant
/// type-related diagnostics.<br/>
/// Also associates string keys in tables with symbols.
/// </summary>
public class Checker(Project project, TypeEvaluator evaluator) : Visitor
{
    private List<Diagnostic> Diagnostics { get; } = [];

    private record FunctionInfo(Type.Function Function, bool InferReturn, Tree.Chunk Chunk);

    private readonly Stack<FunctionInfo> functionStack = [];

    /// <summary>
    /// Returns the name and range that a diagnostic should display for a certain value.
    /// </summary>
    private static (string?, Range) GetValueNameAndRange(Tree.Expression value)
    {
        return value switch
        {
            Tree.Expression.Name name => (name.Value, name.Range),
            Tree.Expression.String str => (str.Value, str.Range),
            Tree.Expression.Access access => GetValueNameAndRange(access.Key),
            _ => default,
        };
    }

    private void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Returns whether a variable is definitely assigned a value at the given FlowNode.
    /// </summary>
    private bool IsVariableAssignedAtFlowNode(Symbol.Variable variable, FlowNode? flowNode)
    {
        if (flowNode == null)
        {
            return true;
        }

        if (flowNode is FlowNode.Start ||
            // This function is called only if `variable.Uninitialized` is true in the first place,
            // so it's definitely not assigned if we reached its declaration.
            (flowNode is FlowNode.VariableDeclaration { Declaration: var declaration } &&
             declaration == variable.Declaration))
        {
            return false;
        }

        if (flowNode is FlowNode.Assignment { AssignmentStatement.Targets: var targets } assignment)
        {
            foreach (var target in targets)
            {
                if (project.GetTreeSymbol(target) == variable)
                {
                    return true;
                }
            }

            return IsVariableAssignedAtFlowNode(variable, assignment.Antecedent);
        }

        if (flowNode is FlowNode.Label { Antecedents: var antecedents })
        {
            foreach (var antecedent in antecedents)
            {
                if (!IsVariableAssignedAtFlowNode(variable, antecedent))
                {
                    return false;
                }
            }

            return true;
        }

        if (flowNode is FlowNode.Basic basic)
        {
            return IsVariableAssignedAtFlowNode(variable, basic.Antecedent);
        }

        return false;
    }

    protected override void Visit(Tree tree, Tree? parent, ChildKind childKind)
    {
        switch (tree)
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
            case Tree.Expression.Name name:
                if (parent is not Tree.Declaration)
                {
                    Visit(name, childKind);
                }

                break;
            case Tree.Expression.Table table:
                Visit(table);
                break;
            case Tree.Statement.Assignment assignment:
                Visit(assignment);
                break;
            case Tree.Statement.GlobalDeclaration globalDeclaration:
                Visit(globalDeclaration);
                Visit((Tree.Statement.VariableDeclaration)globalDeclaration);
                break;
            case Tree.Statement.LocalDeclaration localDeclaration:
                Visit(localDeclaration);
                break;
            case Tree.Statement.NumericalFor numericalFor:
                Visit(numericalFor);
                break;
            case Tree.Statement.Return @return:
                Visit(@return);
                break;
            case Tree.Type.Name typeName:
                Visit(typeName, childKind);
                break;
            case Tree.Type.Function functionType:
                if (parent is not Tree.Expression.Function)
                {
                    Visit(functionType);
                }

                break;
            case Tree.Type.Instantiation instantiation:
                Visit(instantiation);
                break;
        }
    }

    private void Visit(Tree.Expression.Call call)
    {
        var target = evaluator.GetTypeOfExpression(call.Target);

        if (target == Type.Unknown)
        {
            return;
        }

        // TODO we could store a simple flag for whether a type is callable instead of checking like this.
        if (!evaluator.IsAssignableFrom(Type.FunctionPrimitive, target)) // TODO handle __call metamethod
        {
            Report(new Diagnostic.TypeNotCallable(call.Target.Range));
            return;
        }

        if (target is Type.Function function)
        {
            // TODO support overloads
            var paramTypes = function.Parameters;
            if (function.IsGeneric)
            {
                paramTypes = evaluator.InstantiateTypeList(paramTypes, evaluator.GetGenericCallTypeMap(call, function));
            }

            CheckAssignment(paramTypes, call.Arguments, TypeListKind.Argument, call.Target.Range);
        }
    }

    private void Visit(Tree.Expression.Function function)
    {
        // TODO handle rest parameter

        if (function.Type.ReturnTypes != null)
        {
            // TODO report these only if the return type is not nillable
            if (function.Chunk.ReturnStatements.Count == 0)
            {
                Report(new Diagnostic.FunctionDoesntReturnValue(function.NameRange));
            }
            else if (!function.Chunk.AllPathsReturn)
            {
                Report(new Diagnostic.NotAllPathsReturn(function.NameRange));
            }
        }

        for (var i = 0; i < function.Type.Parameters.Count; i++)
        {
            var parameter = function.Type.Parameters[i];
            if (parameter.Type == null && evaluator.GetInferredParameterType(function, i) == null)
            {
                Report(new Diagnostic.ImplicitAnyType(parameter.Name.Range, parameter.Name.Value));
            }
        }

        functionStack.Push(new(evaluator.GetTypeOfFunction(function),
            function.Type.ReturnTypes == null,
            function.Chunk));
    }

    protected override void PostVisit(Tree tree)
    {
        if (tree is Tree.Expression.Function)
        {
            functionStack.Pop();
        }
    }

    private void Visit(Tree.Statement.NumericalFor numericalFor)
    {
        var startType = evaluator.GetTypeOfExpression(numericalFor.Start);
        if (!evaluator.IsAssignableFrom(Type.NumberPrimitive, startType))
        {
            Report(new Diagnostic.ForLoopStartNotNumber(numericalFor.Start.Range, evaluator.TypeToString(startType)));
        }

        var limitType = evaluator.GetTypeOfExpression(numericalFor.Limit);
        if (!evaluator.IsAssignableFrom(Type.NumberPrimitive, limitType))
        {
            Report(new Diagnostic.ForLoopLimitNotNumber(numericalFor.Limit.Range, evaluator.TypeToString(limitType)));
        }

        if (numericalFor.Step != null)
        {
            var stepType = evaluator.GetTypeOfExpression(numericalFor.Step);
            if (!evaluator.IsAssignableFrom(Type.NumberPrimitive, stepType))
            {
                Report(new Diagnostic.ForLoopStepNotNumber(numericalFor.Step.Range, evaluator.TypeToString(stepType)));
            }
        }
    }

    private void Visit(Tree.Statement.Assignment assignment)
    {
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
        var minimumValues = evaluator.GetTypeListMinimum(targets);
        var maximumValues = evaluator.GetTypeListMaximum(targets);
        var gotValues = evaluator.GetMinimumNumberOfValues(sources);
        if (gotValues < minimumValues)
        {
            Report(new Diagnostic.TypeMismatch(sideErrorRange,
                new TypeMismatch.NotEnoughValues(minimumValues, gotValues, kind, minimumValues == maximumValues)));
            return;
        }

        var targetsHaveRest = evaluator.DoesTypeListHaveRest(targets);

        for (var i = 0; i < sources.Count && (targetsHaveRest || i < maximumValues); i++)
        {
            var value = sources[i];
            // If the last expression is one that returns a TypeList (and that TypeList returns more than one value),
            // check it with `evaluator.IsAssignableFrom`.
            if (i == sources.Count - 1 && i < maximumValues - 1 &&
                evaluator.GetTypeListOfExpression(value) is { } sourceTypeList &&
                evaluator.GetTypeListMinimum(sourceTypeList) > 1)
            {
                if (!evaluator.IsAssignableFrom(targets, sourceTypeList, out var reasons, TypeListKind.Return, i))
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

        if (!targetsHaveRest && sources.Count > maximumValues)
        {
            var firstExcessive = sources[maximumValues];
            var lastExcessive = sources[^1];
            Report(new Diagnostic.TooManyValues(firstExcessive.Range.Union(lastExcessive.Range), maximumValues,
                sources.Count, kind, minimumValues == maximumValues));
        }
    }

    private void Visit(Tree.Statement.Return returnStatement)
    {
        var (function, inferReturn, _) = functionStack.Peek();
        if (!inferReturn)
        {
            CheckAssignment(function.Returns, returnStatement.Values, TypeListKind.Return, returnStatement.Range);
        }
    }

    private void Visit(Tree.Statement.GlobalDeclaration globalDeclaration)
    {
        foreach (var declaration in globalDeclaration.Declarations)
        {
            if (declaration.Name.LocalBinding is Symbol.GlobalVariable declared &&
                project.GetGlobalVariable(declaration.Name.Value) is { } existing &&
                declared != existing)
            {
                Report(new Diagnostic.GlobalAlreadyDeclared(declaration.Name.Range, declaration.Name.Value));
            }
        }
    }

    private void Visit(Tree.Statement.VariableDeclaration variableDeclaration)
    {
        // This runs for both local and global variable declarations.

        for (var i = 0; i < variableDeclaration.Values.Count; i++)
        {
            var value = variableDeclaration.Values[i];

            if (i >= variableDeclaration.Declarations.Count)
            {
                Report(new Diagnostic.ValueNotAssigned(value.Range));
            }
        }

        // For each variable with a type annotation and a value assigned to it, we check the value with the type.
        for (var i = 0; i < variableDeclaration.Declarations.Count; i++)
        {
            var declaration = variableDeclaration.Declarations[i];
            if (declaration.Type != null)
            {
                var targetType = evaluator.GetTypeOfTypeAnnotation(declaration.Type);
                if (i < variableDeclaration.Values.Count)
                {
                    var value = variableDeclaration.Values[i];
                    CheckValueToType(targetType, value, declaration.Name);
                }
                else if (variableDeclaration.Values.Count >= 1 &&
                         evaluator.GetTypeListOfExpression(variableDeclaration.Values[^1]) is { } typeList)
                {
                    CheckTypeToType(targetType,
                        evaluator.GetTypeInTypeList(typeList, i - variableDeclaration.Values.Count + 1),
                        declaration.Name.Range);
                }
            }
        }
    }

    private void Visit(Tree.Expression.Access access)
    {
        var possiblyNil = false;
        var targetType = evaluator.GetTypeOfExpression(access.Target);

        if (targetType is Type.Nillable { Inner: var inner })
        {
            possiblyNil = true;
            targetType = inner;
        }

        if (targetType is not (Type.Table or Type.Array))
        {
            if (targetType != Type.Unknown && targetType != Type.Any)
            {
                Report(new Diagnostic.TypeNotIndexable(access.Key.Range, evaluator.TypeToString(targetType)));
            }

            return;
        }

        if (possiblyNil)
        {
            var (name, range) = GetValueNameAndRange(access.Target);
            Report(new Diagnostic.ValuePossiblyNil(range, name != null ? "'" + name + "'" : null));
        }


        if (evaluator.GetTypeOfAccess(access) is (not null, var symbol))
        {
            if (symbol != null && access.Key is Tree.Expression.String)
            {
                project.AttachNonLocalSymbol(access.Key, symbol);
            }
        }
        else
        {
            Report(new Diagnostic.TypeDoesntHaveKey(access.Key.Range, evaluator.TypeToString(targetType),
                evaluator.TypeToString(evaluator.GetTypeOfExpression(access.Key, true))));
        }
    }

    private void Visit(Tree.Expression.Binary binary)
    {
        if (binary.Operator.Kind is not (TokenKind.And or TokenKind.Or) &&
            evaluator.GetTypeOfBinaryExpression(binary) == null)
        {
            Report(new Diagnostic.BinaryOperatorCantBeUsed(binary.Range,
                binary.Operator.Kind,
                evaluator.TypeToString(evaluator.GetTypeOfExpression(binary.Left)),
                evaluator.TypeToString(evaluator.GetTypeOfExpression(binary.Right))));
        }
    }

    private void Visit(Tree.Expression.Table table)
    {
        foreach (var field in table.Fields)
        {
            if (field.Symbol != null && project.GetTreeSymbol(field.Key) == null)
            {
                // If this table is the origin of an inferred table type, this field will be its symbol's definition.
                // But, CheckValueToType can overwrite it if it references some other field.
                project.AttachNonLocalSymbol(field.Key, field.Symbol);
            }
        }
    }

    private void Visit(Tree.Expression.Name name, ChildKind childKind)
    {
        var symbol = evaluator.GetNameSymbol(name);

        if (symbol == null)
        {
            Report(new Diagnostic.NameNotFound(name.Range, name.Value, Tree.NameContext.Value));
        }
        else if (childKind != ChildKind.AssignmentTarget &&
                 symbol is Symbol.Variable variable &&
                 variable.Uninitialized &&
                 variable.Chunk == functionStack.Peek().Chunk &&
                 !IsVariableAssignedAtFlowNode(variable, name.FlowNode))
        {
            Report(new Diagnostic.VariableUsedBeforeAssignment(name.Range, name.Value));
        }
    }

    private void Visit(Tree.Type.Name name, ChildKind childKind)
    {
        if (childKind == ChildKind.TypeAnnotation)
        {
            if (project.GetTreeSymbol(name) is Symbol.TypeAlias typeAlias &&
                typeAlias.Declaration.TypeParameters.Count > 0)
            {
                Report(new Diagnostic.GenericTypeRequiresTypeArguments(name.Range, typeAlias.Name, typeAlias.Declaration.TypeParameters.Count));
            }
        }
    }

    private void Visit(Tree.Type.Function functionType)
    {
        foreach (var parameter in functionType.Parameters)
        {
            if (parameter.Type == null)
            {
                Report(new Diagnostic.ImplicitAnyType(parameter.Range, parameter.Name.Value));
            }
        }
    }

    private void Visit(Tree.Type.Instantiation instantiation)
    {
        if (project.GetTreeSymbol(instantiation.Name) is {} symbol)
        {
            if (symbol is not Symbol.TypeAlias typeAlias || typeAlias.Declaration.TypeParameters.Count == 0)
            {
                Report(new Diagnostic.NotAGenericType(instantiation.Range, instantiation.Name.Value));
            }
            else if (instantiation.TypeArguments.Count != typeAlias.Declaration.TypeParameters.Count)
            {
                Report(new Diagnostic.GenericTypeRequiresTypeArguments(instantiation.Range, instantiation.Name.Value, typeAlias.Declaration.TypeParameters.Count));
            }
        }
    }

    /// <summary>
    /// Checks an assignment of a single value to a target type.
    /// If the source value is a table, errors will be shown in its keys and values, when applicable.
    /// </summary>
    /// <param name="targetType">The type of the target being assigned to.</param>
    /// <param name="sourceValue">The value being assigned.</param>
    /// <param name="targetValue">The tree node for the target value, if applicable.</param>
    private void CheckValueToType(Type targetType, Tree.Expression sourceValue, Tree.Expression? targetValue)
    {
        var errorRange = (targetValue ?? sourceValue).Range;

        // We only comprehensively check table values against types that can be tables (Table or Array).
        if (sourceValue is not Tree.Expression.Table sourceTable || targetType is not (Type.Table or Type.Array))
        {
            CheckTypeToType(targetType, evaluator.GetTypeOfExpression(sourceValue), errorRange);
            return;
        }

        // TODO check number literals too
        foreach (var sourceField in sourceTable.Fields)
        {
            var sourceKeyType = evaluator.GetTypeOfExpression(sourceField.Key, true);
            var (targetValueType, targetSymbol) = evaluator.GetTypeOfAccessToType(targetType, sourceKeyType);

            if (targetSymbol != null && sourceField.Key is Tree.Expression.String)
            {
                project.AttachNonLocalSymbol(sourceField.Key, targetSymbol);
            }

            if (targetValueType == null)
            {
                Report(new Diagnostic.TableLiteralOnlyKnownKeys(sourceField.Key.Range,
                    evaluator.TypeToString(targetType),
                    evaluator.TypeToString(sourceKeyType)));
            }
            else
            {
                // TODO check for duplicate fields
                CheckValueToType(targetValueType, sourceField.Value, sourceField.Key);
            }
        }

        if (targetType is Type.Table targetTable &&
            evaluator.GetTypeOfTableValue(sourceTable) is Type.Table sourceTableType &&
            !evaluator.DoesTableHaveAllTargetKeys(targetTable, sourceTableType, out var reason))
        {
            Report(new Diagnostic.TypeMismatch(errorRange, reason));
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
        if (!evaluator.IsAssignableFrom(targetType, sourceType, out var reason))
        {
            Report(new Diagnostic.TypeMismatch(errorRange, reason));
        }
    }

    public static List<Diagnostic> Check(Project project, Source source, TypeEvaluator evaluator)
    {
        var checker = new Checker(project, evaluator);
        checker.functionStack.Push(new(new Type.Function(TypeList.Any, TypeList.Any, []), false, source.File));
        checker.Start(source);
        return checker.Diagnostics;
    }
}