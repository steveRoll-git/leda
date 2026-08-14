using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Leda.Lang;

/*
The evaluator's job is to tell the Checker what type a certain node is.
Based on that, the Checker decides whether to show diagnostics on a particular node.
The evaluator strives to be as lazy as possible - for any query, it will respond only with the information needed for
that specific query.
*/

/// <summary>
/// Evaluates the types of tree nodes.
/// </summary>
public class TypeEvaluator(Project project)
{
    private readonly Dictionary<Symbol, Type> typeOfSymbolCache = [];
    private readonly Dictionary<Tree.Expression.Table, Type> inferredTableCache = [];
    private readonly Dictionary<Tree.Type.Table, Type.Table> tableAnnotationCache = [];
    private readonly Dictionary<Tree.Expression.Function, Type.Function> functionTypeCache = [];
    private readonly Dictionary<Tree.Type.Function, Type.Function> functionAnnotationCache = [];
    private readonly Dictionary<Symbol.TypeAlias, Type> typeAliasCache = [];
    private readonly Dictionary<Type, Type.Nillable> nillableTypeCache = [];
    private readonly Dictionary<Type, Type.Array> arrayTypeCache = [];

    /// <summary>
    /// Returns whether a type may need to be narrowed using FlowNodes.
    /// </summary>
    private static bool IsTypeNarrowable(Type type)
    {
        return type is Type.Nillable;
    }

    private static bool IsExpressionNarrowable(Tree.Expression expression)
    {
        if (expression is Tree.Expression.Name)
        {
            return true;
        }

        if (expression is Tree.Expression.Access { Target: var target, Key: var key })
        {
            return IsExpressionNarrowable(target) && key is Tree.Expression.String;
        }

        return false;
    }

    /// <summary>
    /// Returns whether two narrowable expressions are equal.
    /// </summary>
    private bool AreNarrowableExpressionsEqual(Tree.Expression a, Tree.Expression b)
    {
        if (a is Tree.Expression.Name && b is Tree.Expression.Name)
        {
            return project.GetTreeSymbol(a) == project.GetTreeSymbol(b);
        }

        if (a is Tree.Expression.String { Value: var stringA } &&
            b is Tree.Expression.String { Value: var stringB })
        {
            return stringA == stringB;
        }

        if (a is Tree.Expression.Access { Target: var targetA, Key: var keyA } &&
            b is Tree.Expression.Access { Target: var targetB, Key: var keyB })
        {
            return AreNarrowableExpressionsEqual(targetA, targetB) && AreNarrowableExpressionsEqual(keyA, keyB);
        }

        return false;
    }

    private static Type NarrowTypeByTruthiness(Type type, bool isTrue)
    {
        if (type is Type.Nillable { Inner: var inner })
        {
            return isTrue ? inner : Type.Nil;
        }

        return type;
    }

    private Type NarrowTypeByCondition(Tree.Expression expression, Type type, FlowNode.Condition condition)
    {
        var sourceExpression = condition.Expression;

        if (AreNarrowableExpressionsEqual(expression, sourceExpression))
        {
            return NarrowTypeByTruthiness(type, condition.IsTrue);
        }

        return type;
    }

    private Type GetTypeOfExpressionAtFlowNode(Tree.Expression expression, Type declaredType, FlowNode flowNode)
    {
        if (flowNode is FlowNode.Start)
        {
            return declaredType;
        }

        if (flowNode is FlowNode.Label { Antecedents: var antecedents })
        {
            Type? result = null;
            // A union will be constructed here.
            foreach (var antecedent in antecedents)
            {
                var current = GetTypeOfExpressionAtFlowNode(expression, declaredType, antecedent);
                if (result == null || current is Type.Nillable)
                {
                    result = current;
                }
                else if (current == Type.Nil && declaredType is Type.Nillable)
                {
                    // If one of the branches gives us `nil` and the declared type is nillable, set it back to the
                    // declared type.
                    result = declaredType;
                }
            }

            return result ?? declaredType;
        }

        var basic = (flowNode as FlowNode.Basic)!;
        var previous = GetTypeOfExpressionAtFlowNode(expression, declaredType, basic.Antecedent);

        if (flowNode is FlowNode.Condition condition)
        {
            return NarrowTypeByCondition(expression, previous, condition);
        }

        return previous;
    }

