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

    private static Type.Table GetTypeOfTableValueUncached(Tree.Expression.Table table)
    {
        return new Type.Table(table);
    }

    private Type.Table GetTypeOfTableValue(Tree.Expression.Table table)
    {
        return GetQueryOrCached(GetTypeOfTableValueUncached, table, inferredTableCache);
    }

    private static Type.Table GetTypeOfTableAnnotationUncached(Tree.Type.Table table)
    {
        return new Type.Table(table);
    }

    private Type.Table GetTypeOfTableAnnotation(Tree.Type.Table table)
    {
        return GetQueryOrCached(GetTypeOfTableAnnotationUncached, table, tableAnnotationCache);
    }

    internal Type? GetTypeOfStringKeyInTable(Type.Table table, string key)
    {
        if (table.StringLiterals.TryGetValue(key, out var type))
        {
            return type;
        }

        Type? value = null;
        if (table.IsInferred(out var inferTree, out var typeTree))
        {
            foreach (var field in inferTree.Fields)
            {
                if (GetTypeOfExpression(field.Key, true) is Type.StringLiteral stringLiteral &&
                    stringLiteral.Literal == key)
                {
                    value = GetTypeOfExpression(field.Value);
                    break;
                }
            }
        }
        else
        {
            foreach (var pair in typeTree.Pairs)
            {
                if (GetTypeOfTypeAnnotation(pair.Key) is Type.StringLiteral stringLiteral &&
                    stringLiteral.Literal == key)
                {
                    value = GetTypeOfTypeAnnotation(pair.Value);
                    break;
                }
            }
        }

        table.StringLiterals.Add(key, value);
        return value;
    }

    /// <summary>
    /// Evaluates all the table's field types that weren't lazily evaluated before.
    /// </summary>
    internal void CompleteTableType(Type.Table table)
    {
        // TODO number literals and indexers
        if (table.IsInferred(out var inferTree, out var typeTree))
        {
            foreach (var field in inferTree.Fields)
            {
                if (GetTypeOfExpression(field.Key, true) is Type.StringLiteral stringLiteral &&
                    !table.StringLiterals.ContainsKey(stringLiteral.Literal))
                {
                    var value = GetTypeOfExpression(field.Value);
                    table.StringLiterals.Add(stringLiteral.Literal, value);
                }
            }
        }
        else
        {
            foreach (var pair in typeTree.Pairs)
            {
                if (GetTypeOfTypeAnnotation(pair.Key) is Type.StringLiteral stringLiteral &&
                    !table.StringLiterals.ContainsKey(stringLiteral.Literal))
                {
                    var value = GetTypeOfTypeAnnotation(pair.Value);
                    table.StringLiterals.Add(stringLiteral.Literal, value);
                }
            }
        }
    }

    /// <summary>
    /// Evaluates all the type's inner information that wasn't lazily evaluated before.
    /// </summary>
    private void CompleteType(Type type)
    {
        switch (type)
        {
            case Type.Table table:
                CompleteTableType(table);
                break;
        }
    }

    internal Type? GetTypeOfAccess(Tree.Expression.Access access)
    {
        var targetType = GetTypeOfExpression(access.Target);
        if (targetType is not Type.Table tableType)
        {
            return Type.Unknown;
        }

        var keyType = GetTypeOfExpression(access.Key, true);
        if (keyType is Type.StringLiteral stringLiteral)
        {
            return GetTypeOfStringKeyInTable(tableType, stringLiteral.Literal);
        }

        // TODO check number literals, indexers
        return null;
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

        // TODO infer parameter type

        return Type.Unknown;
    }

    private Type GetTypeOfSymbolUncached(Symbol symbol)
    {
        return symbol switch
        {
            Symbol.LocalVariable localVariable => GetTypeOfLocalVariable(localVariable),
            Symbol.LocalFunction localFunction => GetTypeOfFunction(localFunction.Declaration.Function),
            // A parameter symbol will always reference an existent function parameter.
            Symbol.Parameter parameter => GetTypeOfParameter(parameter.Function, parameter.Index)!,
            Symbol.NumericForCounter => Type.NumberPrimitive,
            _ => Type.Unknown
        };
    }

    public Type GetTypeOfSymbol(Symbol symbol)
    {
        return GetQueryOrCached(GetTypeOfSymbolUncached, symbol, typeOfSymbolCache);
    }

    private Type GetTypeOfVariable(Tree.Expression.Name name)
    {
        if (!source.TryGetTreeSymbol(name, out var symbol))
        {
            return Type.Unknown;
        }

        return GetTypeOfSymbol(symbol);
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

    public Type GetTypeOfTypeName(Tree.Type.Name name)
    {
        if (source.TryGetTreeSymbol(name, out var symbol))
        {
            if (symbol is Symbol.IntrinsicType intrinsicType)
            {
                return intrinsicType.Type;
            }

            if (symbol is Symbol.TypeAlias typeAlias)
            {
                return GetTypeOfTypeAlias(typeAlias);
            }
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
            _ => Type.Unknown
        };
    }

    /// <summary>
    /// Returns the effective minimum number of values produced by this expression list, including trailing values.
    /// </summary>
    internal int GetMinimumNumberOfValues(List<Tree.Expression> expressions)
    {
        return expressions.Count + (expressions.Count >= 1 && GetTypeListOfExpression(expressions[^1]) is { } typeList
            ? GetTypeListMinimum(typeList)
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

        if (typeList is TypeList.Parameters { Function: var function })
        {
            return GetTypeOfParameter(function, index);
        }

        if (typeList is TypeList.FromTypes { Types: var types })
        {
            // TODO check rest
            if (index < types.Count)
            {
                return GetTypeOfTypeAnnotation(types[index]);
            }

            return Type.Nil;
        }

        if (typeList is TypeList.FromValues { Values: var values })
        {
            // TODO check rest
            return GetTypeOfExpressionInList(values, index);
        }

        return Type.Unknown;
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
    /// Returns the name of a value in a TypeList, if it exists.
    /// </summary>
    private string? GetNameInTypeList(TypeList typeList, int index)
    {
        if (typeList is TypeList.Parameters { Function.Type: var function } && index < function.Parameters.Count)
        {
            return function.Parameters[index].Name.Value;
        }

        return null;
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
        CompleteTableType(table);

        var s = "{";
        var newIndent = indent + "  ";
        var separator = multiline ? "\n" : " ";

        if (table.StringLiterals.Count > 0)
        {
            s += separator;
        }

        foreach (var pair in table.StringLiterals)
        {
            if (pair.Value != null)
            {
                if (multiline)
                {
                    s += newIndent;
                }

                s +=
                    $"{pair.Key}: {TypeToStringIndent(pair.Value, multiline: multiline, indent: newIndent)},{separator}";
            }
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
            Type.PrimitiveType or Type.Reference or Type.TypeParameter => type.Name!,
            Type.Table table => TableToString(table, multiline, indent),
            Type.Function function => FunctionToString(function),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}