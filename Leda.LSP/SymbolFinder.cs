using Leda.Lang;
using Range = Leda.Lang.Range;
using Type = Leda.Lang.Type;

namespace Leda.LSP;

public static class SymbolFinder
{
    /// <summary>
    /// Returns whether the tree lies under the given position, and symbol information may be derived from it.
    /// </summary>
    private static bool IsSymbolNode(Tree tree, Position position)
    {
        return tree.Range.Contains(position) &&
               tree is Tree.Expression.Name or Tree.Type.Name or Tree.LabelName or Tree.Expression.String
                   or Tree.Type.StringLiteral;
    }

    /// <summary>
    /// Information about a tree node.
    /// </summary>
    /// <param name="Symbol">The symbol that the tree node is associated with.</param>
    /// <param name="Range">The range of the tree node.</param>
    /// <param name="GetTreeType">A function that returns the type of the tree node.</param>
    public record struct GetSymbolResult(Symbol? Symbol, Range Range, Func<TypeEvaluator, Type>? GetTreeType);

    /// <summary>
    /// Returns information about the tree node under the given position, if it exists.
    /// </summary>
    public static GetSymbolResult? GetSymbolAtPosition(Project project, Source source, Position position)
    {
        var result = Visitor.Search<(Tree, Func<TypeEvaluator, Type>?)?>(source, tree =>
        {
            if (tree is Tree.Expression.Access access && access.Key.Range.Contains(position) &&
                access.Key is Tree.Expression.String)
            {
                // When hovering over the key of an access, we want to show the type of the access itself.
                return (access.Key, ev => ev.GetTypeOfExpression(access));
            }

            if (tree is Tree.Expression.Table table)
            {
                foreach (var field in table.Fields)
                {
                    if (IsSymbolNode(field.Key, position))
                    {
                        return (field.Key,
                            table.ValueLocation != null
                                ? ev =>
                                    ev.GetTypeAtValueLocation(new ValueLocation.TableField(field, table.ValueLocation))
                                : null);
                    }
                }
            }
            else if (tree is Tree.Type.Table tableType)
            {
                foreach (var field in tableType.Fields)
                {
                    if (IsSymbolNode(field.Key, position))
                    {
                        return (field.Key, ev => ev.GetTypeOfTypeAnnotation(field.Value));
                    }
                }
            }
            else if (IsSymbolNode(tree, position))
            {
                // If the tree is a variable, we'd like to show its type at the FlowNode it's in.
                return (tree, tree is Tree.Expression.Name name ? ev => ev.GetTypeOfExpression(name) : null);
            }

            return null;
        }, tree => tree.Range.Contains(position));

        if (result is var (foundTree, getType))
        {
            return new GetSymbolResult(project.GetTreeSymbol(foundTree), foundTree.Range, getType);
        }

        return null;
    }
}