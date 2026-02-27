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
        void Visit(Statement.Do block);
        void Visit(Statement.NumericalFor numericalFor);
        void Visit(Statement.If ifStatement);
        void Visit(Statement.Assignment assignment);
        void Visit(Expression.MethodCall methodCall);
        void Visit(Expression.Call call);
        void Visit(Expression.Access access);
        void Visit(Expression.Binary binary);
        void Visit(Expression.Unary unary);
        void Visit(Expression.Function function);
        void Visit(Expression.Name name);
        void Visit(Type.Name name);
        void Visit(Expression.Table table);
        void Visit(Statement.Return returnStatement);
        void Visit(Statement.LocalFunctionDeclaration declaration);
        void Visit(Statement.GlobalDeclaration declaration);
        void Visit(Statement.LocalDeclaration localDeclaration);
        void Visit(Statement.RepeatUntil repeatUntil);
        void Visit(Statement.While whileLoop);
        void Visit(Statement.IteratorFor forLoop);
        void Visit(Type.Function functionType);
        void Visit(Type.Table table);
    }

    public interface IExpressionVisitor<T>
    {
        T VisitExpression(Expression.Function function, bool isConstant);
        T VisitExpression(Expression.MethodCall methodCall, bool isConstant);
        T VisitExpression(Expression.Call call, bool isConstant);
        T VisitExpression(Expression.Access access, bool isConstant);
        T VisitExpression(Expression.Binary binary, bool isConstant);
        T VisitExpression(Expression.Unary unary, bool isConstant);
        T VisitExpression(Expression.Name name, bool isConstant);
        T VisitExpression(Expression.Number number, bool isConstant);
        T VisitExpression(Expression.String stringValue, bool isConstant);
        T VisitExpression(Expression.Table table, bool isConstant);
        T VisitExpression(Expression.True trueValue, bool isConstant);
        T VisitExpression(Expression.False falseValue, bool isConstant);
        T VisitExpression(Expression.Nil nil, bool isConstant);
        T VisitExpression(Expression.Error error, bool isConstant);
    }

    public interface ITypeVisitor<T>
    {
        T VisitType(Type.Name name);
        T VisitType(Type.Function function);
        T VisitType(Type.Table table);
        T VisitType(Type.StringLiteral stringLiteral);
        T VisitType(Type.NumberLiteral numberLiteral);
    }

    /// <summary>
    /// The range in the source code that this tree occupies.
    /// </summary>
    public Range Range { get; internal set; }

    /// <summary>
    /// Calls the `visitor`'s appropriate `Visit` method.
    /// </summary>
    public abstract void AcceptVisitor(IVisitor visitor);

    /// <summary>
    /// A tree that defines a type.
    /// </summary>
    public abstract class Type : Tree
    {
        public abstract T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor);

        /// <summary>
        /// A named reference to a type.
        /// </summary>
        public class Name(string value) : Type
        {
            public string Value => value;

            public override string ToString() => Value;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }

            public override T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
            {
                return visitor.VisitType(this);
            }
        }

        public class StringLiteral(string value) : Type
        {
            public string Value => value;

            public override T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
            {
                return visitor.VisitType(this);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        public class NumberLiteral(double value) : Type
        {
            public double Value => value;

            public override T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
            {
                return visitor.VisitType(this);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
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
        /// The type of a function.
        /// </summary>
        public class Function(List<Declaration> parameters, List<Type>? returnTypes) : Type
        {
            public List<Declaration> Parameters => parameters;
            public List<Type>? ReturnTypes => returnTypes;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }

            public override T AcceptTypeVisitor<T>(ITypeVisitor<T> visitor)
            {
                return visitor.VisitType(this);
            }
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
    /// Tree nodes that appear as statements.
    /// </summary>
    public abstract class Statement : Tree
    {
        /// <summary>
        /// An invalid tree node - returned when an error was encountered during parsing.
        /// </summary>
        public class Error : Statement
        {
            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// A do-end block.
        /// </summary>
        public class Do(Block body) : Statement
        {
            public Block Body => body;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// An `if` statement, with zero or more `elseif` branches and an optional `else` branch.
        /// </summary>
        public class If(IfBranch primary, List<IfBranch> elseIfs, Block? elseBody) : Statement
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

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// A for loop with an iterator.
        /// </summary>
        public class IteratorFor(List<Declaration> declarations, Expression iterator, Block body) : Statement
        {
            public List<Declaration> Declarations => declarations;
            public Expression Iterator => iterator;
            public Block Body => body;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// A while loop.
        /// </summary>
        public class While(Expression condition, Block body) : Statement
        {
            public Expression Condition => condition;
            public Block Body => body;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// A repeat-until loop.
        /// </summary>
        public class RepeatUntil(Block body, Expression condition) : Statement
        {
            public Block Body => body;
            public Expression Condition => condition;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// Declarations of one or more local variables.
        /// </summary>
        public class LocalDeclaration(List<Declaration> declarations, List<Expression> values) : Statement
        {
            public List<Declaration> Declarations => declarations;
            public List<Expression> Values => values;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// Declarations of one or more global variables.
        /// </summary>
        public class GlobalDeclaration(List<Declaration> declarations, List<Expression> values) : Statement
        {
            public List<Declaration> Declarations => declarations;
            public List<Expression> Values => values;

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
        public class LocalFunctionDeclaration(Expression.Name name, Expression.Function function) : Statement
        {
            public Expression.Name Name => name;
            public Expression.Function Function => function;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// A `return` statement, with an optional return value.
        /// </summary>
        public class Return(Expression? value) : Statement
        {
            public Expression? Value => value;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// A `break` statement.
        /// </summary>
        public class Break : Statement
        {
            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// An assignment of one or more values to one or more targets.
        /// </summary>
        public class Assignment(List<Expression> targets, List<Expression> values) : Statement
        {
            public List<Expression> Targets => targets;
            public List<Expression> Values => values;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// A wrapper that allows Expression.Call to be a statement.
        /// </summary>
        public class Call(Expression.Call call) : Statement
        {
            public Expression.Call CallExpr => call;

            public override void AcceptVisitor(IVisitor visitor)
            {
                CallExpr.AcceptVisitor(visitor);
            }
        }

        /// <summary>
        /// A wrapper that allows Expression.MethodCall to be a statement.
        /// </summary>
        public class MethodCall(Expression.MethodCall methodCall) : Statement
        {
            public Expression.MethodCall CallExpr => methodCall;

            public override void AcceptVisitor(IVisitor visitor)
            {
                CallExpr.AcceptVisitor(visitor);
            }
        }
    }

    /// <summary>
    /// Tree nodes that appear as expressions.
    /// </summary>
    public abstract class Expression : Tree
    {
        /// <summary>
        /// Calls the `visitor`'s appropriate `Visit` method.
        /// </summary>
        public abstract T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant);


        /// <summary>
        /// An invalid tree node - returned when an error was encountered during parsing.
        /// </summary>
        public class Error : Expression
        {
            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// A named reference to a variable.
        /// </summary>
        public class Name(string value) : Expression
        {
            public string Value => value;

            public override string ToString() => Value;

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
        /// The `nil` value.
        /// </summary>
        public class Nil : Expression
        {
            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// The `true` value.
        /// </summary>
        public class True : Expression
        {
            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// The `false` value.
        /// </summary>
        public class False : Expression
        {
            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// A numerical constant.
        /// </summary>
        public class Number(string value, double numberValue) : Expression
        {
            public string Value => value;
            public double NumberValue => numberValue;

            public override string ToString() => Value;

            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        /// <summary>
        /// A string literal.
        /// </summary>
        public class String(string value) : Expression
        {
            public string Value { get; } = value;

            public override string ToString() => Value;

            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            public override void AcceptVisitor(IVisitor visitor) { }
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
        /// A table constructor.
        /// </summary>
        public class Table(List<Table.Field> fields) : Expression
        {
            public List<Field> Fields => fields;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }

            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }

            /// <summary>
            /// A field in a table constructor.
            /// </summary>
            public class Field(Expression key, Expression value) : Tree
            {
                public Expression Key => key;
                public Expression Value => value;
                public override void AcceptVisitor(IVisitor visitor) { }
            }
        }

        /// <summary>
        /// A function value.
        /// </summary>
        public class Function(Type.Function type, Block body, bool isMethod) : Expression
        {
            public new Type.Function Type => type;
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
        public class Vararg : Expression
        {
            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                throw new NotImplementedException();
            }

            public override void AcceptVisitor(IVisitor visitor) { }
        }

        public abstract class Unary(Expression expression) : Expression
        {
            public Expression Expression => expression;
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
        public class Negate(Expression expression) : Unary(expression)
        {
            public override string Token => "-";
        }

        /// <summary>
        /// Unary not.
        /// </summary>
        public class Not(Expression expression) : Unary(expression)
        {
            public override string Token => "not ";
        }

        /// <summary>
        /// Unary length (#).
        /// </summary>
        public class Length(Expression expression) : Unary(expression)
        {
            public override string Token => "#";
        }

        /// <summary>
        /// A binary operator.
        /// </summary>
        public abstract class Binary(Expression left, Expression right) : Expression
        {
            public Expression Left => left;
            public Expression Right => right;
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
        public class Add(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "+";
            public override int Precedence => 4;
        }

        /// <summary>
        /// Subtraction (-).
        /// </summary>
        public class Subtract(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "-";
            public override int Precedence => 4;
        }

        /// <summary>
        /// Multiplication (*).
        /// </summary>
        public class Multiply(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "*";
            public override int Precedence => 5;
        }

        /// <summary>
        /// Division (/).
        /// </summary>
        public class Divide(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "/";
            public override int Precedence => 5;
        }

        /// <summary>
        /// Modulo (%).
        /// </summary>
        public class Modulo(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "%";
            public override int Precedence => 5;
        }

        /// <summary>
        /// Power (^).
        /// </summary>
        public class Power(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "^";
            public override int Precedence => 6;
        }

        /// <summary>
        /// Concat (..).
        /// </summary>
        public class Concat(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "..";
            public override int Precedence => 3;
        }

        /// <summary>
        /// Equal (==).
        /// </summary>
        public class Equal(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "==";
            public override int Precedence => 2;
        }

        /// <summary>
        /// Not equal (~=).
        /// </summary>
        public class NotEqual(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "~=";
            public override int Precedence => 2;
        }

        /// <summary>
        /// Less equal (&lt;=).
        /// </summary>
        public class LessEqual(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "<=";
            public override int Precedence => 2;
        }

        /// <summary>
        /// Greater equal (>=).
        /// </summary>
        public class GreaterEqual(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => ">=";
            public override int Precedence => 2;
        }

        /// <summary>
        /// Less (&lt;).
        /// </summary>
        public class Less(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "<";
            public override int Precedence => 2;
        }

        /// <summary>
        /// Greater (>).
        /// </summary>
        public class Greater(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => ">";
            public override int Precedence => 2;
        }

        /// <summary>
        /// Boolean and.
        /// </summary>
        public class And(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "and";
            public override int Precedence => 1;
        }

        /// <summary>
        /// Boolean or.
        /// </summary>
        public class Or(Expression left, Expression right) : Binary(left, right)
        {
            public override string Token => "or";
            public override int Precedence => 0;
        }

        /// <summary>
        /// Indexed value access - target.key or target[key].
        /// </summary>
        public class Access(Expression target, Expression key) : Expression
        {
            public Expression Target => target;
            public Expression Key => key;

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
        public class Call(Expression target, List<Expression> parameters) : Expression
        {
            public Expression Target => target;
            public List<Expression> Parameters => parameters;

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
        public class MethodCall(Expression target, String funcName, List<Expression> parameters) : Expression
        {
            public Expression Target => target;
            public String FuncName => funcName;
            public List<Expression> Parameters => parameters;

            public override void AcceptVisitor(IVisitor visitor)
            {
                visitor.Visit(this);
            }

            public override T AcceptExpressionVisitor<T>(IExpressionVisitor<T> visitor, bool isConstant)
            {
                return visitor.VisitExpression(this, isConstant);
            }
        }
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
        public override void AcceptVisitor(IVisitor visitor) { }
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
        public override void AcceptVisitor(IVisitor visitor) { }
    }
}