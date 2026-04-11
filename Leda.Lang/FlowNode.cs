namespace Leda.Lang;

/// <summary>
/// Represents some location in a function's execution. Used for control flow analysis.
/// </summary>
public class FlowNode(List<FlowNode> antecedents)
{
    /// <summary>
    /// List of flow nodes whose execution flows to this node.
    /// </summary>
    public List<FlowNode> Antecedents => antecedents;
}