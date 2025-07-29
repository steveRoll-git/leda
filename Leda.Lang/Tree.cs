namespace Leda.Lang;

/// <summary>
/// A node in an untyped abstract syntax tree.
/// </summary>
public abstract class Tree
{
    /// <summary>
    /// An interface for visiting all nodes of a tree.
    /// </summary>
    public interface IVisitor
    {
        void Visit(Do block);
        void Visit(NumericalFor numericalFor);
        void Visit(If ifStatement);
        void Visit(Assignment assignment);
        void Visit(MethodCall methodCall);
        void Visit(Call call);
        void Visit(Access access);
        void Visit(Binary binary);
        void Visit(Unary unary);
        void Visit(Function function);
        void Visit(Name name);
        void Visit(Table table);
        void Visit(Return returnStatement);
        void Visit(LocalFunctionDeclaration declaration);
        void Visit(GlobalDeclaration declaration);
        void Visit(LocalDeclaration localDeclaration);
        void Visit(RepeatUntil repeatUntil);
        void Visit(While whileLoop);
        void Visit(IteratorFor forLoop);
        void Visit(FunctionType functionType);
    }

    public interface IExpressionVisitor<T>
    {
        T VisitExpression(Function function, bool isConstant);
        T VisitExpression(MethodCall methodCall, bool isConstant);
        T VisitExpression(Call call, bool isConstant);
        T VisitExpression(Access access, bool isConstant);
        T VisitExpression(Binary binary, bool isConstant);
        T VisitExpression(Unary unary, bool isConstant);
        T VisitExpression(Name name, bool isConstant);
        T VisitExpression(Number number, bool isConstant);
        T VisitExpression(String stringValue, bool isConstant);
        T VisitExpression(Table table, bool isConstant);
        T VisitExpression(True trueValue, bool isConstant);
        T VisitExpression(False falseValue, bool isConstant);
        T VisitExpression(Nil nil, bool isConstant);
        T VisitExpression(Error error, bool isConstant);
    }

    public interface ITypeVisitor<T>
    {
        T VisitType(Name name);
        T VisitType(FunctionType function);
    }

    /// <summary>
    /// The range in the source code that this tree occupies.
    /// </summary>
    public Range Range { get; internal set; }

    /// <summary>
    /// Calls the `visitor`'s appropriate `Visit` method.
    /// </summary>
    public virtual void AcceptVisitor(IVisitor visitor) { }

    /// <summary>
    /// Calls the `visitor`'s appropriate `Visit` method.
    /// </summary>
    public virtual T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
    {
        throw new InvalidOperationException("This node is not an expression");
    }

    public virtual T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
    {
        throw new InvalidOperationException("This node is not a type");
    }

    /// <summary>
    /// An invalid tree node - returned when an error was encountered during parsing.
    /// </summary>
    public class Error : Tree
    {
        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A type declaration.
    /// </summary>
    public class TypeDeclaration : Tree
    {
        public class Union(List<TypeDeclaration> types) : TypeDeclaration
        {
            public List<TypeDeclaration> Types => types;
        }
    }

    /// <summary>
    /// The type of a function.
    /// </summary>
    public class FunctionType(List<Declaration> parameters, List<Tree>? returnTypes) : Tree
    {
        public List<Declaration> Parameters => parameters;
        public List<Tree>? ReturnTypes => returnTypes;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
        {
            return visitor.VisitType(this);
        }
    }

    /// <summary>
    /// A list of statements.
    /// </summary>
    public class Block(List<Tree> statements, List<TypeDeclaration> typeDeclarations)
    {
        public List<Tree> Statements => statements;

        /// <summary>
        /// All types that were declared in this block.
        /// </summary>
        public List<TypeDeclaration> TypeDeclarations => typeDeclarations;
    }

    /// <summary>
    /// A do-end block.
    /// </summary>
    public class Do(Block body) : Tree
    {
        public Block Body => body;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A branch in an `if` statement.
    /// </summary>
    public class IfBranch(Tree condition, Block body)
    {
        public Tree Condition => condition;
        public Block Body => body;
    }

