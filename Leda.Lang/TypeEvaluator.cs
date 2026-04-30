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
public class TypeEvaluator(Source source)
{
    private readonly Dictionary<Symbol, Type> typeOfSymbolCache = [];
    private readonly Dictionary<Tree.Expression.Table, Type.Table> inferredTableCache = [];
    private readonly Dictionary<Tree.Type.Table, Type.Table> tableAnnotationCache = [];
    private readonly Dictionary<Tree.Expression.Function, Type.Function> functionTypeCache = [];
    private readonly Dictionary<Tree.Type.Function, Type.Function> functionAnnotationCache = [];
    private readonly Dictionary<Symbol.TypeAlias, Type> typeAliasCache = [];

    internal Type GetTypeOfExpression(Tree.Expression expression, bool isConstant = false)
    {
        switch (expression)
        {
            case Tree.Expression.Name name:
                return GetTypeOfVariable(name);
            case Tree.Expression.Number number:
                return isConstant ? new Type.NumberLiteral(number.NumberValue) : Type.NumberPrimitive;
            case Tree.Expression.String s:
                return isConstant ? new Type.StringLiteral(s.Value) : Type.StringPrimitive;
            case Tree.Expression.Function function:
                return GetTypeOfFunction(function);
            case Tree.Expression.Table table:
                return GetTypeOfTableValue(table);
            case Tree.Expression.Access access:
                return GetTypeOfAccess(access) ?? Type.Unknown;
            case Tree.Expression.Call call:
                return GetTypeInTypeList(GetTypeListOfCall(call), 0);
            case Tree.Expression.Binary binary:
                return GetTypeOfBinaryExpression(binary) ?? Type.Unknown;
            case Tree.Expression.False:
                return isConstant ? Type.False : Type.Boolean;
            case Tree.Expression.True:
                return isConstant ? Type.True : Type.Boolean;
            case Tree.Expression.Nil:
                return Type.Nil;
            case Tree.Expression.Error:
                return Type.Unknown;
        }

        throw new ArgumentOutOfRangeException(nameof(expression));
    }

