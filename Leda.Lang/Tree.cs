namespace Leda.Lang;

/// <summary>
/// A node in an untyped abstract syntax tree.
/// </summary>
public abstract class Tree
{
    /// <summary>
    /// An invalid tree node - returned when an error was encountered during parsing.
    /// </summary>
    public class Error : Tree;

    /// <summary>
    /// A list of statements.
    /// </summary>
    public class Block(List<Tree> statements) : Tree
    {
        public readonly List<Tree> Statements = statements;
    }

    /// <summary>
    /// A branch in an `if` statement.
    /// </summary>
    public class IfBranch(Tree condition, Tree body)
    {
        public Tree Condition => condition;
        public Tree Body => body;
    }

    /// <summary>
    /// An `if` statement, with zero or more `elseif` branches and an optional `else` branch.
    /// </summary>
    public class If(IfBranch primary, List<IfBranch> elseIfs, Tree? elseBody) : Tree
    {
        public IfBranch Primary => primary;
        public List<IfBranch> ElseIfs => elseIfs;
        public Tree? ElseBody => elseBody;
    }

    /// <summary>
    /// A `return` statement, with an optional return expression.
    /// </summary>
    public class Return(Tree? expression) : Tree
    {
        public readonly Tree? Expression = expression;
    }

    /// <summary>
    /// A `break` statement.
    /// </summary>
    public class Break : Tree;

    /// <summary>
    /// A named reference to a variable or type.
    /// </summary>
    public class Name(string value) : Tree
    {
        public string Value => value;
    }

    /// <summary>
    /// The `nil` value.
    /// </summary>
    public class Nil : Tree;

    /// <summary>
    /// The `true` value.
    /// </summary>
    public class True : Tree;

    /// <summary>
    /// The `false` value.
    /// </summary>
    public class False : Tree;

    /// <summary>
    /// A numerical constant.
    /// </summary>
    public class Number(string value, double numberValue) : Tree
    {
        public string Value => value;
        public double NumberValue => numberValue;
    };

    /// <summary>
    /// A string literal.
    /// </summary>
    public class String : Tree
    {
        public string Value { get; init; }

        public String(string value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// A multi-line string literal.
    /// </summary>
    public class LongString : String
    {
        public int Level { get; init; }

        public LongString(string value, int level) : base(value)
        {
            Level = level;
        }
    }

    /// <summary>
    /// A vararg expression (...).
    /// </summary>
    public class Vararg : Tree { }

    /// <summary>
    /// Unary negation.
    /// </summary>
    public class Negate(Tree expression) : Tree
    {
        public Tree Expression => expression;
    }

    /// <summary>
    /// Unary not.
    /// </summary>
    public class Not(Tree expression) : Tree
    {
        public Tree Expression => expression;
    }

    /// <summary>
    /// Unary length (#).
    /// </summary>
    public class Length(Tree expression) : Tree
    {
        public Tree Expression => expression;
    }

    /// <summary>
    /// Addition (+).
    /// </summary>
    public class Add(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    /// <summary>
    /// Subtraction (-).
    /// </summary>
    public class Subtract(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    /// <summary>
    /// Multiplication (*).
    /// </summary>
    public class Multiply(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    /// <summary>
    /// Division (/).
    /// </summary>
    public class Divide(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    /// <summary>
    /// Modulo (%).
    /// </summary>
    public class Modulo(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    /// <summary>
    /// Power (^).
    /// </summary>
    public class Power(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    public class Concat(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
    }

    /// <summary>
    /// Indexed value access - target.key or target[key].
    /// </summary>
    public class Access(Tree target, Tree key) : Tree
    {
        public Tree Target => target;
        public Tree Key => key;
    }

    /// <summary>
    /// A function call.
    /// </summary>
    public class Call(Tree target, List<Tree> parameters) : Tree
    {
        public Tree Target => target;
        public List<Tree> Parameters => parameters;
    }

    /// <summary>
    /// An assignment of one or more values to one or more targets.
    /// </summary>
    public class Assignment(List<Tree> targets, List<Tree> values) : Tree
    {
        public List<Tree> Targets => targets;
        public List<Tree> Values => values;
    }
}