    /// <summary>
    /// An `if` statement, with zero or more `elseif` branches and an optional `else` branch.
    /// </summary>
    public class If(IfBranch primary, List<IfBranch> elseIfs, Block? elseBody) : Tree
    {
        public IfBranch Primary => primary;
        public List<IfBranch> ElseIfs => elseIfs;
        public Block? ElseBody => elseBody;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A numerical for loop.
    /// </summary>
    public class NumericalFor(Name counter, Tree start, Tree limit, Tree? step, Block body) : Tree
    {
        public Name Counter => counter;
        public Tree Start => start;
        public Tree Limit => limit;
        public Tree? Step => step;
        public Block Body => body;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A for loop with an iterator.
    /// </summary>
    public class IteratorFor(List<Declaration> declarations, Tree iterator, Block body) : Tree
    {
        public List<Declaration> Declarations => declarations;
        public Tree Iterator => iterator;
        public Block Body => body;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A while loop.
    /// </summary>
    public class While(Tree condition, Block body) : Tree
    {
        public Tree Condition => condition;
        public Block Body => body;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A repeat-until loop.
    /// </summary>
    public class RepeatUntil(Block body, Tree condition) : Tree
    {
        public Block Body => body;
        public Tree Condition => condition;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A declaration of a named value, with an optional type.
    /// </summary>
    public class Declaration(Name name, Tree? type) : Tree
    {
        public Name Name => name;
        public Tree? Type => type;
    }

    /// <summary>
    /// Declarations of one or more local variables.
    /// </summary>
    public class LocalDeclaration(List<Declaration> declarations, List<Tree> values) : Tree
    {
        public List<Declaration> Declarations => declarations;
        public List<Tree> Values => values;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// Declarations of one or more global variables.
    /// </summary>
    public class GlobalDeclaration(List<Declaration> declarations, List<Tree> values) : Tree
    {
        public List<Declaration> Declarations => declarations;
        public List<Tree> Values => values;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A local function declaration.<br/>
    /// (This is different from a `LocalDeclaration`, because here, the function's name is made available in the body,
    /// allowing it to reference itself.)
    /// </summary>
    public class LocalFunctionDeclaration(Name name, Function function) : Tree
    {
        public Name Name => name;
        public Function Function => function;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A `return` statement, with an optional return expression.
    /// </summary>
    public class Return(Tree? expression) : Tree
    {
        public Tree? Expression => expression;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// A `break` statement.
    /// </summary>
    public class Break : Tree;

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
    /// A named reference to a variable or type.
    /// </summary>
    public class Name(string value, NameContext context) : Tree
    {
        public string Value => value;

        public NameContext Context => context;

        public override string ToString() => Value;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }

        public override T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
        {
            return visitor.VisitType(this);
        }
    }

    /// <summary>
    /// The `nil` value.
    /// </summary>
    public class Nil : Tree
    {
        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// The `true` value.
    /// </summary>
    public class True : Tree
    {
        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// The `false` value.
    /// </summary>
    public class False : Tree
    {
        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A numerical constant.
    /// </summary>
    public class Number(string value, double numberValue) : Tree
    {
        public string Value => value;
        public double NumberValue => numberValue;

        public override string ToString() => Value;

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    };

    /// <summary>
    /// A string literal.
    /// </summary>
    public class String(string value) : Tree
    {
        public string Value { get; } = value;

        public override string ToString() => Value;

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A multi-line string literal.
    /// </summary>
    public class LongString(string value, int level) : String(value)
    {
        public int Level { get; } = level;

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A field in a table constructor.
    /// </summary>
    public class TableField(Tree key, Tree value) : Tree
    {
        public Tree Key => key;
        public Tree Value => value;
    }

    /// <summary>
    /// A table constructor.
    /// </summary>
    public class Table(List<TableField> fields) : Tree
    {
        public List<TableField> Fields => fields;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A function value.
    /// </summary>
    public class Function(FunctionType type, Block body, bool isMethod) : Tree
    {
        public FunctionType Type => type;
        public Block Body => body;

        /// <summary>
        /// Whether this function was defined with a `:`.
        /// </summary>
        public bool IsMethod => isMethod;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A vararg expression (...).
    /// </summary>
    public class Vararg : Tree { }