    private static Type.Function GetTypeOfFunctionUncached(Tree.Expression.Function function)
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
            returns = new TypeList.FromValues(function.Chunk.ReturnStatements[0].Values);
        }
        else
        {
            returns = TypeList.Empty;
        }

        return new Type.Function(parameters, returns, []);
    }

    internal Type.Function GetTypeOfFunction(Tree.Expression.Function function)
    {
        return GetQueryOrCached(GetTypeOfFunctionUncached, function, functionTypeCache);
    }

    private Type.Table GetTypeOfTableValueUncached(Tree.Expression.Table table)
    {
        var type = new Type.Table(table);
        foreach (var field in table.Fields)
        {
            if (GetTypeOfExpression(field.Key, true) is Type.StringLiteral { Literal: var literal })
            {
                var newKey = new Type.Table.ValueStringField(new Symbol.StringField(type, literal), field);
                type.StringLiterals[literal] = newKey;
                if (field.Key is Tree.Expression.String && !source.TryGetTreeSymbol(field.Key, out _))
                {
                    source.AttachSymbol(field.Key, newKey.Symbol, true);
                }
                else
                {
                    newKey.Symbol.Definition = new(source, field.Key.Range);
                }
            }
        }

        return type;
    }

    internal Type.Table GetTypeOfTableValue(Tree.Expression.Table table)
    {
        return GetQueryOrCached(GetTypeOfTableValueUncached, table, inferredTableCache);
    }

    private Type.Table GetTypeOfTableAnnotationUncached(Tree.Type.Table table)
    {
        var type = new Type.Table(table);
        foreach (var field in table.Fields)
        {
            if (GetTypeOfTypeAnnotation(field.Key) is Type.StringLiteral { Literal: var literal })
            {
                var newKey = new Type.Table.TypeStringField(new Symbol.StringField(type, literal), field);
                type.StringLiterals[literal] = newKey;
                source.AttachSymbol(field.Key, newKey.Symbol, true);
            }
        }

        return type;
    }

    internal Type.Table GetTypeOfTableAnnotation(Tree.Type.Table table)
    {
        return GetQueryOrCached(GetTypeOfTableAnnotationUncached, table, tableAnnotationCache);
    }

    internal Type GetTypeOfStringField(Type.Table.StringField stringField)
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

        return stringField.CachedType!;
    }

    public Type? GetTypeOfStringFieldInTable(Type.Table table, string key)
    {
        if (!table.StringLiterals.TryGetValue(key, out var field))
        {
            return null;
        }

        return GetTypeOfStringField(field);
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

    internal Type? GetTypeOfAccess(Tree.Expression.Access access)
    {
        var targetType = GetTypeOfExpression(access.Target);
        if (targetType is Type.Table table)
        {
            return GetTypeOfTableAccess(table, access.Key);
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
            table.StringLiterals.TryGetValue(key, out var value);
            return value;
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

    private Type GetTypeOfLocalVariable(Symbol.LocalVariable localVariable)
    {
        var declaration = localVariable.Declaration.Declarations[localVariable.Index];

        if (declaration.Type != null)
        {
            return GetTypeOfTypeAnnotation(declaration.Type);
        }

        return GetTypeOfExpressionInList(localVariable.Declaration.Values, localVariable.Index);
    }

    private Type GetAssignmentPathType(AssignmentPath assignmentPath)
    {
        var path = assignmentPath switch
        {
            AssignmentPath.LocalVariable { LocalDeclaration.Declarations: var declarations, Index: var localIndex }
                when localIndex < declarations.Count && declarations[localIndex].Type is { } annotation =>
                GetTypeOfTypeAnnotation(annotation),
            AssignmentPath.AssignmentValue { Assignment.Targets: var targets, Index: var assignIndex }
                when assignIndex < targets.Count =>
                GetTypeOfExpression(targets[assignIndex]),
            AssignmentPath.Argument { Call.Target: var callee, Index: var argIndex }
                when GetTypeOfExpression(callee) is Type.Function function =>
                GetTypeInTypeList(function.Parameters, argIndex),
            AssignmentPath.ReturnValue { Return: var returnStmt, Index: var returnIndex }
                when returnStmt.ParentChunk.ParentFunction is { } function =>
                GetTypeInTypeList(GetTypeOfFunction(function).Return, returnIndex),
            _ => Type.Unknown
        };

        foreach (var key in assignmentPath.TableFields)
        {
            if (path is Type.Table table)
            {
                path = GetTypeOfTableAccess(table, key) ?? Type.Unknown;
            }
            else
            {
                path = Type.Unknown;
                break;
            }
        }

        return path;
    }

    // TODO this probably needs to be cached
    internal Type? GetInferredParameterType(Tree.Expression.Function function, int index)
    {
        if (function.AssignmentPath != null &&
            GetAssignmentPathType(function.AssignmentPath) is Type.Function targetFunction)
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
            Symbol.LocalVariable localVariable => GetTypeOfLocalVariable(localVariable),
            Symbol.LocalFunction localFunction => GetTypeOfFunction(localFunction.Declaration.Function),
            Symbol.Parameter parameter => GetTypeOfParameter(parameter.Function, parameter.Index),
            Symbol.NumericForCounter => Type.NumberPrimitive,
            Symbol.IntrinsicType intrinsicType => intrinsicType.Type,
            Symbol.TypeAlias typeAlias => GetTypeOfTypeAlias(typeAlias),
            _ => Type.Unknown
        };
    }

    public Type GetTypeOfSymbol(Symbol symbol)
    {
        return GetQueryOrCached(GetTypeOfSymbolUncached, symbol, typeOfSymbolCache);
    }

    private Type GetTypeOfVariable(Tree.Expression.Name name)
    {
        if (source.TryGetTreeSymbol(name, out var symbol))
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
                if (IsAssignableFrom(Type.NumberPrimitive, left) && IsAssignableFrom(Type.NumberPrimitive, right) ||
                    IsAssignableFrom(Type.StringPrimitive, left) && IsAssignableFrom(Type.StringPrimitive, right))
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
        if (type is Type.Table)
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
        if (source.TryGetTreeSymbol(name, out var symbol))
        {
            return GetTypeOfSymbol(symbol);
        }

        return Type.Unknown;
    }

    internal Type GetTypeOfTypeAnnotation(Tree.Type typeAnnotation)
    {
        return typeAnnotation switch
        {
            Tree.Type.StringLiteral stringLiteral => new Type.StringLiteral(stringLiteral.Value),
            Tree.Type.NumberLiteral numberLiteral => new Type.NumberLiteral(numberLiteral.Value),
            Tree.Type.Name name => GetTypeOfTypeName(name),
            Tree.Type.Table table => GetTypeOfTableAnnotation(table),
            Tree.Type.Function function => GetTypeOfFunctionAnnotation(function),
            Tree.Type.Nillable { Inner: var inner } => new Type.Nillable(GetTypeOfTypeAnnotation(inner)),
            _ => Type.Unknown,
        };
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

            case TypeList.FromTypes { Types: var types } when index < types.Count:
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

            default:
                return Type.Unknown;
        }
    }

    private TypeList GetTypeListOfCall(Tree.Expression.Call call)
    {
        var targetType = GetTypeOfExpression(call.Target);
        if (targetType is Type.Function { Return: var returns })
        {
            return returns;
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
    /// If the type points to another type, returns the pointed-to type.
    /// </summary>
    internal Type Dereference(Type type)
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
            sourceType is Type.Nillable { Inner: var sourceInner, } ? sourceInner : sourceType,
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

    internal bool IsAssignableFrom(Type targetType, Type sourceType, [NotNullWhen(false)] out TypeMismatch? reason)
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

            if (!IsAssignableFrom(GetTypeOfStringField(targetStringField), sourceType, out var valueReason))
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

        if (!IsAssignableFrom(targetFunction.Return, sourceFunction.Return, out var returnReasons,
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
            _ => null
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
                $"{key}: {TypeToStringIndent(GetTypeOfStringField(value), multiline: multiline, indent: newIndent)},{separator}";
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
        var returns = function.Return == TypeList.Empty ? "" : ": " + TypeListToString(function.Return);
        return $"({parameters}){returns}";
    }

    private string FunctionToString(Type.Function function)
    {
        return "function" + FunctionSignatureToString(function);
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
            Type.PrimitiveType or Type.TypeParameter => type.Name!,
            Type.Table table => TableToString(table, multiline, indent),
            Type.Function function => FunctionToString(function),
            Type.Nillable { Inner: var inner } => TypeToStringIndent(inner, typeContents, multiline, indent) + "?",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}