    public Type GetTypeOfExpression(Tree.Expression expression, bool isConstant = false)
    {
        var result = expression switch
        {
            Tree.Expression.Name name => GetTypeOfVariable(name),
            Tree.Expression.Number number => isConstant
                ? new Type.NumberLiteral(number.NumberValue)
                : Type.NumberPrimitive,
            Tree.Expression.String s => isConstant ? new Type.StringLiteral(s.Value) : Type.StringPrimitive,
            Tree.Expression.Function function => GetTypeOfFunction(function),
            Tree.Expression.Table table => GetTypeOfTableValue(table),
            Tree.Expression.Access access => GetTypeOfAccess(access) ?? Type.Unknown,
            Tree.Expression.Call call => GetTypeInTypeList(GetTypeListOfCall(call), 0),
            Tree.Expression.Binary binary => GetTypeOfBinaryExpression(binary) ?? Type.Unknown,
            Tree.Expression.False => isConstant ? Type.False : Type.Boolean,
            Tree.Expression.True => isConstant ? Type.True : Type.Boolean,
            Tree.Expression.Nil => Type.Nil,
            Tree.Expression.Error => Type.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(expression)),
        };

        if (IsExpressionNarrowable(expression) && IsTypeNarrowable(result) && expression.FlowNode != null)
        {
            return GetTypeOfExpressionAtFlowNode(expression, result, expression.FlowNode);
        }

