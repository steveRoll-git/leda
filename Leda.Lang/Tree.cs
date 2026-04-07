namespace Leda.Lang;

/// <summary>
/// A node in an untyped abstract syntax tree.
/// </summary>
public abstract class Tree
{
    /// <summary>
    /// The range in the source code that this tree occupies.
    /// </summary>
    public Range Range { get; internal set; }

    /// <summary>
    /// A tree that defines a type.
    /// </summary>
    public abstract class Type : Tree
    {
        /// <summary>
        /// A named reference to a type.
        /// </summary>
        public class Name(string value) : Type
        {
            public string Value => value;

            public override string ToString() => Value;
        }

        public class StringLiteral(string value) : Type
        {
            public string Value => value;
        }

        public class NumberLiteral(double value) : Type
        {
            public double Value => value;
        }

        /// <summary>
        /// A pair of key and value types.
        /// </summary>
        public record struct Pair(Type Key, Type Value);

        /// <summary>
        /// A list of key-value pairs of types.
        /// </summary>
        public class Table(List<Pair> pairs) : Type
        {
            public List<Pair> Pairs => pairs;
        }

        /// <summary>
        /// The type of a function.
        /// </summary>
        public class Function(List<Declaration> parameters, List<Type>? returnTypes, List<Name>? typeParameters)
            : Type
        {
            public List<Declaration> Parameters => parameters;
            public List<Type>? ReturnTypes => returnTypes;
            public List<Name>? TypeParameters => typeParameters;
        }

        // public class Union(List<Type> types) : Type
        // {
        //     public List<Type> Types => types;
        // }
    }

    /// <summary>
    /// A list of statements.
    /// </summary>
    public class Block(List<Statement> statements, List<TypeAliasDeclaration> typeDeclarations)
    {
        public List<Statement> Statements => statements;

        /// <summary>
        /// All types that were declared in this block.
        /// </summary>
        public List<TypeAliasDeclaration> TypeDeclarations => typeDeclarations;
    }

    /// <summary>
    /// The top-level block of a function or a file, which also stores all of its return statements.
    /// </summary>
    public class Chunk(
        List<Statement> statements,
        List<TypeAliasDeclaration> typeDeclarations,
        List<Statement.Return> returnStatements)
        : Block(statements, typeDeclarations)
    {
        public List<Statement.Return> ReturnStatements => returnStatements;
    }

    /// <summary>
    /// Tree nodes that appear as statements.
    /// </summary>
    public abstract class Statement : Tree
    {
        /// <summary>
        /// An invalid tree node - returned when an error was encountered during parsing.
        /// </summary>
        public class Error : Statement;

        /// <summary>
        /// A do-end block.
        /// </summary>
        public class Do(Block body) : Statement
        {
            public Block Body => body;
        }

        /// <summary>
        /// An `if` statement, with zero or more `elseif` branches and an optional `else` branch.
        /// </summary>
        public class If(IfBranch primary, List<IfBranch> elseIfs, Block? elseBody) : Statement
        {
            public IfBranch Primary => primary;
            public List<IfBranch> ElseIfs => elseIfs;
            public Block? ElseBody => elseBody;
        }

        /// <summary>
        /// A numerical for loop.
        /// </summary>
        public class NumericalFor(
            Expression.Name counter,
            Expression start,
            Expression limit,
            Expression? step,
            Block body) : Statement
        {
            public Expression.Name Counter => counter;
            public Expression Start => start;
            public Expression Limit => limit;
            public Expression? Step => step;
            public Block Body => body;
        }

        /// <summary>
        /// A for loop with an iterator.
        /// </summary>
        public class IteratorFor(List<Declaration> declarations, Expression iterator, Block body) : Statement
        {
            public List<Declaration> Declarations => declarations;
            public Expression Iterator => iterator;
            public Block Body => body;
        }

        /// <summary>
        /// A while loop.
        /// </summary>
        public class While(Expression condition, Block body) : Statement
        {
            public Expression Condition => condition;
            public Block Body => body;
        }

        /// <summary>
        /// A repeat-until loop.
        /// </summary>
        public class RepeatUntil(Block body, Expression condition) : Statement
        {
            public Block Body => body;
            public Expression Condition => condition;
        }

        /// <summary>
        /// Declarations of one or more local variables.
        /// </summary>
        public class LocalDeclaration(List<Declaration> declarations, List<Expression> values) : Statement
        {
            public List<Declaration> Declarations => declarations;
            public List<Expression> Values => values;
        }

        /// <summary>
        /// Declarations of one or more global variables.
        /// </summary>
        public class GlobalDeclaration(List<Declaration> declarations, List<Expression> values) : Statement
        {
            public List<Declaration> Declarations => declarations;
            public List<Expression> Values => values;
        }

        /// <summary>
        /// A local function declaration.<br/>
        /// (This is different from a `LocalDeclaration`, because here, the function's name is made available in the body,
        /// allowing it to reference itself.)
        /// </summary>
        public class LocalFunctionDeclaration(Expression.Name name, Expression.Function function) : Statement
        {
            public Expression.Name Name => name;
            public Expression.Function Function => function;
        }

        /// <summary>
        /// A `return` statement, with optional return values.
        /// </summary>
        public class Return(List<Expression> values) : Statement
        {
            public List<Expression> Values => values;
        }

        /// <summary>
        /// A `break` statement.
        /// </summary>
        public class Break : Statement;