    public abstract class Unary(Tree expression) : Tree
    {
        public Tree Expression => expression;
        public abstract string Token { get; }

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// Unary negation.
    /// </summary>
    public class Negate(Tree expression) : Unary(expression)
    {
        public override string Token => "-";
    }

    /// <summary>
    /// Unary not.
    /// </summary>
    public class Not(Tree expression) : Unary(expression)
    {
        public override string Token => "not ";
    }

    /// <summary>
    /// Unary length (#).
    /// </summary>
    public class Length(Tree expression) : Unary(expression)
    {
        public override string Token => "#";
    }

    /// <summary>
    /// A binary operator.
    /// </summary>
    public abstract class Binary(Tree left, Tree right) : Tree
    {
        public Tree Left => left;
        public Tree Right => right;
        public abstract string Token { get; }
        public abstract int Precedence { get; }

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// Addition (+).
    /// </summary>
    public class Add(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "+";
        public override int Precedence => 4;
    }

    /// <summary>
    /// Subtraction (-).
    /// </summary>
    public class Subtract(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "-";
        public override int Precedence => 4;
    }

    /// <summary>
    /// Multiplication (*).
    /// </summary>
    public class Multiply(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "*";
        public override int Precedence => 5;
    }

    /// <summary>
    /// Division (/).
    /// </summary>
    public class Divide(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "/";
        public override int Precedence => 5;
    }

    /// <summary>
    /// Modulo (%).
    /// </summary>
    public class Modulo(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "%";
        public override int Precedence => 5;
    }

    /// <summary>
    /// Power (^).
    /// </summary>
    public class Power(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "^";
        public override int Precedence => 6;
    }

    /// <summary>
    /// Concat (..).
    /// </summary>
    public class Concat(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "..";
        public override int Precedence => 3;
    }

    /// <summary>
    /// Equal (==).
    /// </summary>
    public class Equal(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "==";
        public override int Precedence => 2;
    }

    /// <summary>
    /// Not equal (~=).
    /// </summary>
    public class NotEqual(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "~=";
        public override int Precedence => 2;
    }

    /// <summary>
    /// Less equal (&lt;=).
    /// </summary>
    public class LessEqual(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "<=";
        public override int Precedence => 2;
    }

    /// <summary>
    /// Greater equal (>=).
    /// </summary>
    public class GreaterEqual(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => ">=";
        public override int Precedence => 2;
    }

    /// <summary>
    /// Less (&lt;).
    /// </summary>
    public class Less(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "<";
        public override int Precedence => 2;
    }

    /// <summary>
    /// Greater (>).
    /// </summary>
    public class Greater(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => ">";
        public override int Precedence => 2;
    }

    /// <summary>
    /// Boolean and.
    /// </summary>
    public class And(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "and";
        public override int Precedence => 1;
    }

    /// <summary>
    /// Boolean or.
    /// </summary>
    public class Or(Tree left, Tree right) : Binary(left, right)
    {
        public override string Token => "or";
        public override int Precedence => 0;
    }

    /// <summary>
    /// Indexed value access - target.key or target[key].
    /// </summary>
    public class Access(Tree target, Tree key) : Tree
    {
        public Tree Target => target;
        public Tree Key => key;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A function call.
    /// </summary>
    public class Call(Tree target, List<Tree> parameters) : Tree
    {
        public Tree Target => target;
        public List<Tree> Parameters => parameters;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// A method call using `:` syntax.
    /// </summary>
    public class MethodCall(Tree target, String funcName, List<Tree> parameters) : Tree
    {
        public Tree Target => target;
        public String FuncName => funcName;
        public List<Tree> Parameters => parameters;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
        {
            return visitor.VisitExpression(this, isConstant);
        }
    }

    /// <summary>
    /// An assignment of one or more values to one or more targets.
    /// </summary>
    public class Assignment(List<Tree> targets, List<Tree> values) : Tree
    {
        public List<Tree> Targets => targets;
        public List<Tree> Values => values;

        public override void AcceptVisitor(IVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}