        return result;
    }

    private static TypeList CreateTypeListFromValues(List<Tree.Expression> values)
    {
        if (values.Count > 0)
        {
            return new TypeList.FromValues(values);
        }

        return TypeList.Empty;
    }

    private Type.Function GetTypeOfFunctionUncached(Tree.Expression.Function function)
    {
        var parameters = new TypeList.Parameters(function);

        TypeList returns;
        if (function.Type.ReturnTypes != null)
        {
            returns = new TypeList.FromTypes(function.Type.ReturnTypes);
        }
        else if (function.Chunk.ReturnStatements.Count > 0)
        {
            // TODO make union of all return statements
            returns = CreateTypeListFromValues(function.Chunk.ReturnStatements[0].Values);
        }
        else
        {
            returns = TypeList.Empty;
        }

        var typeParameters = new List<Type.TypeParameter>();

        if (function.Type.TypeParameters != null)
        {
            foreach (var typeParameter in function.Type.TypeParameters)
            {
                typeParameters.Add((project.GetTreeSymbol(typeParameter) as Symbol.TypeParameter)!.Type);
            }
        }

        return new Type.Function(parameters, returns, typeParameters);
    }

    internal Type.Function GetTypeOfFunction(Tree.Expression.Function function)
    {
        return GetQueryOrCached(GetTypeOfFunctionUncached, function, functionTypeCache);
    }

    private Type GetTypeOfTableValueUncached(Tree.Expression.Table table)
    {
        if (table.IsArray)
        {
            // TODO the element type needs to be a union of all the elements in the table
            return CreateArrayType(GetTypeOfExpression(table.Fields[0].Value));
        }

        var type = new Type.Table();
        foreach (var field in table.Fields)
        {
            if (GetTypeOfExpression(field.Key, true) is Type.StringLiteral { Literal: var literal })
            {
                var newKey = new Type.Table.ValueStringField(field.Symbol, field);
                type.StringLiterals[literal] = newKey;
            }
        }

        return type;
    }

    private Type GetTypeOfTableValue(Tree.Expression.Table table)
    {
        return GetQueryOrCached(GetTypeOfTableValueUncached, table, inferredTableCache);
    }

    private Type.Table GetTypeOfTableAnnotationUncached(Tree.Type.Table table)
    {
        var type = new Type.Table();
        foreach (var field in table.Fields)
        {
            if (GetTypeOfTypeAnnotation(field.Key) is Type.StringLiteral { Literal: var literal })
            {
                var newKey = new Type.Table.TypeStringField(field.Symbol, field);
                type.StringLiterals[literal] = newKey;
            }
        }

        return type;
    }

    private Type.Table GetTypeOfTableAnnotation(Tree.Type.Table table)
    {
        return GetQueryOrCached(GetTypeOfTableAnnotationUncached, table, tableAnnotationCache);
    }

    internal Type GetTypeOfStringField(Type.Table table, Type.Table.StringField stringField)
    {
        if (stringField.CachedType == null)
        {
            if (stringField is Type.Table.ValueStringField valueStringField)
            {
                stringField.CachedType = GetTypeOfExpression(valueStringField.Field.Value);
            }
            else if (stringField is Type.Table.TypeStringField typeStringField)
            {
                stringField.CachedType = GetTypeOfTypeAnnotation(typeStringField.Field.Value);
            }
        }

        if (table.TypeMap != null)
        {
            return InstantiateType(stringField.CachedType!, table.TypeMap);
        }

        return stringField.CachedType!;
    }

    public Type? GetTypeOfStringFieldInTable(Type.Table table, string key)
    {
        if (!table.StringLiterals.TryGetValue(key, out var field))
        {
            return null;
        }

        return GetTypeOfStringField(table, field);
    }

    private Type? GetTypeOfTableAccess(Type.Table table, Tree.Expression key)
    {
        var keyType = GetTypeOfExpression(key, true);
        if (keyType is Type.StringLiteral stringLiteral)
        {
            return GetTypeOfStringFieldInTable(table, stringLiteral.Literal);
        }

        // TODO check number literals, indexers
        return null;
    }

    private Type? GetTypeOfArrayAccess(Type.Array array, Tree.Expression key)
    {
        if (IsAssignableFrom(Type.NumberPrimitive, GetTypeOfExpression(key)))
        {
            return array.ElementType;
        }

        return null;
    }

    internal Type? GetTypeOfAccess(Tree.Expression.Access access)
    {
        var targetType = GetTypeOfExpression(access.Target);

        if (targetType is Type.Nillable { Inner: var inner })
        {
            targetType = inner;
        }

        if (targetType == Type.Any)
        {
            return Type.Any;
        }

        if (targetType is Type.Table table)
        {
            return GetTypeOfTableAccess(table, access.Key);
        }

        if (targetType is Type.Array array)
        {
            return GetTypeOfArrayAccess(array, access.Key);
        }

        return Type.Unknown;
    }

    /// <summary>
    /// Gets a string field in a value, whose type may not necessarily be a table.
    /// (For example, other types with a `__index`.)
    /// </summary>
    internal static Type.Table.StringField? GetStringFieldInType(Type type, string key)
    {
        if (type is Type.Table table)
        {
            return table.StringLiterals.GetValueOrDefault(key);
        }

        return null;
    }

    private static Type.Function GetTypeOfFunctionAnnotationUncached(Tree.Type.Function function)
    {
        return new Type.Function(new TypeList.FromDeclarations(function.Parameters),
            function.ReturnTypes != null ? new TypeList.FromTypes(function.ReturnTypes) : TypeList.Empty, []);
    }

    private Type.Function GetTypeOfFunctionAnnotation(Tree.Type.Function function)
    {
        return GetQueryOrCached(GetTypeOfFunctionAnnotationUncached, function, functionAnnotationCache);
    }

    private Type GetTypeOfVariable(Tree.Statement.VariableDeclaration declaration, int index)
    {
        if (index < declaration.Declarations.Count && declaration.Declarations[index].Type is { } typeAnnotation)
        {
            return GetTypeOfTypeAnnotation(typeAnnotation);
        }

        return GetTypeOfExpressionInList(declaration.Values, index);
    }

    public Type GetTypeAtValueLocation(ValueLocation valueLocation)
    {
        return valueLocation switch
        {
            ValueLocation.Variable { VariableDeclaration: var declaration, Index: var varIndex } =>
                GetTypeOfVariable(declaration, varIndex),
            ValueLocation.AssignmentValue { Assignment.Targets: var targets, Index: var assignIndex }
                when assignIndex < targets.Count =>
                GetTypeOfExpression(targets[assignIndex]),
            ValueLocation.Argument { Call.Target: var callee, Index: var argIndex }
                when GetTypeOfExpression(callee) is Type.Function function =>
                GetTypeInTypeList(function.Parameters, argIndex),
            ValueLocation.ReturnValue { Return: var returnStmt, Index: var returnIndex }
                when returnStmt.ParentChunk.ParentFunction is { } function =>
                GetTypeInTypeList(GetTypeOfFunction(function).Returns, returnIndex),
            ValueLocation.TableField { Field: var field, Parent: var parent }
                when GetTypeAtValueLocation(parent) is Type.Table parentTable &&
                     GetTypeOfTableAccess(parentTable, field.Key) is { } keyType =>
                keyType,
            _ => Type.Unknown,
        };
    }

    // TODO this probably needs to be cached
    internal Type? GetInferredParameterType(Tree.Expression.Function function, int index)
    {
        if (function.ValueLocation != null &&
            GetTypeAtValueLocation(function.ValueLocation) is Type.Function targetFunction &&
            !(targetFunction.Parameters is TypeList.Parameters { Function: var other } && other == function))
        {
            return GetTypeInTypeList(targetFunction.Parameters, index);
        }

        return null;
    }

    private Type GetTypeOfParameter(Tree.Expression.Function function, int index)
    {
        if (index >= function.Type.Parameters.Count)
        {
            // TODO check rest
            return Type.Unknown;
        }

        var declaration = function.Type.Parameters[index];

        if (declaration.Type != null)
        {
            return GetTypeOfTypeAnnotation(declaration.Type);
        }

        return GetInferredParameterType(function, index) ?? Type.Any;
    }

    private Type GetTypeOfSymbolUncached(Symbol symbol)
    {
        return symbol switch
        {
            Symbol.Variable variable => GetTypeOfVariable(variable.Declaration, variable.Index),
            Symbol.LocalFunction localFunction => GetTypeOfFunction(localFunction.Declaration.Function),
            Symbol.Parameter parameter => GetTypeOfParameter(parameter.Function, parameter.Index),
            Symbol.NumericForCounter => Type.NumberPrimitive,
            Symbol.IntrinsicType intrinsicType => intrinsicType.Type,
            Symbol.TypeAlias typeAlias => GetTypeOfTypeAlias(typeAlias),
            Symbol.TypeParameter typeParameter => typeParameter.Type,
            _ => Type.Unknown,
        };
    }

    public Type GetTypeOfSymbol(Symbol symbol)
    {
        return GetQueryOrCached(GetTypeOfSymbolUncached, symbol, typeOfSymbolCache);
    }

    /// <summary>
    /// Returns the symbol that this name refers to.
    /// If it's not bound to any local symbol, attempts to attach and return a global variable with that name.
    /// </summary>
    internal Symbol? GetNameSymbol(Tree.Expression.Name name)
    {
        if (project.GetTreeSymbol(name) is { } symbol)
        {
            return symbol;
        }

        if (project.GetGlobalVariable(name.Value) is { } globalVariable)
        {
            project.AttachNonLocalSymbol(name, globalVariable);
            return globalVariable;
        }

        return null;
    }

    public Type GetTypeOfVariable(Tree.Expression.Name name)
    {
        if (GetNameSymbol(name) is { } symbol)
        {
            return GetTypeOfSymbol(symbol);
        }

        return Type.Unknown;
    }

    private Type GetTypeOfExpressionInList(List<Tree.Expression> expressions, int index)
    {
        if (index < expressions.Count)
        {
            return GetTypeOfExpression(expressions[index]);
        }

        if (expressions.Count >= 1)
        {
            var last = expressions[^1];
            if (last is Tree.Expression.Call call)
            {
                return GetTypeInTypeList(GetTypeListOfCall(call), index - expressions.Count + 1);
            }
        }

        return Type.Nil;
    }

    internal Type? GetTypeOfBinaryExpression(Tree.Expression.Binary binary)
    {
        var left = GetTypeOfExpression(binary.Left);
        var right = GetTypeOfExpression(binary.Right);

        // Operator metamethods should be handled here.

        switch (binary.Operator.Kind)
        {
            case TokenKind.Plus or TokenKind.Minus or TokenKind.Multiply or TokenKind.Divide or TokenKind.Modulo
                or TokenKind.Power:
            {
                if (IsAssignableFrom(Type.NumberPrimitive, left) && IsAssignableFrom(Type.NumberPrimitive, right))
                {
                    return Type.NumberPrimitive;
                }

                break;
            }

            case TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual:
            {
                if ((IsAssignableFrom(Type.NumberPrimitive, left) && IsAssignableFrom(Type.NumberPrimitive, right)) ||
                    (IsAssignableFrom(Type.StringPrimitive, left) && IsAssignableFrom(Type.StringPrimitive, right)))
                {
                    return Type.Boolean;
                }

                break;
            }

            case TokenKind.Equal or TokenKind.NotEqual:
                return Type.Boolean;

            case TokenKind.Concat:
            {
                if ((IsAssignableFrom(Type.NumberPrimitive, left) || IsAssignableFrom(Type.StringPrimitive, left)) &&
                    (IsAssignableFrom(Type.NumberPrimitive, right) || IsAssignableFrom(Type.StringPrimitive, right)))
                {
                    return Type.StringPrimitive;
                }

                break;
            }
        }

        return null;
    }

    private Type GetTypeOfTypeAliasUncached(Symbol.TypeAlias typeAlias)
    {
        var type = GetTypeOfTypeAnnotation(typeAlias.Declaration.Type);
        if (typeAlias.Declaration.Type is Tree.Type.Table)
        {
            type.Name = typeAlias.Declaration.Name.Value;
        }

        return type;
    }

    private Type GetTypeOfTypeAlias(Symbol.TypeAlias typeAlias)
    {
        return GetQueryOrCached(GetTypeOfTypeAliasUncached, typeAlias, typeAliasCache);
    }

    private Type GetTypeOfTypeName(Tree.Type.Name name)
    {
        if (project.GetTreeSymbol(name) is { } symbol)
        {
            return GetTypeOfSymbol(symbol);
        }

        return Type.Unknown;
    }

    private static Type.Nillable CreateNillableTypeUncached(Type inner)
    {
        return new Type.Nillable(inner);
    }

    private Type.Nillable CreateNillableType(Type inner)
    {
        if (inner is Type.Nillable nillable)
        {
            return nillable;
        }

        return GetQueryOrCached(CreateNillableTypeUncached, inner, nillableTypeCache);
    }

    private static Type.Array CreateArrayTypeUncached(Type elementType)
    {
        return new Type.Array(elementType);
    }

    private Type.Array CreateArrayType(Type elementType)
    {
        return GetQueryOrCached(CreateArrayTypeUncached, elementType, arrayTypeCache);
    }

    public Type GetTypeOfTypeAnnotation(Tree.Type typeAnnotation)
    {
        return typeAnnotation switch
        {
            Tree.Type.StringLiteral stringLiteral => new Type.StringLiteral(stringLiteral.Value),
            Tree.Type.NumberLiteral numberLiteral => new Type.NumberLiteral(numberLiteral.Value),
            Tree.Type.Name name => GetTypeOfTypeName(name),
            Tree.Type.Table table => GetTypeOfTableAnnotation(table),
            Tree.Type.Function function => GetTypeOfFunctionAnnotation(function),
            Tree.Type.Nillable { Inner: var inner } => CreateNillableType(GetTypeOfTypeAnnotation(inner)),
            Tree.Type.Array { ElementType: var elementType } => CreateArrayType(GetTypeOfTypeAnnotation(elementType)),
            _ => Type.Unknown,
        };
    }

    /// <summary>
    /// Instantiates a generic type with the given TypeMap.
    /// </summary>
    /// <param name="type">The generic type to instantiate.</param>
    /// <param name="map">The type arguments.</param>
    private Type InstantiateType(Type type, TypeMap map)
    {
        if (type is Type.TypeParameter typeParameter && map.TryGetValue(typeParameter, out var mappedType))
        {
            return mappedType;
        }

        // TODO check whether the type contains type parameters at all
        if (type is Type.Table original)
        {
            return new Type.Table(original) { TypeMap = map };
        }

        if (type is Type.Function function)
        {
            // This will not instantiate the function's own type parameters if it has any.
            return new Type.Function(InstantiateTypeList(function.Parameters, map),
                InstantiateTypeList(function.Returns, map),
                function.TypeParameters);
        }

        return type;
    }

    /// <summary>
    /// Returns the effective minimum number of values produced by this expression list, including trailing values.
    /// </summary>
    internal int GetMinimumNumberOfValues(List<Tree.Expression> expressions)
    {
        return expressions.Count + (expressions.Count >= 1 && GetTypeListOfExpression(expressions[^1]) is { } typeList
            ? GetTypeListMinimum(typeList) - 1
            : 0);
    }

    /// <summary>
    /// Returns the minimum number of elements in this TypeList.
    /// </summary>
    internal int GetTypeListMinimum(TypeList typeList)
    {
        // TODO consider nillable types and rest
        return typeList.Count;
    }

    /// <summary>
    /// Returns the maximum number of elements in this TypeList.<br/>
    /// (Only relevant if the TypeList does not have a repeating `rest` type.)
    /// </summary>
    internal int GetTypeListMaximum(TypeList typeList)
    {
        // TODO consider rest
        return typeList.Count;
    }

    /// <summary>
    /// Returns whether a TypeList has a repeating `rest` type.
    /// </summary>
    internal bool DoesTypeListHaveRest(TypeList typeList)
    {
        if (typeList is TypeList.Builtin)
        {
            return true;
        }

        // TODO
        return false;
    }

    internal Type GetTypeInTypeList(TypeList typeList, int index)
    {
        if (typeList == TypeList.Empty)
        {
            return Type.Nil;
        }

        if (typeList == TypeList.Any)
        {
            return Type.Any;
        }

        if (typeList == TypeList.Unknown)
        {
            return Type.Unknown;
        }

        switch (typeList)
        {
            case TypeList.Parameters { Function: var function }:
                return GetTypeOfParameter(function, index);

            case TypeList.FromTypes { Types: var types }:
                // TODO check rest
                if (index < types.Count)
                {
                    return GetTypeOfTypeAnnotation(types[index]);
                }

                return Type.Nil;

            case TypeList.FromValues { Values: var values }:
                // TODO check rest
                return GetTypeOfExpressionInList(values, index);

            case TypeList.AssignmentTargets { Targets: var targets }:
                // TODO check rest
                return GetTypeOfExpressionInList(targets, index);

            case TypeList.FromDeclarations { Declarations: var declarations }:
                if (index < declarations.Count && declarations[index].Type is { } declarationType)
                {
                    return GetTypeOfTypeAnnotation(declarationType);
                }

                return Type.Nil;

            case TypeList.Instantiation { Inner: var inner, Map: var map }:
                return InstantiateType(GetTypeInTypeList(inner, index), map);

            default:
                return Type.Unknown;
        }
    }

    private readonly Dictionary<Tree.Expression.Call, TypeMap> genericCallTypeMapCache = [];

    /// <summary>
    /// Returns the TypeMap of a generic call - either through explicitly given type arguments, or by inferring them.
    /// </summary>
    internal TypeMap GetGenericCallTypeMap(Tree.Expression.Call call, Type.Function callee)
    {
        if (genericCallTypeMapCache.TryGetValue(call, out var cached))
        {
            return cached;
        }

        TypeMap typeMap;
        if (call.TypeArguments != null)
        {
            typeMap = new();
            for (var i = 0; i < Math.Min(callee.TypeParameters.Count, call.TypeArguments.Count); i++)
            {
                typeMap[callee.TypeParameters[i]] = GetTypeOfTypeAnnotation(call.TypeArguments[i]);
            }
        }
        else
        {
            typeMap = InferCallTypeParameters(call, callee);
        }

        genericCallTypeMapCache.Add(call, typeMap);

        return typeMap;
    }

    private TypeMap InferCallTypeParameters(Tree.Expression.Call call, Type.Function callee)
    {
        var typeMap = new TypeMap();
        for (var i = 0; i < callee.Parameters.Count; i++)
        {
            if (GetTypeInTypeList(callee.Parameters, i) is Type.TypeParameter typeParameter &&
                callee.TypeParameters.Contains(typeParameter) &&
                GetTypeOfExpressionInList(call.Arguments, i) is { } argument)
            {
                typeMap.Add(typeParameter, argument);
            }
        }

        return typeMap;
    }

    private TypeList GetTypeListOfCall(Tree.Expression.Call call)
    {
        var targetType = GetTypeOfExpression(call.Target);
        if (targetType is Type.Function function)
        {
            // TODO check whether the return types need to be instantiated at all
            if (function.IsGeneric)
            {
                var typeMap = GetGenericCallTypeMap(call, function);
                return InstantiateTypeList(function.Returns, typeMap);
            }

            return function.Returns;
        }

        return TypeList.Unknown;
    }

    internal TypeList? GetTypeListOfExpression(Tree.Expression expression)
    {
        if (expression is Tree.Expression.Call call)
        {
            return GetTypeListOfCall(call);
        }

        // TODO handle vararg

        return null;
    }

    /// <summary>
    /// Instantiates a TypeList containing generic types with the given TypeMap.
    /// </summary>
    internal TypeList InstantiateTypeList(TypeList typeList, TypeMap map)
    {
        return new TypeList.Instantiation(typeList, map);
    }

    private static bool IsTriviallyAssignableFrom(Type targetType, Type sourceType)
    {
        if (targetType == Type.Unknown || sourceType == Type.Unknown)
        {
            return true;
        }

        return targetType == sourceType;
    }

    private bool IsNillableAssignableFrom(Type.Nillable targetNillable, Type sourceType,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        if (sourceType == Type.Nil)
        {
            reason = null;
            return true;
        }

        return IsAssignableFrom(targetNillable.Inner,
            // If the target type is nillable, we don't care about the source type's nillability.
            sourceType is Type.Nillable { Inner: var sourceInner } ? sourceInner : sourceType,
            out reason);
    }

    private bool IsAssignableFromNillable(Type targetType, Type.Nillable sourceNillable,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        if (!IsAssignableFrom(targetType, Type.Nil, out reason))
        {
            return false;
        }

        if (!IsAssignableFrom(targetType, sourceNillable.Inner, out reason))
        {
            return false;
        }

        return true;
    }

    private bool IsAssignableFrom(Type.Array targetArray, Type.Array sourceArray,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        // NOTE: this means that if B is a subtype of A, B[] will be assignable to A[], which may not be desired
        // (TypeScript accepts this assignment too)
        if (!IsAssignableFrom(targetArray.ElementType, sourceArray.ElementType, out reason))
        {
            return false;
        }

        return true;
    }

    internal bool IsAssignableFrom(Type targetType, Type sourceType, [NotNullWhen(false)] out TypeMismatch? reason)
    {
        reason = null;
        if (IsTriviallyAssignableFrom(targetType, sourceType))
        {
            return true;
        }

        if (targetType is Type.Table targetTable && sourceType is Type.Table sourceTable)
        {
            return IsAssignableFrom(targetTable, sourceTable, out reason);
        }

        if (targetType is Type.Function targetFunction && sourceType is Type.Function sourceFunction)
        {
            return IsAssignableFrom(targetFunction, sourceFunction, out reason);
        }

        TypeMismatch? subReason = null;

        if (targetType is Type.Nillable targetNillable)
        {
            if (IsNillableAssignableFrom(targetNillable, sourceType, out subReason))
            {
                return true;
            }
        }
        else if (sourceType is Type.Nillable sourceNillable)
        {
            if (IsAssignableFromNillable(targetType, sourceNillable, out subReason))
            {
                return true;
            }
        }
        else if (targetType is Type.PrimitiveType primitive)
        {
            if (primitive.AssignableFunc(sourceType))
            {
                return true;
            }
        }
        else if (targetType is Type.NumberLiteral numberLiteral)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (sourceType is Type.NumberLiteral sourceLiteral &&
                numberLiteral.Literal == sourceLiteral.Literal)
            {
                return true;
            }
        }
        else if (targetType is Type.StringLiteral stringLiteral)
        {
            if (sourceType is Type.StringLiteral sourceLiteral &&
                stringLiteral.Literal == sourceLiteral.Literal)
            {
                return true;
            }
        }
        else if (targetType is Type.Array targetArray && sourceType is Type.Array sourceArray)
        {
            if (IsAssignableFrom(targetArray, sourceArray, out subReason))
            {
                return true;
            }
        }

        reason = new TypeMismatch.Primitive(TypeToString(targetType), TypeToString(sourceType))
        {
            Children = subReason != null ? [subReason] : [],
        };
        return false;
    }

    internal bool IsAssignableFrom(Type targetType, Type sourceType)
    {
        return IsAssignableFrom(targetType, sourceType, out _);
    }

    private bool IsAssignableFrom(Type.Table targetTable, Type.Table sourceTable,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        MismatchList reasons = [];

        foreach (var (targetKey, targetStringField) in targetTable.StringLiterals)
        {
            var sourceType = GetTypeOfStringFieldInTable(sourceTable, targetKey);
            if (sourceType == null)
            {
                reasons.Add(new TypeMismatch.SourceMissingKey(TypeToString(targetTable),
                    TypeToString(sourceTable),
                    '"' + targetKey + '"'));
                continue;
            }

            if (!IsAssignableFrom(GetTypeOfStringField(targetTable, targetStringField), sourceType,
                    out var valueReason))
            {
                reasons.Add(new TypeMismatch.TableKeyIncompatible('"' + targetKey + '"') { Children = [valueReason] });
            }
        }
        // TODO check number literals too

        if (reasons.Count > 0)
        {
            reason = new TypeMismatch.Primitive(TypeToString(targetTable),
                TypeToString(sourceTable)) { Children = reasons };
            return false;
        }

        reason = null;
        return true;
    }

    internal bool IsAssignableFrom(Type.Function targetFunction, Type.Function sourceFunction,
        [NotNullWhen(false)] out TypeMismatch? reason)
    {
        MismatchList reasons = [];
        if (!IsAssignableFrom(sourceFunction.Parameters, targetFunction.Parameters, out var parameterReasons,
                TypeListKind.FunctionTypeParameter))
        {
            reasons.AddRange(parameterReasons);
        }

        if (!IsAssignableFrom(targetFunction.Returns, sourceFunction.Returns, out var returnReasons,
                TypeListKind.FunctionTypeReturn))
        {
            reasons.AddRange(returnReasons);
        }

        if (reasons.Count > 0)
        {
            reason = new TypeMismatch.Primitive(TypeToString(targetFunction),
                TypeToString(sourceFunction)) { Children = reasons };
            return false;
        }

        reason = null;
        return true;
    }

    internal bool IsAssignableFrom(TypeList targets, TypeList sources,
        [NotNullWhen(false)] out MismatchList? reasons,
        TypeListKind kind,
        int targetIndex = 0)
    {
        reasons = [];

        var targetMinimum = GetTypeListMinimum(targets) - targetIndex;
        var sourceMinimum = GetTypeListMinimum(sources);
        if (sourceMinimum < targetMinimum)
        {
            reasons.Add(new TypeMismatch.NotEnoughValues(targetMinimum, sourceMinimum, kind));
            return false;
        }

        var sourcesHaveRest = DoesTypeListHaveRest(sources);
        var targetsHaveRest = DoesTypeListHaveRest(targets);

        int maximum;
        if (sourcesHaveRest && !targetsHaveRest)
        {
            maximum = GetTypeListMaximum(targets);
        }
        else if (!sourcesHaveRest && targetsHaveRest)
        {
            maximum = GetTypeListMaximum(sources) + targetIndex;
        }
        else
        {
            maximum = Math.Min(GetTypeListMaximum(targets),
                GetTypeListMaximum(sources) + targetIndex);
        }

        var sourceIndex = 0;
        for (; targetIndex < maximum; targetIndex++)
        {
            var sourceType = GetTypeInTypeList(sources, sourceIndex);
            var targetType = GetTypeInTypeList(targets, targetIndex);

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

    /// <summary>
    /// Returns the name of a value in a TypeList, if it exists.
    /// </summary>
    private string? GetNameInTypeList(TypeList typeList, int index)
    {
        return typeList switch
        {
            TypeList.Parameters { Function.Type.Parameters: var parameters } when index < parameters.Count =>
                parameters[index].Name.Value,
            TypeList.FromDeclarations { Declarations: var declarations } when index < declarations.Count =>
                declarations[index].Name.Value,
            _ => null,
        };
    }

    /// <summary>
    /// Runs the function and returns its result, only if it doesn't exist in the cache.
    /// </summary>
    /// <param name="function">The function that performs the query.</param>
    /// <param name="parameter">The parameter to pass to the function, and to check the cache with.</param>
    /// <param name="cache">The cache that will store the result.</param>
    /// <typeparam name="TParameter"></typeparam>
    /// <typeparam name="TReturn"></typeparam>
    /// <returns>The result of the query</returns>
    private static TReturn GetQueryOrCached<TParameter, TReturn>(Func<TParameter, TReturn> function,
        TParameter parameter,
        Dictionary<TParameter, TReturn> cache) where TParameter : notnull
    {
        if (cache.TryGetValue(parameter, out var cached))
        {
            return cached;
        }

        var result = function(parameter);
        cache[parameter] = result;
        return result;
    }

    private string TableToString(Type.Table table, bool multiline, string indent)
    {
        var s = "{";
        var newIndent = indent + "  ";
        var separator = multiline ? "\n" : " ";

        if (table.StringLiterals.Count > 0)
        {
            s += separator;
        }

        foreach (var (key, value) in table.StringLiterals)
        {
            if (multiline)
            {
                s += newIndent;
            }

            s +=
                $"{key}: {TypeToStringIndent(GetTypeOfStringField(table, value), multiline: multiline, indent: newIndent)},{separator}";
        }

        if (multiline)
        {
            s += indent;
        }

        return s + "}";
    }

    private string TypeListToString(TypeList typeList)
    {
        var result = "";

        var maximum = GetTypeListMaximum(typeList);
        for (var i = 0; i < maximum; i++)
        {
            if (GetNameInTypeList(typeList, i) is { } name)
            {
                result += name + ": ";
            }

            result += TypeToString(GetTypeInTypeList(typeList, i));

            if (i < maximum - 1)
            {
                result += ", ";
            }
        }

        return result;
    }

    public string FunctionSignatureToString(Type.Function function)
    {
        var parameters = TypeListToString(function.Parameters);
        var typeParameters = function.TypeParameters.Count > 0
            ? $"<{string.Join(", ", function.TypeParameters.Select(t => t.Name))}>"
            : "";
        var returns = function.Returns == TypeList.Empty ? "" : ": " + TypeListToString(function.Returns);
        return $"{typeParameters}({parameters}){returns}";
    }

    private string FunctionToString(Type.Function function)
    {
        return "function" + FunctionSignatureToString(function);
    }

    private string TypeToStringIndent(Type type, bool typeContents = false, bool multiline = false, string indent = "")
    {
        if (!typeContents && type.Name != null)
        {
            return type.Name;
        }

        return type switch
        {
            Type.NumberLiteral numberLiteral => numberLiteral.Literal.ToString(CultureInfo.InvariantCulture),
            Type.StringLiteral stringLiteral => '"' + stringLiteral.Literal + '"',
            Type.PrimitiveType => type.Name!,
            Type.Table table => TableToString(table, multiline, indent),
            Type.Function function => FunctionToString(function),
            Type.Nillable { Inner: var inner } => TypeToStringIndent(inner, typeContents, multiline, indent) + "?",
            Type.Array { ElementType: var elementType } =>
                TypeToStringIndent(elementType, typeContents, multiline, indent) + "[]",
            Type.TypeParameter typeParameter => typeParameter.Name!,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    /// <summary>
    /// Returns a string representation of the type.
    /// </summary>
    /// <param name="type">The type to convert to a string.</param>
    /// <param name="typeContents">Whether to display the type's contents as a string, even if it's behind an alias.</param>
    /// <param name="multiline">Whether the string should be spread across multiple lines.</param>
    public string TypeToString(Type type, bool typeContents = false, bool multiline = false)
    {
        return TypeToStringIndent(type, typeContents, multiline);
    }
}