        /// <summary>
        /// An assignment of one or more values to one or more targets.
        /// </summary>
        public class Assignment(List<Expression> targets, List<Expression> values) : Statement
        {
            public List<Expression> Targets => targets;
            public List<Expression> Values => values;
        }

        /// <summary>
        /// A wrapper that allows Expression.Call to be a statement.
        /// </summary>
        public class Call(Expression.Call call) : Statement
        {
            public Expression.Call CallExpr => call;
        }

        /// <summary>
        /// A wrapper that allows Expression.MethodCall to be a statement.
        /// </summary>
        public class MethodCall(Expression.MethodCall methodCall) : Statement
        {
            public Expression.MethodCall CallExpr => methodCall;
        }
    }

    /// <summary>
    /// Tree nodes that appear as expressions.
    /// </summary>
    public abstract class Expression : Tree
    {
        /// <summary>
        /// An invalid tree node - returned when an error was encountered during parsing.
        /// </summary>
        public class Error : Expression;

        /// <summary>
        /// A named reference to a variable.
        /// </summary>
        public class Name(string value) : Expression
        {
            public string Value => value;

            public override string ToString() => Value;
        }

        /// <summary>
        /// The `nil` value.
        /// </summary>
        public class Nil : Expression;

        /// <summary>
        /// The `true` value.
        /// </summary>
        public class True : Expression;

        /// <summary>
        /// The `false` value.
        /// </summary>
        public class False : Expression;

        /// <summary>
        /// A numerical constant.
        /// </summary>
        public class Number(string value, double numberValue) : Expression
        {
            public string Value => value;
            public double NumberValue => numberValue;

            public override string ToString() => Value;
        }

        /// <summary>
        /// A string literal.
        /// </summary>
        public class String(string value) : Expression
        {
            public string Value { get; } = value;

            public override string ToString() => Value;
        }

        /// <summary>
        /// A multi-line string literal.
        /// </summary>
        public class LongString(string value, int level) : String(value)
        {
            public int Level { get; } = level;
        }

        /// <summary>
        /// A table constructor.
        /// </summary>
        public class Table(List<Table.Field> fields) : Expression
        {
            public List<Field> Fields => fields;


            /// <summary>
            /// A field in a table constructor.
            /// </summary>
            public class Field(Expression key, Expression value) : Tree
            {
                public Expression Key => key;
                public Expression Value => value;
            }
        }

        /// <summary>
        /// A function value.
        /// </summary>
        public class Function(Type.Function type, Chunk chunk, bool isMethod)
            : Expression
        {
            public new Type.Function Type => type;
            public Chunk Chunk => chunk;

            /// <summary>
            /// Whether this function was defined with a `:`.
            /// </summary>
            public bool IsMethod => isMethod;
        }

        /// <summary>
        /// A vararg expression (...).
        /// </summary>
        public class Vararg : Expression;

        public class Unary(Expression expression, Token op) : Expression
        {
            public Expression Expression => expression;
            public Token Operator => op;
        }

        /// <summary>
        /// A binary operator.
        /// </summary>
        public class Binary(Expression left, Expression right, Token op) : Expression
        {
            public Expression Left => left;
            public Expression Right => right;
            public Token Operator => op;
        }

        /// <summary>
        /// Indexed value access - target.key or target[key].
        /// </summary>
        public class Access(Expression target, Expression key) : Expression
        {
            public Expression Target => target;
            public Expression Key => key;
        }

        /// <summary>
        /// A function call.
        /// </summary>
        public class Call(Expression target, List<Expression> parameters, List<Type>? typeParameters) : Expression
        {
            public Expression Target => target;
            public List<Expression> Parameters => parameters;
            public List<Type>? TypeParameters => typeParameters;
        }

        /// <summary>
        /// A method call using `:` syntax.
        /// </summary>
        public class MethodCall(Expression target, String funcName, List<Expression> parameters) : Expression
        {
            public Expression Target => target;
            public String FuncName => funcName;
            public List<Expression> Parameters => parameters;
        }

        /// <summary>
        /// Whether this expression is one that may return multiple values.
        /// </summary>
        public bool MayReturnMultiple => this is Call or Vararg;
    }

    /// <summary>
    /// A branch in an `if` statement.
    /// </summary>
    public class IfBranch(Expression condition, Block body)
    {
        public Expression Condition => condition;
        public Block Body => body;
    }

    /// <summary>
    /// A declaration of a named value, with an optional type.
    /// </summary>
    public class Declaration(Expression.Name name, Type? type) : Tree
    {
        public Expression.Name Name => name;
        public Type? Type => type;
    }

    /// <summary>
    /// The contexts in which a Name can appear.
    /// </summary>
    public enum NameContext
    {
        /// <summary>
        /// The name references a value.
        /// </summary>
        Value,

        /// <summary>
        /// The name references a type.
        /// </summary>
        Type
    }

    /// <summary>
    /// A declaration of a type alias.
    /// </summary>
    public class TypeAliasDeclaration(Type.Name name, Type type) : Statement
    {
        public Type.Name Name => name;
        public Type Type => type;
    }
}

public static class ExpressionListExtensions
{
    /// <summary>
    /// Returns whether the last value in a list of expressions may return multiple values.
    /// </summary>
    public static bool MayReturnTrailingValues(this List<Tree.Expression> expressions) =>
        expressions.Count >= 1 && expressions[^1].MayReturnMultiple;
}