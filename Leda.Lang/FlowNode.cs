namespace Leda.Lang;

/// <summary>
/// Represents some location in a function's execution. Used for control flow analysis.
/// </summary>
public abstract class FlowNode
{
    /// <summary>
    /// A FlowNode that denotes the beginning of a function's execution.
    /// </summary>
    public class Start : FlowNode;

    /// <summary>
    /// A FlowNode that branching constructs can jump to.
    /// </summary>
    public class Label(List<FlowNode> antecedents) : FlowNode
    {
        /// <summary>
        /// List of FlowNodes whose execution leads to this node.
        /// </summary>
        public List<FlowNode> Antecedents => antecedents;
    }

    /// <summary>
    /// A FlowNode that has an antecedent node.
    /// </summary>
    public class Basic(FlowNode? antecedent) : FlowNode
    {
        /// <summary>
        /// The FlowNode whose execution leads to this node.
        /// </summary>
        public FlowNode? Antecedent => antecedent;
    }

    /// <summary>
    /// A FlowNode where a certain condition is known to be true or false.
    /// </summary>
    public class Condition(FlowNode? antecedent, Tree.Expression expression, bool isTrue) : Basic(antecedent)
    {
        public Tree.Expression Expression => expression;
        public bool IsTrue => isTrue;
    }
}