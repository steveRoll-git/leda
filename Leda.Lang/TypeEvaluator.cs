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
    private readonly Dictionary<Tree.Expression.Name, Type> typeOfVariableCache = [];

    internal Type GetTypeOfExpression(Tree.Expression expression, bool isConstant = false)
    {
        // TODO incomplete
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

    internal Type.Function GetTypeOfFunction(Tree.Expression.Function function)
    {
        throw new NotImplementedException();
    }

    private Type GetTypeOfVariableUncached(Tree.Expression.Name name)
    {
        if (!source.TryGetTreeSymbol(name, out var symbol))
        {
            return Type.Unknown;
        }

        if (symbol is Symbol.LocalVariable localVariable)
        {
            var declaration = localVariable.Declaration.Declarations[localVariable.Index];

            if (declaration.Type != null)
            {
                return GetTypeOfTypeAnnotation(declaration.Type);
            }

            return GetTypeOfExpressionInList(localVariable.Declaration.Values, localVariable.Index);
        }

        return Type.Unknown;
    }

    private Type GetTypeOfExpressionInList(List<Tree.Expression> expressions, int index)
    {
        if (index < expressions.Count)
        {
            return GetTypeOfExpression(expressions[index], false);
        }

        // TODO
        return Type.Unknown;
    }

    private Type GetTypeOfTypeName(Tree.Type.Name name)
    {
        if (source.TryGetTreeSymbol(name, out var symbol))
        {
            if (symbol is Symbol.IntrinsicType intrinsicType)
            {
                return intrinsicType.Type;
            }
        }

        return Type.Unknown;
    }

    internal Type GetTypeOfTypeAnnotation(Tree.Type typeAnnotation)
    {
        if (typeAnnotation is Tree.Type.Name name)
        {
            return GetTypeOfTypeName(name);
        }

        // TODO incomplete
        return Type.Unknown;
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

    internal Type GetTypeOfVariable(Tree.Expression.Name name)
    {
        return GetQueryOrCached(GetTypeOfVariableUncached, name, typeOfVariableCache);
    }
}